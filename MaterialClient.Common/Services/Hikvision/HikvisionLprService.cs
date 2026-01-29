using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别服务实现
///     通过 HCNetSDK 与海康设备通信，接收车牌识别结果
///     支持被动捕获（设备推送）和主动捕获（应用触发）
/// </summary>
public sealed class HikvisionLprService : IHikvisionLprService, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, LicensePlateRecognitionConfig> _deviceConfigs = new();
    private readonly ILogger<HikvisionLprService>? _logger;
    private GCHandle? _callbackHandle;
    private bool _isInitialized;
    private int _listenHandle = -1;
    private readonly Subject<LicensePlateRecognizedEvent> _plateRecognizedSubject = new();

    public HikvisionLprService(ILogger<HikvisionLprService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     车牌识别事件流
    /// </summary>
    public IObservable<LicensePlateRecognizedEvent> PlateRecognized => _plateRecognizedSubject.AsObservable();

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
    ///     检查设备是否在线
    /// </summary>
    public bool IsOnline(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        EnsureInitialized();

        var success = TryLogin(config, out var userId);

        if (success)
        {
            // 登录成功后登出，释放资源
            HikvisionSdk.NET_DVR_Logout(userId);
            _logger?.LogDebug("设备在线检查成功: IP={Ip}", config.Ip);
        }
        else
        {
            _logger?.LogWarning("设备离线: IP={Ip}", config.Ip);
        }

        return success;
    }

    /// <summary>
    ///     启动监听服务
    /// </summary>
    public async Task<bool> StartAsync(string listenLocalIp, int listenLocalPort)
    {
        // 验证参数
        if (string.IsNullOrWhiteSpace(listenLocalIp))
        {
            throw new ArgumentException("监听 IP 地址不能为空", nameof(listenLocalIp));
        }

        if (listenLocalPort <= 0 || listenLocalPort > 65535)
        {
            throw new ArgumentException("监听端口必须在 1-65535 范围内", nameof(listenLocalPort));
        }

        await Task.CompletedTask; // 保持方法签名为异步

        // 检查是否已经启动
        if (_listenHandle >= 0)
        {
            _logger?.LogWarning("监听服务已经启动，无需重复启动");
            return false;
        }

        try
        {
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

            _logger?.LogInformation("监听服务启动成功: IP={Ip}, Port={Port}, ListenHandle={Handle}",
                listenLocalIp, listenLocalPort, _listenHandle);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "启动监听服务时发生异常: IP={Ip}, Port={Port}",
                listenLocalIp, listenLocalPort);

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
    ///     停止监听服务
    /// </summary>
    public async Task StopAsync()
    {
        await Task.CompletedTask; // 保持方法签名为异步

        if (_listenHandle < 0)
        {
            _logger?.LogWarning("监听服务未启动，无需停止");
            return;
        }

        try
        {
            // 停止监听
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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "停止监听服务时发生异常: ListenHandle={Handle}", _listenHandle);
        }
        finally
        {
            // CRITICAL: 释放 GCHandle，允许委托被垃圾回收
            // 必须在 SDK 停止调用回调之后才能释放
            if (_callbackHandle.HasValue)
            {
                _callbackHandle.Value.Free();
                _callbackHandle = null;
            }

            _listenHandle = -1;
        }
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
            // 解析报警器信息（包含设备 IP）
            var alarmer = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_ALARMER>(pAlarmer);
            var deviceIp = Encoding.ASCII.GetString(alarmer.sDeviceIP).TrimEnd('\0');

            // 查找设备配置
            _deviceConfigs.TryGetValue(deviceIp, out var config);

            // 解析车牌识别结果
            var plateResult = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_PLATE_RESULT>(pAlarmInfo);

            // 使用 GBK 编码提取车牌号
            var plateNumber = HikvisionEncodingHelper.GetStringFromPtr(plateResult.struPlateInfo.sLicense, 48, _logger);

            // 创建事件
            var @event = new LicensePlateRecognizedEvent
            {
                PlateNumber = plateNumber,
                DeviceName = config?.Name ?? $"Unknown ({deviceIp})",
                Direction = config?.Direction ?? MaterialClient.Common.Entities.Enums.LicensePlateDirection.In,
                Timestamp = DateTime.Now
            };

            // 发布事件
            _plateRecognizedSubject.OnNext(@event);

            _logger?.LogInformation(
                "收到车牌识别结果: Device={Device}, Plate={Plate}, Direction={Direction}, Time={Time}",
                @event.DeviceName, @event.PlateNumber, @event.Direction, @event.Timestamp);
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
            // 解析报警器信息（包含设备 IP）
            var alarmer = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_ALARMER>(pAlarmer);
            var deviceIp = Encoding.ASCII.GetString(alarmer.sDeviceIP).TrimEnd('\0');

            // 查找设备配置
            _deviceConfigs.TryGetValue(deviceIp, out var config);

            // 解析 ITS 车牌识别结果
            var itsResult = Marshal.PtrToStructure<HikvisionSdk.NET_ITS_PLATE_RESULT>(pAlarmInfo);

            // 遍历所有车牌识别结果
            for (var i = 0; i < itsResult.dwResultNum && i < itsResult.struPlateInfo.Length; i++)
            {
                var plateInfo = itsResult.struPlateInfo[i];

                // 使用 GBK 编码提取车牌号
                var plateNumber = HikvisionEncodingHelper.GetStringFromPtr(plateInfo.sLicense, 48, _logger);

                // 创建事件
                var @event = new LicensePlateRecognizedEvent
                {
                    PlateNumber = plateNumber,
                    DeviceName = config?.Name ?? $"Unknown ({deviceIp})",
                    Direction = config?.Direction ?? MaterialClient.Common.Entities.Enums.LicensePlateDirection.In,
                    Timestamp = DateTime.Now
                };

                // 发布事件
                _plateRecognizedSubject.OnNext(@event);

                _logger?.LogInformation(
                    "收到 ITS 车牌识别结果: Device={Device}, Plate={Plate}, Direction={Direction}, Time={Time}",
                    @event.DeviceName, @event.PlateNumber, @event.Direction, @event.Timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理 ITS 车牌识别结果失败");
        }
    }

    /// <summary>
    ///     确保 SDK 已初始化
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (this)
        {
            if (_isInitialized)
            {
                return;
            }

            if (!HikvisionSdk.NET_DVR_Init())
            {
                var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                throw new InvalidOperationException(
                    $"SDK 初始化失败: ErrorCode={errorCode}, ErrorDesc={GetErrorDescription(errorCode)}");
            }

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
    ///     尝试登录设备
    /// </summary>
    private bool TryLogin(LicensePlateRecognitionConfig config, out int userId)
    {
        userId = -1;

        try
        {
            // 验证配置
            if (!int.TryParse(config.Port, out var port) || port <= 0)
            {
                _logger?.LogWarning("设备端口无效: IP={Ip}, Port={Port}", config.Ip, config.Port);
                return false;
            }

            // 构建设备登录信息
            var loginInfo = new HikvisionSdk.NET_DVR_USER_LOGIN_INFO
            {
                sDeviceAddress = ToFixedBytes(config.Ip, 129),
                sUserName = ToFixedBytes(config.UserName ?? string.Empty, 64),
                sPassword = ToFixedBytes(config.Password ?? string.Empty, 64),
                wPort = (ushort)port,
                bUseAsynLogin = 0
            };

            var devInfo = new HikvisionSdk.NET_DVR_DEVICEINFO_V40();

            // 调用登录 API
            userId = HikvisionSdk.NET_DVR_Login_V40(ref loginInfo, ref devInfo);

            if (userId < 0)
            {
                var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                _logger?.LogWarning(
                    "设备登录失败: IP={Ip}, Port={Port}, Username={Username}, ErrorCode={ErrorCode}, ErrorDesc={ErrorDesc}",
                    config.Ip, config.Port, config.UserName, errorCode, GetErrorDescription(errorCode));
                return false;
            }

            _logger?.LogDebug("设备登录成功: IP={Ip}, UserId={UserId}", config.Ip, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "设备登录异常: IP={Ip}", config.Ip);
            return false;
        }
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
}
