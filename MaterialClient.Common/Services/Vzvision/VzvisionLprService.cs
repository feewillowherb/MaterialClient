using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

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
}

/// <inheritdoc cref="IVzvisionLprService" />
public sealed class VzvisionLprService : IVzvisionLprService, ISingletonDependency
{
    private readonly ILogger<VzvisionLprService>? _logger;

    private readonly ConcurrentDictionary<string, LicensePlateRecognitionConfig> _configs = new(
        StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _ipToHandle = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, string> _handleToIp = new();

    private readonly object _sync = new();

    /// <summary>防止回调被 GC 回收</summary>
    private VzvisionSdk.VZLPRC_PLATE_INFO_CALLBACK? _plateCallback;

    private bool _sdkSetupDone;
    private bool _started;

    public VzvisionLprService(ILogger<VzvisionLprService>? logger = null)
    {
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
        await Task.CompletedTask;
        lock (_sync)
        {
            foreach (var ip in _ipToHandle.Keys.ToArray())
            {
                if (_ipToHandle.TryRemove(ip, out var handle) && handle != 0)
                {
                    _handleToIp.TryRemove(handle, out _);
                    _ = VzvisionSdk.VzLPRClient_Close(handle);
                    _logger?.LogInformation("Vzvision 设备已关闭: IP={Ip}, Handle={Handle}", ip, handle);
                }
            }

            if (_sdkSetupDone)
            {
                VzvisionSdk.VzLPRClient_Cleanup();
                _sdkSetupDone = false;
            }

            _started = false;
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
        _ = pImgFull;
        _ = pImgPlateClip;
        _ = eResultType;

        if (uNumPlates == 0 || pResult == IntPtr.Zero)
            return 0;

        try
        {
            var plate = Marshal.PtrToStructure<VzvisionSdk.TH_PlateResult>(pResult);
            var license = DecodeLicense(plate.license);
            if (string.IsNullOrWhiteSpace(license))
                return 0;

            if (!_handleToIp.TryGetValue(handle, out var ip) || !_configs.TryGetValue(ip, out var cfg))
            {
                _logger?.LogWarning("车牌回调未匹配配置: Handle={Handle}", handle);
                return 0;
            }

            var color = MapColor(plate.nColor);
            var deviceName = cfg.Name;

            Observable.Return(Unit.Default)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
                    {
                        PlateNumber = license,
                        ColorType = color,
                        DeviceType = LprDeviceType.Vzvision,
                        DeviceName = deviceName,
                        Timestamp = DateTime.Now
                    });
                });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理 Vzvision 车牌回调异常");
        }

        return 0;
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

    private static string DecodeLicense(byte[]? license)
    {
        if (license == null || license.Length == 0)
            return string.Empty;

        var n = Array.IndexOf(license, (byte)0);
        if (n < 0)
            n = license.Length;

        try
        {
            return Encoding.Default.GetString(license, 0, n);
        }
        catch
        {
            return Encoding.UTF8.GetString(license, 0, n);
        }
    }
}
