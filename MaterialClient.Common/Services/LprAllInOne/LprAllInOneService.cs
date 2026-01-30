using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.LprAllInOne;

/// <summary>
///     LPRAllInOne 设备服务接口
///     用于触发车牌识别一体机的手动识别功能
///     基于 comet 轮询机制：设备会轮询服务器端点，服务器在响应中返回触发消息
/// </summary>
public interface ILprAllInOneService
{
    /// <summary>
    ///     触发手动识别
    ///     设置标志，等待设备轮询时返回触发消息
    /// </summary>
    /// <param name="config">车牌识别配置</param>
    /// <returns>如果成功设置标志返回 true，否则返回 false</returns>
    Task<bool> TriggerManualRecognitionAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     检查设备是否需要触发车牌识别，并清除标志
    /// </summary>
    /// <param name="deviceIp">设备IP地址</param>
    /// <returns>如果需要触发返回 true，否则返回 false</returns>
    bool CheckAndClearTriggerFlag(string deviceIp);
}
/// <summary>
///     LPRAllInOne 设备服务实现
///     基于 comet 轮询机制：设备会轮询 GET /api/CarLicense/CallDeviceStatus
///     当需要触发车牌识别时，在响应中返回 {"Response_AlarmInfoPlate": {"manualTrigger": "ok"}}
/// </summary>
public class LprAllInOneService : ILprAllInOneService, ILprDevice, ISingletonDependency
{
    private readonly ILogger<LprAllInOneService>? _logger;
    
    // 存储每个设备IP的触发标志（设备IP -> 是否需要触发）
    private readonly ConcurrentDictionary<string, bool> _triggerFlags = new();

    public LprAllInOneService(ILogger<LprAllInOneService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     触发手动识别
    ///     设置标志，等待设备轮询时返回触发消息
    ///     根据 cap.md 文档，设备轮询时会收到以下 JSON 格式的响应：
    ///     {
    ///       "Response_AlarmInfoPlate": {
    ///         "manualTrigger": "ok"
    ///       }
    ///     }
    /// </summary>
    public Task<bool> TriggerManualRecognitionAsync(LicensePlateRecognitionConfig config)
    {
        if (config == null)
        {
            _logger?.LogWarning("LicensePlateRecognitionConfig is null");
            return Task.FromResult(false);
        }

        if (!config.IsValid())
        {
            _logger?.LogWarning(
                "Invalid LicensePlateRecognitionConfig: Name={Name}, Ip={Ip}",
                config.Name, config.Ip);
            return Task.FromResult(false);
        }

        try
        {
            // 设置触发标志，等待设备轮询时返回触发消息
            _triggerFlags.AddOrUpdate(config.Ip, true, (_, _) => true);
            
            _logger?.LogInformation(
                "Set trigger flag for LPRAllInOne device: {Name} ({Ip}). Device will receive trigger message on next poll.",
                config.Name, config.Ip);
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Unexpected error while setting trigger flag for device: {Name} ({Ip})",
                config.Name, config.Ip);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    ///     检查设备是否需要触发车牌识别，并清除标志
    ///     如果返回 true，表示需要触发，标志会被清除
    /// </summary>
    public bool CheckAndClearTriggerFlag(string deviceIp)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
        {
            return false;
        }

        // 检查并清除标志（原子操作）
        if (_triggerFlags.TryRemove(deviceIp, out var shouldTrigger) && shouldTrigger)
        {
            _logger?.LogInformation(
                "Trigger flag found and cleared for device IP: {Ip}",
                deviceIp);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     LprAllInOne 设备支持主动抓拍
    /// </summary>
    public bool SupportsActiveCapture => true;

    /// <summary>
    ///     主动触发车牌识别抓拍
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>
    ///     可观察的车牌识别事件流。
    ///     由于 LprAllInOne 使用轮询机制,此方法返回一个异步流,等待设备回调。
    /// </returns>
    public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config)
    {
        return Observable.FromAsync(async () =>
        {
            var success = await TriggerManualRecognitionAsync(config);
            if (!success)
            {
                throw new InvalidOperationException($"触发识别失败: {config.Name}");
            }

            _logger?.LogInformation("已触发 LprAllInOne 设备抓拍: Device={Device}", config.Name);

            // 注意: LprAllInOne 使用轮询机制,实际结果会通过 HTTP 回调返回
            // 并在 MinimalWebHostService 中发布到 MessageBus
            // 这里我们返回一个占位事件,实际应用可能需要等待回调
            return new LicensePlateRecognizedEvent
            {
                PlateNumber = string.Empty, // 占位,实际结果通过回调获取
                DeviceName = config.Name,
                Direction = config.Direction,
                Timestamp = DateTime.Now
            };
        });
    }
}

