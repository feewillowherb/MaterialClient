using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Services;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别服务接口
///     支持多设备管理、被动监听与主动抓拍（<see cref="ILprDevice.TriggerCaptureAsync"/>）
/// </summary>
public interface IHikvisionLprService : ILprDevice
{
    /// <summary>
    ///     添加或更新设备配置
    ///     如果设备已存在则更新配置，否则添加新设备
    /// </summary>
    /// <param name="config">设备配置</param>
    void AddOrUpdateDevice(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     检查设备是否在线
    ///     尝试连接设备并验证连接状态
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>设备是否在线</returns>
    bool IsOnline(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     启动监听服务
    ///     从 SystemSettings.Urls 获取监听地址和端口，启动监听服务，可接收多个海康设备的车牌识别数据
    /// </summary>
    /// <returns>启动是否成功</returns>
    Task<bool> StartAsync();

    /// <summary>
    ///     停止监听服务
    /// </summary>
    Task StopAsync();
}

/// <summary>
///     海康威视车牌识别服务实现
///     通过 HCNetSDK 与海康设备通信，接收车牌识别结果
///     支持被动捕获（设备推送）和主动捕获（应用触发）
/// </summary>
public class HikvisionLprService : IHikvisionLprService, ILprDevice, ISingletonDependency, IAsyncDisposable
{
    private static readonly TimeSpan ShootCallbackWarningTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, LicensePlateRecognitionConfig> _deviceConfigs = new();
    private readonly ConcurrentDictionary<string, byte> _heldSessionKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingShootByDeviceIp = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHikvisionLoginSessionStore _sessionStore;
    private readonly ILogger<HikvisionLprService>? _logger;
    private readonly ISettingsService _settingsService;
    private readonly ILocalEventBus _localEventBus;
    private GCHandle? _callbackHandle;
    private bool _isInitialized;
    private int _listenHandle = -1;
    private WeighingMode _cachedWeighingMode = WeighingMode.Standard;
    private bool _cachedHasCameraConfigs = true;
    private string? _listenLocalIp;
    private int _listenLocalPort;

    public HikvisionLprService(
        ISettingsService settingsService,
        ILocalEventBus localEventBus,
        IHikvisionLoginSessionStore? sessionStore = null,
        ILogger<HikvisionLprService>? logger = null)
    {
        _settingsService = settingsService;
        _localEventBus = localEventBus;
        _sessionStore = sessionStore ?? new HikvisionLoginSessionStore(logger: null);
        _logger = logger;
        _sessionStore.SessionsClearedForSdkReset += OnSessionsClearedForSdkReset;
        _sessionStore.SdkReinitialized += OnSdkReinitialized;
    }

    /// <summary>
    ///     海康威视设备支持主动抓拍
    /// </summary>
    public bool SupportsActiveCapture => true;

    /// <summary>
    ///     添加或更新设备配置
    /// </summary>
    public void AddOrUpdateDevice(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid())
        {
            throw new ArgumentException("设备配置无效", nameof(config));
        }

        _deviceConfigs.AddOrUpdate(config.Ip, config, (_, __) => config);
        _logger?.LogInformation("设备配置已添加/更新: IP={Ip}, Name={Name}, Direction={Direction}",
            config.Ip, config.Name, config.Direction);
    }

    /// <summary>
    ///     检查设备是否在线（缓存会话上 CHECK_USER_STATUS；无会话时 Acquire，禁止 Login→Logout）
    /// </summary>
    public bool IsOnline(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        EnsureInitialized();

        if (!TryParsePort(config, out var port))
            return false;

        var key = _sessionStore.BuildKey(config.Ip, port, config.UserName ?? string.Empty);
        if (_sessionStore.TryGetUserId(key, out _))
        {
            var probe = _sessionStore.Probe(key);
            if (probe.Valid)
            {
                _logger?.LogDebug("设备在线检查成功(探活): IP={Ip}, Key={Key}", config.Ip, key);
                return true;
            }

            _logger?.LogWarning(
                "设备探活失败，将 Invalidate 后重登: IP={Ip}, Key={Key}, ErrorCode={ErrorCode}, Message={Message}",
                config.Ip, key, probe.ErrorCode, probe.Message);
            _sessionStore.Invalidate(key);
            _heldSessionKeys.TryRemove(key, out _);
        }

        var acquire = _sessionStore.Acquire(config.Ip, port, config.UserName ?? string.Empty, config.Password ?? string.Empty);
        if (!acquire.Success)
        {
            _logger?.LogWarning("设备离线: IP={Ip}, Message={Message}", config.Ip, acquire.Message);
            return false;
        }

        _heldSessionKeys[key] = 0;
        _logger?.LogDebug("设备在线检查成功(Acquire): IP={Ip}, UserId={UserId}", config.Ip, acquire.UserId);
        return true;
    }

    /// <summary>
    ///     启动监听服务
    /// </summary>
    public async Task<bool> StartAsync()
    {
        await Task.CompletedTask; // 保持方法签名为异步

        // 检查是否已经启动
        if (_listenHandle >= 0)
        {
            _logger?.LogWarning("监听服务已经启动，无需重复启动");
            return false;
        }

        try
        {
            // 从 SystemSettings.Urls 获取监听地址和端口
            var settings = await _settingsService.GetSettingsAsync();
            var urls = settings.SystemSettings.Urls;

            // 缓存当前称重模式（用于 Lpr 附件保存判断）
            _cachedWeighingMode = settings.SystemSettings.DefaultWeighingMode;
            _cachedHasCameraConfigs = settings.CameraConfigs.Count > 0;
            
            if (string.IsNullOrWhiteSpace(urls))
            {
                _logger?.LogError("SystemSettings.Urls 为空，无法启动监听服务");
                return false;
            }

            // 解析 URL，提取 IP 和端口
            var (listenLocalIp, listenLocalPort) = ParseUrl(urls);
            
            if (string.IsNullOrWhiteSpace(listenLocalIp))
            {
                _logger?.LogError("无法从 SystemSettings.Urls 解析 IP 地址: {Urls}", urls);
                return false;
            }

            if (listenLocalPort <= 0 || listenLocalPort > 65535)
            {
                _logger?.LogError("从 SystemSettings.Urls 解析的端口无效: {Port}, Urls={Urls}", listenLocalPort, urls);
                return false;
            }

            // 确保 SDK 已初始化
            EnsureInitialized();

            // 创建回调委托
            HikvisionSdk.MSGCallBack callback = MessageCallback;

            // CRITICAL: 使用 GCHandle 钉住委托，防止垃圾回收
            // 非托管 SDK 只存储函数指针，GC 无法知道它仍在使用
            _callbackHandle = GCHandle.Alloc(callback);

            // 启动监听
            _listenHandle = HikvisionSdk.NET_DVR_StartListen_V30(listenLocalIp, (ushort)listenLocalPort,
                callback, IntPtr.Zero);

            if (_listenHandle < 0)
            {
                var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                _logger?.LogError("启动监听失败: IP={Ip}, Port={Port}, ErrorCode={ErrorCode}, ErrorDesc={ErrorDesc}",
                    listenLocalIp, listenLocalPort, errorCode, GetErrorDescription(errorCode));

                // 释放 GCHandle
                if (_callbackHandle.HasValue)
                {
                    _callbackHandle.Value.Free();
                    _callbackHandle = null;
                }

                _listenHandle = -1;
                return false;
            }

            _listenLocalIp = listenLocalIp;
            _listenLocalPort = listenLocalPort;
            _logger?.LogInformation("监听服务启动成功: IP={Ip}, Port={Port}, ListenHandle={Handle}",
                listenLocalIp, listenLocalPort, _listenHandle);

            await AcquireConfiguredLprSessionsAsync(settings).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "启动监听服务时发生异常");

            // 清理资源
            if (_callbackHandle.HasValue)
            {
                _callbackHandle.Value.Free();
                _callbackHandle = null;
            }

            _listenHandle = -1;
            return false;
        }
    }

    /// <summary>
    ///     解析 URL，提取 IP 地址和端口
    /// </summary>
    private (string ip, int port) ParseUrl(string url)
    {
        try
        {
            // 如果没有协议前缀，自动添加 http://
            var urlToParse = url.Trim();
            if (!urlToParse.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !urlToParse.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urlToParse = "http://" + urlToParse;
            }

            var uri = new Uri(urlToParse);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 80; // 默认端口 80

            // 如果 host 是 localhost，转换为 0.0.0.0（监听所有接口）
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                host = "0.0.0.0";
            }

            return (host, port);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "解析 URL 失败: {Url}", url);
            return (string.Empty, 0);
        }
    }

    /// <summary>
    ///     停止监听服务
    /// </summary>
    public async Task StopAsync()
    {
        await Task.CompletedTask; // 保持方法签名为异步

        try
        {
            if (_listenHandle >= 0)
            {
                var success = HikvisionSdk.NET_DVR_StopListen_V30(_listenHandle);

                if (!success)
                {
                    var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                    _logger?.LogWarning("停止监听失败: ListenHandle={Handle}, ErrorCode={ErrorCode}, ErrorDesc={ErrorDesc}",
                        _listenHandle, errorCode, GetErrorDescription(errorCode));
                }
                else
                {
                    _logger?.LogInformation("监听服务已停止: ListenHandle={Handle}", _listenHandle);
                }
            }
            else
            {
                _logger?.LogDebug("监听服务未启动，跳过 StopListen");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "停止监听服务时发生异常: ListenHandle={Handle}", _listenHandle);
        }
        finally
        {
            if (_callbackHandle.HasValue)
            {
                _callbackHandle.Value.Free();
                _callbackHandle = null;
            }

            _listenHandle = -1;
            ReleaseHeldSessions();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sessionStore.SessionsClearedForSdkReset -= OnSessionsClearedForSdkReset;
        _sessionStore.SdkReinitialized -= OnSdkReinitialized;
        await StopAsync();
    }

    /// <summary>
    ///     测试专用：回放 SDK 报警回调，无需 NET_DVR_StartListen_V30 或物理设备。
    /// </summary>
    /// <param name="lCommand">报警命令，如 COMM_UPLOAD_PLATE_RESULT / COMM_ITS_PLATE_RESULT</param>
    /// <param name="pAlarmer">NET_DVR_ALARMER 非托管指针</param>
    /// <param name="pAlarmInfo">NET_DVR_PLATE_RESULT 或 NET_ITS_PLATE_RESULT 非托管指针</param>
    /// <param name="dwBufLen">报警数据长度</param>
    /// <param name="weighingMode">可选，覆盖称重模式（如 UrbanMode 以测试 Lpr 图片保存）</param>
    internal void InvokePlateAlarmCallbackForTests(
        int lCommand,
        IntPtr pAlarmer,
        IntPtr pAlarmInfo,
        uint dwBufLen,
        WeighingMode? weighingMode = null)
    {
        if (weighingMode.HasValue)
        {
            _cachedWeighingMode = weighingMode.Value;
        }

        MessageCallback(lCommand, pAlarmer, pAlarmInfo, dwBufLen, IntPtr.Zero);
    }

    /// <summary>
    ///     消息回调函数
    ///     CRITICAL: 整个回调必须用 try-catch 包裹，防止未处理异常导致进程崩溃
    /// </summary>
    private void MessageCallback(int lCommand, IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser)
    {
        try
        {
            // 根据命令类型分发到不同的处理方法
            switch (lCommand)
            {
                case HikvisionSdk.COMM_UPLOAD_PLATE_RESULT:
                    HandlePlateResult(pAlarmer, pAlarmInfo, dwBufLen);
                    break;

                case HikvisionSdk.COMM_ITS_PLATE_RESULT:
                    HandleItsPlateResult(pAlarmer, pAlarmInfo, dwBufLen);
                    break;

                default:
                    // 忽略其他消息
                    _logger?.LogDebug("收到未处理的消息: Command={Command}", lCommand);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 必须捕获所有异常，防止非托管回调崩溃进程
            _logger?.LogError(ex, "消息回调异常: Command={Command}", lCommand);
        }
    }

    /// <summary>
    ///     处理车牌识别结果 (COMM_UPLOAD_PLATE_RESULT)
    /// </summary>
    private void HandlePlateResult(IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen)
    {
        try
        {
            var alarmer = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_ALARMER>(pAlarmer);
            var deviceIp = ResolveDeviceIp(alarmer);
            MarkShootCallbackReceived(deviceIp);
            _deviceConfigs.TryGetValue(deviceIp, out var config);

            var plateResult = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_PLATE_RESULT>(pAlarmInfo);
            var plateRaw = HikvisionEncodingHelper.GetString(plateResult.struPlateInfo.sLicense, _logger);
            var license = HikvisionPlateNumberHelper.ParseLicense(plateRaw, MapPlateColor(plateResult.struPlateInfo));
            if (!string.Equals(plateRaw?.Trim(), license.PlateNumber, StringComparison.Ordinal))
            {
                _logger?.LogDebug(
                    "已解析 sLicense: Raw={Raw}, Plate={Plate}, PlateColor={PlateColor}",
                    plateRaw, license.PlateNumber, license.PlateColor);
            }
            var vehicleColor = MapVehicleColor(plateResult.struVehicleInfo.byColor);
            var vehicleType = MapVehicleType(plateResult.byVehicleType);
            var hasImage = (plateResult.dwPicLen != 0 && plateResult.pBuffer1 != IntPtr.Zero)
                           || (plateResult.dwFarCarPicLen != 0 && plateResult.pBuffer5 != IntPtr.Zero);

            _logger?.LogDebug(
                "COMM_UPLOAD_PLATE_RESULT: Plate={Plate}, VehicleColor={VehicleColor}, VehicleType={VehicleType}, PlateColor={PlateColor}, HasImage={HasImage}",
                license.PlateNumber, vehicleColor, vehicleType, license.PlateColor, hasImage);

            IntPtr imagePtr = IntPtr.Zero;
            var imageLen = 0;
            if (plateResult.dwPicLen != 0 && plateResult.pBuffer1 != IntPtr.Zero)
            {
                imagePtr = plateResult.pBuffer1;
                imageLen = (int)plateResult.dwPicLen;
            }
            else if (plateResult.dwFarCarPicLen != 0 && plateResult.pBuffer5 != IntPtr.Zero)
            {
                imagePtr = plateResult.pBuffer5;
                imageLen = (int)plateResult.dwFarCarPicLen;
            }

            ProcessRecognizedPlate(license.PlateNumber, deviceIp, config, vehicleColor, vehicleType, license.PlateColor,
                imagePtr, imageLen, "upload", "收到车牌识别结果", "无效车牌，仅保留 Lpr 图片");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理车牌识别结果失败");
        }
    }

    /// <summary>
    ///     处理 ITS 车牌识别结果 (COMM_ITS_PLATE_RESULT)
    /// </summary>
    private void HandleItsPlateResult(IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen)
    {
        try
        {
            var alarmer = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_ALARMER>(pAlarmer);
            var deviceIp = ResolveDeviceIp(alarmer);
            MarkShootCallbackReceived(deviceIp);
            _deviceConfigs.TryGetValue(deviceIp, out var config);

            var itsResult = Marshal.PtrToStructure<HikvisionSdk.NET_ITS_PLATE_RESULT>(pAlarmInfo);
            var plateRaw = HikvisionEncodingHelper.GetString(itsResult.struPlateInfo.sLicense, _logger);
            var license = HikvisionPlateNumberHelper.ParseLicense(plateRaw, MapPlateColor(itsResult.struPlateInfo));
            if (!string.Equals(plateRaw?.Trim(), license.PlateNumber, StringComparison.Ordinal))
            {
                _logger?.LogDebug(
                    "已解析 sLicense: Raw={Raw}, Plate={Plate}, PlateColor={PlateColor}",
                    plateRaw, license.PlateNumber, license.PlateColor);
            }
            var vehicleColor = MapVehicleColor(itsResult.struVehicleInfo.byColor);
            var vehicleType = MapVehicleType(itsResult.struVehicleInfo.byVehicleType);

            IntPtr imagePtr = IntPtr.Zero;
            var imageLen = 0;
            var hasImage = false;
            var picCount = Math.Min((int)itsResult.dwPicNum, itsResult.struPicInfo.Length);
            for (var i = 0; i < picCount; i++)
            {
                var picInfo = itsResult.struPicInfo[i];
                if (picInfo.dwDataLen == 0 || picInfo.byType != HikvisionSdk.HikItsPictureTypeScene)
                {
                    continue;
                }

                hasImage = true;
                imagePtr = picInfo.pBuffer;
                imageLen = (int)picInfo.dwDataLen;
                break;
            }

            _logger?.LogDebug(
                "COMM_ITS_PLATE_RESULT: Plate={Plate}, VehicleColor={VehicleColor}, VehicleType={VehicleType}, PlateColor={PlateColor}, HasImage={HasImage}",
                license.PlateNumber, vehicleColor, vehicleType, license.PlateColor, hasImage);

            ProcessRecognizedPlate(license.PlateNumber, deviceIp, config, vehicleColor, vehicleType, license.PlateColor,
                imagePtr, imageLen, "its", "收到 ITS 车牌识别结果", "无效车牌，仅保留 Lpr 图片");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理 ITS 车牌识别结果失败");
        }
    }

    private static string ResolveDeviceIp(HikvisionSdk.NET_DVR_ALARMER alarmer)
    {
        if (alarmer.byDeviceIPValid == 0 || alarmer.sDeviceIP is not { Length: > 0 })
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(alarmer.sDeviceIP).TrimEnd('\0');
    }

    private static bool TryValidatePlateNumber(string plateNumber)
    {
        return !string.IsNullOrWhiteSpace(plateNumber) && !plateNumber.Contains("车牌");
    }

    /// <summary>
    ///     处理识别结果：有效车牌正常发布事件；无效车牌仍尝试保存 Lpr 图片，有图时以空车牌号发布事件。
    /// </summary>
    private void ProcessRecognizedPlate(
        string plateNumber,
        string deviceIp,
        LicensePlateRecognitionConfig? config,
        string? vehicleColor,
        string? vehicleType,
        string? plateColor,
        IntPtr imagePtr,
        int imageLen,
        string debugImageSource,
        string validPlateLogMessage,
        string invalidPlateLogMessage)
    {
        var isValidPlate = TryValidatePlateNumber(plateNumber);
        var lrpPath = TrySaveLprAttachment(imagePtr, imageLen, plateNumber);

        if (!isValidPlate)
        {
            _logger?.LogWarning(
                "无效车牌，已尝试保留 Lpr 图片: Plate={Plate}, LprPath={LprPath}, DeviceIp={DeviceIp}",
                plateNumber, lrpPath ?? "(无图)", deviceIp);

#if DEBUG
            TrySaveInvalidPlateDebugImage(imagePtr, imageLen, plateNumber, debugImageSource, deviceIp);
#endif
            if (!string.IsNullOrWhiteSpace(lrpPath))
            {
                PublishPlateRecognizedEvent(string.Empty, deviceIp, config, vehicleColor, vehicleType, plateColor,
                    lrpPath, invalidPlateLogMessage);
            }

            return;
        }

        PublishPlateRecognizedEvent(plateNumber, deviceIp, config, vehicleColor, vehicleType, plateColor, lrpPath,
            validPlateLogMessage);
    }

    private void PublishPlateRecognizedEvent(
        string plateNumber,
        string deviceIp,
        LicensePlateRecognitionConfig? config,
        string? vehicleColor,
        string? vehicleType,
        string? plateColor,
        string? lrpPath,
        string logMessageTemplate)
    {
        var eventData = new LicensePlateRecognizedEventData
        {
            PlateNumber = plateNumber,
            ColorType = null,
            VehicleColor = vehicleColor,
            VehicleType = vehicleType,
            PlateColor = plateColor,
            DeviceType = LprDeviceType.Hikvision,
            DeviceName = config?.Name ?? (string.IsNullOrWhiteSpace(deviceIp) ? "Unknown" : $"Unknown ({deviceIp})"),
            Timestamp = DateTime.Now,
            LprImagePath = lrpPath
        };
        _ = _localEventBus.PublishAsync(eventData);

        _logger?.LogInformation(
            "{LogMessage}: Device={Device}, Plate={Plate}, Direction={Direction}, Time={Time}",
            logMessageTemplate, eventData.DeviceName, eventData.PlateNumber,
            config?.Direction ?? LicensePlateDirection.A, eventData.Timestamp);
    }

    /// <summary>
    ///     尝试保存 Lpr 车牌识别图片
    ///     从海康威视 SDK 回调的 pBuffer 提取图片数据，压缩后保存到磁盘
    /// </summary>
    /// <param name="pBuffer">SDK 回调中的图片数据指针</param>
    /// <param name="picLen">图片数据长度</param>
    /// <param name="plateNumber">车牌号（用于文件名）</param>
    /// <returns>保存的相对路径，保存失败时返回 null</returns>
    private string? TrySaveLprAttachment(IntPtr pBuffer, int picLen, string plateNumber)
    {
        if (pBuffer == IntPtr.Zero || picLen <= 0)
            return null;

        try
        {
            // 从非托管内存复制图片字节
            var imageBytes = new byte[picLen];
            Marshal.Copy(pBuffer, imageBytes, 0, picLen);

            // 使用 JpegCompressionUtil 压缩（Lpr 专用质量）
            var compressedBytes = JpegCompressionUtil.TryCompressJpegBytes(
                imageBytes, JpegCompressionUtil.LprCompressionQuality, _logger);
            var finalBytes = compressedBytes ?? imageBytes;

            // Save under Lpr/{yyyy}/{MM}/{dd}/ (same dated layout as Camera)
            var relativeDir = AttachmentPathUtils.GetLocalStoragePath(AttachType.Lpr).TrimEnd('/', '\\');
            var lrpDir = PathManager.EnsureDirectoryExists(relativeDir);
            var safePlate = string.IsNullOrWhiteSpace(plateNumber) ? "unknown" : plateNumber;
            var fileName = $"{safePlate}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            var filePath = Path.Combine(lrpDir, fileName);
            File.WriteAllBytes(filePath, finalBytes);

            var relativePath = PathManager.ToRelativePath(filePath);
            _logger?.LogInformation("已保存 Lpr 附件: {Path} ({Size} bytes, 原始 {Original} bytes)",
                relativePath, finalBytes.Length, imageBytes.Length);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "保存 Lpr 附件失败: Plate={Plate}", plateNumber);
            return null;
        }
    }

#if DEBUG
    /// <summary>
    ///     调试专用：车牌校验失败时仍将附带图片落盘，便于分析结构体错位或无效识别结果。
    ///     仅 DEBUG 构建生效，保存至 LprDebug/invalid-plate/。
    /// </summary>
    private void TrySaveInvalidPlateDebugImage(
        IntPtr pBuffer,
        int picLen,
        string plateRaw,
        string source,
        string deviceIp)
    {
        if (pBuffer == IntPtr.Zero || picLen <= 0)
        {
            return;
        }

        try
        {
            var imageBytes = new byte[picLen];
            Marshal.Copy(pBuffer, imageBytes, 0, picLen);

            var debugDir = PathManager.EnsureDirectoryExists("LprDebug/invalid-plate");
            var safePlate = SanitizeDebugFileNameFragment(plateRaw);
            var safeIp = string.IsNullOrWhiteSpace(deviceIp) ? "unknown-ip" : deviceIp.Replace('.', '-');
            var fileName = $"{source}_{safePlate}_{safeIp}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            var filePath = Path.Combine(debugDir, fileName);
            File.WriteAllBytes(filePath, imageBytes);

            _logger?.LogDebug(
                "无效车牌调试图片已保存: Path={Path}, PlateRaw={PlateRaw}, Source={Source}, DeviceIp={DeviceIp}",
                PathManager.ToRelativePath(filePath), plateRaw, source, deviceIp);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "保存无效车牌调试图片失败: PlateRaw={PlateRaw}, Source={Source}, DeviceIp={DeviceIp}",
                plateRaw, source, deviceIp);
        }
    }

    private static string SanitizeDebugFileNameFragment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "empty";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(Math.Min(text.Length, 32));
        foreach (var ch in text)
        {
            if (Array.IndexOf(invalidChars, ch) >= 0 || char.IsControl(ch))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }

            if (builder.Length >= 32)
            {
                break;
            }
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrEmpty(sanitized) ? "invalid" : sanitized;
    }
#endif

    /// <summary>
    ///     确保 SDK 已初始化。
    ///     Process-wide init flag is <see cref="NET_DVR._initialized"/> (shared with HikvisionService soft reset).
    /// </summary>
    private void EnsureInitialized()
    {
        lock (this)
        {
            // Soft reset in HikvisionService clears NET_DVR._initialized; re-sync local flag.
            if (_isInitialized && !NET_DVR._initialized)
            {
                _isInitialized = false;
                _logger?.LogWarning("Detected HCNetSDK soft reset; HikvisionLprService will re-initialize");
            }

            if (_isInitialized)
            {
                return;
            }

            // Capture service may already have initialized the same DLL.
            if (NET_DVR._initialized)
            {
                _isInitialized = true;
                return;
            }

            if (!HikvisionSdk.NET_DVR_Init())
            {
                var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                throw new InvalidOperationException(
                    $"SDK 初始化失败: ErrorCode={errorCode}, ErrorDesc={GetErrorDescription(errorCode)}");
            }

            NET_DVR._initialized = true;
            _isInitialized = true;
            _logger?.LogInformation("海康威视 SDK 初始化成功");

            // 注册进程退出处理
            AppDomain.CurrentDomain.ProcessExit += (_, __) => Cleanup();
        }
    }

    /// <summary>
    ///     清理 SDK 资源
    /// </summary>
    private void Cleanup()
    {
        if (!_isInitialized)
        {
            return;
        }

        lock (this)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                HikvisionSdk.NET_DVR_Cleanup();
                NET_DVR._initialized = false;
                _isInitialized = false;
                _logger?.LogInformation("海康威视 SDK 资源已清理");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理 SDK 资源时发生异常");
            }
        }
    }

    /// <summary>
    ///     尝试登录设备（已废弃直连 Login；保留空实现避免外部反射依赖）
    /// </summary>
    private bool TryLogin(LicensePlateRecognitionConfig config, out int userId)
    {
        userId = -1;
        if (!TryParsePort(config, out var port))
            return false;

        var acquire = _sessionStore.Acquire(
            config.Ip, port, config.UserName ?? string.Empty, config.Password ?? string.Empty);
        if (!acquire.Success)
            return false;

        var key = _sessionStore.BuildKey(config.Ip, port, config.UserName ?? string.Empty);
        _heldSessionKeys[key] = 0;
        userId = acquire.UserId;
        return true;
    }

    /// <summary>
    ///     将字符串转换为固定长度的字节数组
    /// </summary>
    private static byte[] ToFixedBytes(string text, int fixedLen)
    {
        var bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
        Array.Resize(ref bytes, fixedLen);
        return bytes;
    }

    /// <summary>
    ///     获取错误描述
    /// </summary>
    private static string GetErrorDescription(uint errorCode)
    {
        return errorCode switch
        {
            0 => "无错误",
            1 => "用户名或密码错误",
            2 => "权限不足",
            3 => "SDK 未初始化",
            4 => "通道号错误",
            5 => "设备连接数达到上限",
            6 => "版本不匹配",
            7 => "连接设备失败",
            8 => "发送失败",
            9 => "接收失败",
            10 => "超时",
            11 => "数据传输失败",
            12 => "端口错误",
            13 => "密码错误",
            14 => "获取 DVR 工作状态失败",
            15 => "获取 DVR 系统信息失败",
            16 => "DVR 不支持此功能",
            17 => "DVR 离线",
            18 => "用户被锁定",
            19 => "分配资源失败",
            20 => "DVR 正在操作",
            21 => "DVR 资源正在使用",
            22 => "DVR 不允许更多连接",
            23 => "DVR 命令执行失败",
            24 => "DVR 预览失败",
            25 => "DVR 参数格式错误",
            26 => "DVR 无效文件或文件错误",
            27 => "启动预览失败",
            28 => "打开文件失败",
            29 => "读取文件失败",
            30 => "写入文件失败",
            31 => "关闭文件失败",
            32 => "创建文件失败",
            33 => "删除文件失败",
            34 => "定位文件失败",
            35 => "获取文件大小失败",
            36 => "打开流失败",
            37 => "关闭流失败",
            38 => "获取流失败",
            39 => "开始录像失败",
            40 => "停止录像失败",
            41 => "开始抓拍失败",
            42 => "停止抓拍失败",
            43 => "无图像",
            44 => "抓拍超时",
            45 => "获取流超时",
            _ => $"未知错误 ({errorCode})"
        };
    }

    /// <summary>
    ///     主动触发海康威视设备的车牌识别；识别结果通过 ILocalEventBus 的 LicensePlateRecognizedEventData 交付。
    /// </summary>
    /// <param name="config">设备配置</param>
    public async Task TriggerCaptureAsync(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        await RefreshCachedWeighingModeAsync();

        if (!TryParsePort(config, out var port))
        {
            throw new InvalidOperationException($"设备端口无效: {config.Name}");
        }

        var key = _sessionStore.BuildKey(config.Ip, port, config.UserName ?? string.Empty);
        var acquire = _sessionStore.Acquire(config.Ip, port, config.UserName ?? string.Empty, config.Password ?? string.Empty);
        if (!acquire.Success)
        {
            _logger?.LogError("登录海康威视设备失败: {Device}, Message={Message}", config.Name, acquire.Message);
            throw new InvalidOperationException($"设备登录失败: {config.Name}");
        }

        _heldSessionKeys[key] = 0;
        var userId = acquire.UserId;

        var snapCfg = new HikvisionSdk.NET_DVR_SNAPCFG
        {
            dwSize = (uint)Marshal.SizeOf<HikvisionSdk.NET_DVR_SNAPCFG>(),
            byRelatedDriveWay = 0,
            bySnapTimes = 1,
            wSnapWaitTime = 1,
            wIntervalTime = [200, 0, 0, 0],
            dwSnapVehicleNum = 1,
            struJpegPara = new HikvisionSdk.NET_DVR_JPEGPARA { wPicSize = 0xff, wPicQuality = 1 },
            byRes2 = new byte[16]
        };

        var result = HikvisionSdk.NET_DVR_ContinuousShoot(userId, ref snapCfg);
        if (!result)
        {
            var errorCode = HikvisionSdk.NET_DVR_GetLastError();
            _logger?.LogWarning(
                "ContinuousShoot 失败，Invalidate 后重试一次: Device={Device}, UserId={UserId}, ErrorCode={ErrorCode}",
                config.Name, userId, errorCode);
            _sessionStore.Invalidate(key);
            _heldSessionKeys.TryRemove(key, out _);

            acquire = _sessionStore.Acquire(config.Ip, port, config.UserName ?? string.Empty, config.Password ?? string.Empty);
            if (!acquire.Success)
            {
                var error = GetErrorDescription(errorCode);
                _logger?.LogError("触发抓拍失败(重登亦失败): {Error}", error);
                throw new InvalidOperationException($"触发抓拍失败: {error}");
            }

            _heldSessionKeys[key] = 0;
            userId = acquire.UserId;
            result = HikvisionSdk.NET_DVR_ContinuousShoot(userId, ref snapCfg);
            if (!result)
            {
                errorCode = HikvisionSdk.NET_DVR_GetLastError();
                var error = GetErrorDescription(errorCode);
                _logger?.LogError("触发抓拍失败: {Error}", error);
                throw new InvalidOperationException($"触发抓拍失败: {error}");
            }
        }

        _logger?.LogInformation("已触发海康威视设备抓拍: Device={Device}, UserId={UserId}", config.Name, userId);
        ScheduleShootCallbackWarning(config.Ip);
        await Task.CompletedTask;
    }

    private async Task RefreshCachedWeighingModeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            _cachedWeighingMode = settings.SystemSettings.DefaultWeighingMode;
            _cachedHasCameraConfigs = settings.CameraConfigs.Count > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "刷新称重模式缓存失败，Lpr 图片保存将沿用缓存模式 {Mode}", _cachedWeighingMode);
        }
    }

    private async Task AcquireConfiguredLprSessionsAsync(SettingsEntity? settings = null)
    {
        settings ??= await _settingsService.GetSettingsAsync().ConfigureAwait(false);
        var configs = (settings.LicensePlateRecognitionConfigs ?? [])
            .Where(c => c.IsValid())
            .ToList();

        foreach (var config in configs)
        {
            _deviceConfigs.AddOrUpdate(config.Ip, config, (_, __) => config);
            if (!TryParsePort(config, out var port))
                continue;

            var acquire = _sessionStore.Acquire(
                config.Ip,
                port,
                config.UserName ?? string.Empty,
                config.Password ?? string.Empty);
            if (!acquire.Success)
            {
                _logger?.LogWarning(
                    "LPR 启动后 Acquire 会话失败: Device={Device}, IP={Ip}, Message={Message}",
                    config.Name, config.Ip, acquire.Message);
                continue;
            }

            var key = _sessionStore.BuildKey(config.Ip, port, config.UserName ?? string.Empty);
            _heldSessionKeys[key] = 0;
            _logger?.LogInformation(
                "LPR 长会话已 Acquire: Device={Device}, Key={Key}, UserId={UserId}",
                config.Name, key, acquire.UserId);
        }
    }

    private void ReleaseHeldSessions()
    {
        foreach (var key in _heldSessionKeys.Keys.ToArray())
        {
            _sessionStore.Release(key);
            _heldSessionKeys.TryRemove(key, out _);
        }
    }

    private void OnSessionsClearedForSdkReset()
    {
        _logger?.LogWarning(
            "SDK soft-reset: clearing LPR listen handle (was {Handle})", _listenHandle);
        try
        {
            if (_listenHandle >= 0)
            {
                HikvisionSdk.NET_DVR_StopListen_V30(_listenHandle);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "StopListen during soft-reset clear failed");
        }
        finally
        {
            if (_callbackHandle.HasValue && _callbackHandle.Value.IsAllocated)
            {
                _callbackHandle.Value.Free();
                _callbackHandle = null;
            }

            _listenHandle = -1;
            _heldSessionKeys.Clear();
        }
    }

    private void OnSdkReinitialized()
    {
        _ = RebuildListenAfterSoftResetAsync();
    }

    private async Task RebuildListenAfterSoftResetAsync()
    {
        try
        {
            _logger?.LogWarning("SDK soft-reset: rebuilding LPR StartListen and sessions");
            var ok = await StartAsync().ConfigureAwait(false);
            if (!ok)
            {
                _logger?.LogError(
                    "SDK soft-reset: LPR StartListen rebuild failed (ListenHandle={Handle}, Ip={Ip}, Port={Port})",
                    _listenHandle, _listenLocalIp, _listenLocalPort);
            }
            else
            {
                _logger?.LogInformation(
                    "SDK soft-reset: LPR Listen rebuilt ListenHandle={Handle}, Ip={Ip}, Port={Port}",
                    _listenHandle, _listenLocalIp, _listenLocalPort);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SDK soft-reset: LPR rebuild threw");
        }
    }

    private void ScheduleShootCallbackWarning(string deviceIp)
    {
        var dueAt = DateTimeOffset.UtcNow.Add(ShootCallbackWarningTimeout);
        _pendingShootByDeviceIp[deviceIp] = dueAt;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ShootCallbackWarningTimeout).ConfigureAwait(false);
                if (_pendingShootByDeviceIp.TryGetValue(deviceIp, out var pending) &&
                    pending == dueAt)
                {
                    _pendingShootByDeviceIp.TryRemove(deviceIp, out _);
                    _logger?.LogWarning(
                        "ContinuousShoot accepted but no plate/ITS callback within {TimeoutSec}s: DeviceIp={DeviceIp}",
                        ShootCallbackWarningTimeout.TotalSeconds, deviceIp);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Shoot callback warning timer failed: DeviceIp={DeviceIp}", deviceIp);
            }
        });
    }

    private void MarkShootCallbackReceived(string? deviceIp)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
            return;
        _pendingShootByDeviceIp.TryRemove(deviceIp, out _);
    }

    private bool TryParsePort(LicensePlateRecognitionConfig config, out int port)
    {
        if (!int.TryParse(config.Port, out port) || port <= 0)
        {
            _logger?.LogWarning("设备端口无效: IP={Ip}, Port={Port}", config.Ip, config.Port);
            port = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     映射车身颜色枚举值为可读字符串
    /// </summary>
    /// <param name="byColor">车身颜色枚举值</param>
    /// <returns>可读字符串，未知值返回 null</returns>
    private static string? MapVehicleColor(int byColor)
    {
        if (!Enum.IsDefined(typeof(HikvisionVehicleColorType), byColor))
            return null;

        var vehicleColorType = (HikvisionVehicleColorType)byColor;
        return vehicleColorType.GetDescription();
    }

    /// <summary>
    ///     映射车型枚举值为可读字符串
    /// </summary>
    /// <param name="byVehicleType">车型枚举值</param>
    /// <returns>可读字符串，未知值返回 null</returns>
    private static string? MapVehicleType(int byVehicleType)
    {
        if (!Enum.IsDefined(typeof(HikvisionVehicleType), byVehicleType))
            return null;

        var vehicleType = (HikvisionVehicleType)byVehicleType;
        return vehicleType.GetDescription();
    }

    /// <summary>
    ///     映射车牌颜色枚举值为可读字符串
    /// </summary>
    /// <param name="plateInfoEx">车牌扩展信息</param>
    /// <returns>可读字符串，未知值返回 null</returns>
    private static string? MapPlateColor(HikvisionSdk.NET_DVR_PLATE_INFO plateInfo)
    {
        var colorValue = (int)plateInfo.byColor;
        if (!Enum.IsDefined(typeof(HikvisionPlateColorType), colorValue))
            return null;

        var plateColorType = (HikvisionPlateColorType)colorValue;
        return plateColorType.GetDescription();
    }
}
