using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services.Vzvision;

/// <summary>
///     臻识 Vz 车牌识别：SDK 连接、车牌回调、主动抓拍（<see cref="VzvisionSdk.VzLPRClient_ForceTrigger"/>）。
///     不调用 <see cref="VzvisionSdk.VzLPRClient_StartRealPlay"/>（无实时预览）。
/// </summary>
public interface IVzvisionLprService : ILprDevice
{
    void AddOrUpdateDevice(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     初始化 SDK 并打开已登记设备、注册车牌回调。
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>
    ///     关闭所有连接并 <see cref="VzvisionSdk.VzLPRClient_Cleanup"/>。
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     基于当前连接句柄的 <see cref="VzvisionSdk.VzLPRClient_IsConnected"/>（无 HTTP 轮询）。
    /// </summary>
    bool IsOnline(string deviceIp, TimeSpan? timeout = null);

    /// <summary>
    ///     发送道闸 I/O 自动复位输出（带响应确认）。
    /// </summary>
    Task SetIoOutputAutoRespAsync(LicensePlateRecognitionConfig config, uint ioChannel, int durationMs = 500);
}

/// <inheritdoc cref="IVzvisionLprService" />
public class VzvisionLprService : IVzvisionLprService, ISingletonDependency, IAsyncDisposable
{
    /// <summary>SDK 车牌字节为 GB2312（与 Vz 设备侧常见编码一致）</summary>
    private static readonly Lazy<Encoding> Gb2312Encoding = new(() =>
    {
        CodePagesEncodingInitializer.Register();
        return Encoding.GetEncoding("GB2312");
    });

    private readonly ILogger<VzvisionLprService>? _logger;
    private readonly ILocalEventBus _localEventBus;
    private readonly ISettingsService _settingsService;

    private readonly ConcurrentDictionary<string, LicensePlateRecognitionConfig> _configs = new(
        StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _ipToHandle = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, string> _handleToIp = new();

    private readonly object _sync = new();

    /// <summary>防止回调被 GC 回收</summary>
    private VzvisionSdk.VZLPRC_PLATE_INFO_CALLBACK? _plateCallback;

    private bool _sdkSetupDone;
    private bool _started;
    private WeighingMode _cachedWeighingMode = WeighingMode.Standard;
    private bool _cachedHasCameraConfigs = true;

    public VzvisionLprService(ISettingsService settingsService, ILocalEventBus localEventBus, ILogger<VzvisionLprService>? logger = null)
    {
        _settingsService = settingsService;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsActiveCapture => true;

    /// <inheritdoc />
    public void AddOrUpdateDevice(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.IsValid())
            throw new ArgumentException("设备配置无效", nameof(config));

        _configs[config.Ip] = config;
        _logger?.LogInformation("Vzvision 设备配置已添加/更新: IP={Ip}, Name={Name}", config.Ip, config.Name);
    }

    /// <inheritdoc />
    public async Task<bool> StartAsync()
    {
        await Task.CompletedTask;

        // 缓存当前称重模式（用于 Lpr 附件保存判断）
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            _cachedWeighingMode = settings.SystemSettings.DefaultWeighingMode;
            _cachedHasCameraConfigs = settings.CameraConfigs.Count > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "读取称重模式设置失败，使用默认值");
        }

        lock (_sync)
        {
            if (_started)
            {
                _logger?.LogWarning("Vzvision 服务已启动，跳过重复启动");
                return false;
            }

            if (!EnsureSetupLocked())
                return false;

            foreach (var cfg in _configs.Values)
            {
                if (!TryOpenDeviceLocked(cfg, out _))
                    _logger?.LogWarning("Vzvision 启动时未能打开设备: {Ip}", cfg.Ip);
            }

            _started = true;
            return true;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        _started = false;

        var handlesToClose = new List<(string Ip, int Handle)>();
        bool cleanupNeeded;

        lock (_sync)
        {
            foreach (var ip in _ipToHandle.Keys.ToArray())
            {
                if (_ipToHandle.TryRemove(ip, out var handle) && handle != 0)
                {
                    _handleToIp.TryRemove(handle, out _);
                    handlesToClose.Add((ip, handle));
                }
            }

            cleanupNeeded = _sdkSetupDone;
            _sdkSetupDone = false;
        }

        foreach (var (ip, handle) in handlesToClose)
        {
            await CloseHandleWithTimeoutAsync(ip, handle);
        }

        if (cleanupNeeded)
        {
            await CleanupWithTimeoutAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private static readonly TimeSpan SdkTimeout = TimeSpan.FromSeconds(3);

    private async Task CloseHandleWithTimeoutAsync(string ip, int handle)
    {
        var task = Task.Run(() => VzvisionSdk.VzLPRClient_Close(handle));
        if (await Task.WhenAny(task, Task.Delay(SdkTimeout)) != task)
        {
            _logger?.LogWarning("VzLPRClient_Close 超时: IP={Ip}, Handle={Handle}", ip, handle);
            return;
        }

        _logger?.LogInformation("Vzvision 设备已关闭: IP={Ip}, Handle={Handle}", ip, handle);
    }

    private async Task CleanupWithTimeoutAsync()
    {
        var task = Task.Run(() => VzvisionSdk.VzLPRClient_Cleanup());
        if (await Task.WhenAny(task, Task.Delay(SdkTimeout)) != task)
        {
            _logger?.LogWarning("VzLPRClient_Cleanup 超时");
        }
    }

    /// <inheritdoc />
    public bool IsOnline(string deviceIp, TimeSpan? timeout = null)
    {
        _ = timeout;
        if (string.IsNullOrWhiteSpace(deviceIp))
            return false;

        if (!_ipToHandle.TryGetValue(deviceIp, out var handle) || handle == 0)
            return false;

        var ret = VzvisionSdk.VzLPRClient_IsConnected(handle, out var status);
        return ret == 0 && status == 1;
    }

    /// <inheritdoc />
    public async Task TriggerCaptureAsync(LicensePlateRecognitionConfig config)
    {
        await Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(config);
        if (!config.IsValid())
            throw new InvalidOperationException("车牌识别配置无效");

        if (!TryEnsureHandle(config, out var handle))
            throw new InvalidOperationException($"设备未连接或打开失败: {config.Name} ({config.Ip})");

        var ret = VzvisionSdk.VzLPRClient_ForceTrigger(handle);
        if (ret != 0)
        {
            _logger?.LogWarning("VzLPRClient_ForceTrigger 返回非零: {Ret}, Device={Name}", ret, config.Name);
            throw new InvalidOperationException($"抓拍触发失败 (代码 {ret})");
        }

        _logger?.LogInformation("已触发 Vzvision 抓拍: Device={Name}", config.Name);
    }

    /// <inheritdoc />
    public async Task SetIoOutputAutoRespAsync(LicensePlateRecognitionConfig config, uint ioChannel, int durationMs = 500)
    {
        await Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(config);
        if (!config.IsValid())
            throw new InvalidOperationException("车牌识别配置无效");

        if (!TryEnsureHandle(config, out var handle))
            throw new InvalidOperationException($"设备未连接或打开失败: {config.Name} ({config.Ip})");

        // SDK 文档约束自动复位时长范围 [500, 5000]，当前需求固定 500ms。
        if (durationMs < 500)
            durationMs = 500;
        else if (durationMs > 5000)
            durationMs = 5000;

        var ret = VzvisionSdk.VzLPRClient_SetIOOutputAutoResp(handle, ioChannel, durationMs);
        if (ret != 0)
        {
            _logger?.LogWarning("VzLPRClient_SetIOOutputAutoResp 返回非零: {Ret}, Device={Name}, IoChannel={IoChannel}, DurationMs={DurationMs}",
                ret, config.Name, ioChannel, durationMs);
            throw new InvalidOperationException($"I/O 自动复位输出失败 (代码 {ret})");
        }

        _logger?.LogInformation("已发送 Vzvision I/O 自动复位输出: Device={Name}, IoChannel={IoChannel}, DurationMs={DurationMs}",
            config.Name, ioChannel, durationMs);
    }

    private bool TryEnsureHandle(LicensePlateRecognitionConfig config, out int handle)
    {
        handle = 0;
        lock (_sync)
        {
            if (!_configs.ContainsKey(config.Ip))
                _configs[config.Ip] = config;

            if (_ipToHandle.TryGetValue(config.Ip, out handle) && handle != 0)
                return true;

            if (!EnsureSetupLocked())
                return false;

            return TryOpenDeviceLocked(config, out handle);
        }
    }

    private bool EnsureSetupLocked()
    {
        if (_sdkSetupDone)
            return true;

        var ret = VzvisionSdk.VzLPRClient_Setup();
        if (ret != 0)
        {
            _logger?.LogError("VzLPRClient_Setup 失败: {Ret}", ret);
            return false;
        }

        _sdkSetupDone = true;
        _plateCallback ??= OnPlateInfo;
        return true;
    }

    private bool TryOpenDeviceLocked(LicensePlateRecognitionConfig config, out int handle)
    {
        handle = 0;
        if (_ipToHandle.TryGetValue(config.Ip, out var existing) && existing != 0)
        {
            handle = existing;
            return true;
        }

        VzvisionLprDefaults.ApplyDefaults(config);
        if (!ushort.TryParse(config.Port, out var port))
            port = 80;

        var user = config.UserName ?? VzvisionLprDefaults.DefaultUserName;
        var pwd = config.Password ?? string.Empty;

        var h = VzvisionSdk.VzLPRClient_Open(config.Ip, port, user, pwd);
        if (h == 0)
        {
            _logger?.LogError("VzLPRClient_Open 失败: IP={Ip}", config.Ip);
            return false;
        }

        var cb = _plateCallback ??= OnPlateInfo;
        var setRet = VzvisionSdk.VzLPRClient_SetPlateInfoCallBack(h, cb, IntPtr.Zero, 0);
        if (setRet != 0)
        {
            _logger?.LogError("VzLPRClient_SetPlateInfoCallBack 失败: {Ret}, IP={Ip}", setRet, config.Ip);
            _ = VzvisionSdk.VzLPRClient_Close(h);
            return false;
        }

        _ipToHandle[config.Ip] = h;
        _handleToIp[h] = config.Ip;
        handle = h;
        _logger?.LogInformation("Vzvision 设备已连接: Name={Name}, IP={Ip}, Handle={Handle}", config.Name, config.Ip,
            h);
        return true;
    }

    private int OnPlateInfo(
        int handle,
        IntPtr pUserData,
        IntPtr pResult,
        uint uNumPlates,
        VzvisionSdk.VZ_LPRC_RESULT_TYPE eResultType,
        IntPtr pImgFull,
        IntPtr pImgPlateClip)
    {
        _ = pUserData;
        _ = eResultType;

        if (uNumPlates == 0 || pResult == IntPtr.Zero)
            return 0;

        try
        {
            var plate = Marshal.PtrToStructure<VzvisionSdk.TH_PlateResult>(pResult);
            var license = DecodeLicense(plate.license);
            if (string.IsNullOrWhiteSpace(license))
                return 0;

            if (!PlateNumberValidator.IsValidChinesePlateNumber(license))
            {
                _logger?.LogWarning("Vzvision 车牌过滤：无效车牌号 {Plate}，Handle={Handle}，ResultType={ResultType}",
                    license, handle, eResultType);
                return 0;
            }

            if (!_handleToIp.TryGetValue(handle, out var ip) || !_configs.TryGetValue(ip, out var cfg))
            {
                _logger?.LogWarning("车牌回调未匹配配置: Handle={Handle}", handle);
                return 0;
            }

            var color = MapColor(plate.nColor);
            var vehicleColor = MapVehicleColor(plate.nCarColor);
            var vehicleType = MapVehicleType(plate.nType);
            var deviceName = cfg.Name;

            // 提取 Lpr 图片（仅 UrbanMode），从 pImgFull 提取全场景图
            var lrpPath = TrySaveVzLprAttachment(pImgFull, license);

            _ = _localEventBus.PublishAsync(new LicensePlateRecognizedEventData
            {
                PlateNumber = license,
                ColorType = color,
                VehicleColor = vehicleColor,
                VehicleType = vehicleType,
                PlateColor = color.GetDescription(),
                DeviceType = LprDeviceType.Vzvision,
                DeviceName = deviceName,
                Timestamp = DateTime.Now,
                LprImagePath = lrpPath
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理 Vzvision 车牌回调异常");
        }

        return 0;
    }

    /// <summary>
    ///     尝试保存 Vzvision Lpr 车牌识别图片
    ///     从 Vzvision SDK 回调的 pImgFull 提取图片数据，压缩后保存到磁盘
    /// </summary>
    /// <param name="pImgFull">Vzvision SDK 回调中的全场景图信息指针</param>
    /// <param name="plateNumber">车牌号（用于文件名）</param>
    /// <returns>保存的相对路径，保存失败时返回 null</returns>
    private string? TrySaveVzLprAttachment(IntPtr pImgFull, string plateNumber)
    {
        if (pImgFull == IntPtr.Zero)
            return null;

        try
        {
            // 从 VZ_LPRC_IMAGE_INFO 结构提取图片数据
            var imgInfo = Marshal.PtrToStructure<VzvisionSdk.VZ_LPRC_IMAGE_INFO>(pImgFull);
            if (imgInfo.pBuffer == IntPtr.Zero)
                return null;

            // 计算图片数据大小（宽 × 高 × 字节深度，简单估算）
            // Vzvision SDK 通常返回 JPEG 格式的图片数据
            var bufferSize = (int)(imgInfo.uWidth * imgInfo.uHeight * 3); // 最大可能大小
            if (bufferSize <= 0 || bufferSize > 10 * 1024 * 1024) // 安全限制：最大 10MB
                return null;

            var imageBytes = new byte[bufferSize];
            Marshal.Copy(imgInfo.pBuffer, imageBytes, 0, bufferSize);

            // 尝试找到 JPEG 结束标记 (FFD9) 以确定实际数据长度
            var actualLength = FindJpegEndMarker(imageBytes);
            if (actualLength > 0)
            {
                Array.Resize(ref imageBytes, actualLength);
            }

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
            _logger?.LogInformation("已保存 Vzvision Lpr 附件: {Path} ({Size} bytes)",
                relativePath, finalBytes.Length);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "保存 Vzvision Lpr 附件失败: Plate={Plate}", plateNumber);
            return null;
        }
    }

    /// <summary>
    ///     查找 JPEG 结束标记 (FFD9) 以确定实际数据长度
    /// </summary>
    private static int FindJpegEndMarker(byte[] data)
    {
        for (var i = data.Length - 1; i >= 1; i--)
        {
            if (data[i - 1] == 0xFF && data[i] == 0xD9)
                return i + 1;
        }
        return -1;
    }

    private static VzvisionColorType MapColor(int nColor)
    {
        return nColor switch
        {
            0 => VzvisionColorType.Unknown,
            1 => VzvisionColorType.Blue,
            2 => VzvisionColorType.Yellow,
            3 => VzvisionColorType.White,
            4 => VzvisionColorType.Black,
            5 => VzvisionColorType.Green,
            _ => VzvisionColorType.Unknown
        };
    }

    /// <summary>
    ///     映射车身颜色枚举值为可读字符串
    /// </summary>
    /// <param name="nCarColor">车身颜色枚举值</param>
    /// <returns>可读字符串，未知值返回 null</returns>
    private static string? MapVehicleColor(byte nCarColor)
    {
        if (!Enum.IsDefined(typeof(VzvisionVehicleColorType), nCarColor))
            return null;

        var vehicleColorType = (VzvisionVehicleColorType)nCarColor;
        return vehicleColorType.GetDescription();
    }

    /// <summary>
    ///     映射车型枚举值为可读字符串
    /// </summary>
    /// <param name="nType">车型枚举值</param>
    /// <returns>可读字符串，未知值返回 null</returns>
    private static string? MapVehicleType(int nType)
    {
        if (!Enum.IsDefined(typeof(VzvisionVehicleType), nType))
            return null;

        var vehicleType = (VzvisionVehicleType)nType;
        return vehicleType.GetDescription();
    }

    private static string DecodeLicense(byte[]? license)
    {
        if (license == null || license.Length == 0)
            return string.Empty;

        var n = Array.IndexOf(license, (byte)0);
        if (n < 0)
            n = license.Length;

        try
        {
            return Gb2312Encoding.Value.GetString(license, 0, n);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }
    }
}
