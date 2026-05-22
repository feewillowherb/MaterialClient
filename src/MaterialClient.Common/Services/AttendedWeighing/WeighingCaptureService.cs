using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Vzvision;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     称重抓拍服务接口
/// </summary>
public interface IWeighingCaptureService
{
    /// <summary>
    ///     抓拍所有配置的相机
    /// </summary>
    Task<List<string>> CaptureAllCamerasAsync(string reason);

    /// <summary>
    ///     触发 Vzvision LPR 抓拍（进入 WaitingForStability 状态时）
    /// </summary>
    Task CaptureOnWaitingForStability();

    /// <summary>
    ///     触发 Vzvision LPR 抓拍（进入 WeightStabilized 状态时）
    /// </summary>
    Task CaptureOnWeightStabilized();

    /// <summary>
    ///     触发 Vzvision LPR 抓拍（进入 OffScale 状态时）
    /// </summary>
    Task CaptureOnOffScale();
}

/// <summary>
///     称重抓拍服务
///     负责相机抓拍编排（Hikvision JPEG 批量抓拍 + Vzvision LPR 触发）
/// </summary>
public class WeighingCaptureService : IWeighingCaptureService, ISingletonDependency
{
    private readonly IHikvisionService _hikvisionService;
    private readonly IVzvisionLprService? _vzvisionLprService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<WeighingCaptureService> _logger;

    public WeighingCaptureService(
        IHikvisionService hikvisionService,
        IVzvisionLprService? vzvisionLprService,
        ISettingsService settingsService,
        ILogger<WeighingCaptureService> logger)
    {
        _hikvisionService = hikvisionService;
        _vzvisionLprService = vzvisionLprService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<string>> CaptureAllCamerasAsync(string reason)
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs.Count == 0)
            {
                _logger.LogWarning("No cameras configured, cannot capture ({Reason})", reason);
                return new List<string>();
            }

            var requests = new List<BatchCaptureRequest>();
            var now = DateTime.Now;
            var basePath = AttachmentPathUtils.GetLocalStorageAbsolutePath(AttachType.EntryPhoto, now);

            foreach (var cameraConfig in cameraConfigs)
            {
                var request = BatchCaptureRequest.FromCameraConfig(cameraConfig, basePath, _logger);
                if (request != null)
                {
                    requests.Add(request);
                }
            }

            if (requests.Count == 0)
            {
                _logger.LogWarning("No valid camera configurations, cannot capture ({Reason})", reason);
                return new List<string>();
            }

            _logger.LogInformation("Starting capture for {Count} cameras ({Reason})", requests.Count, reason);

            var results = await _hikvisionService.CaptureJpegFromStreamBatchAsync(requests);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            _logger.LogInformation("Capture completed, success: {SuccessCount}, failed: {FailCount} ({Reason})",
                successCount, failCount, reason);

            foreach (var result in results.Where(r => !r.Success))
                _logger.LogWarning("Capture failed - Device: {DeviceKey}, Channel: {Channel}, Error: {Error}",
                    result.Request.DeviceKey, result.Request.Channel, result.ErrorMessage);

            var photoPaths = results.Where(r => r.Success && File.Exists(r.Request.SaveFullPath))
                .Select(r => r.Request.SaveFullPath)
                .ToList();

            if (photoPaths.Count == 0)
                _logger.LogWarning("Capture completed, but no photos were successfully obtained ({Reason})", reason);

            return photoPaths;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while capturing all cameras ({Reason})", reason);
            return new List<string>();
        }
    }

    /// <inheritdoc />
    public Task CaptureOnWaitingForStability() => TriggerLprCaptureForAllAsync("WaitingForStability");

    /// <inheritdoc />
    public Task CaptureOnWeightStabilized() => TriggerLprCaptureForAllAsync("WeightStabilized");

    /// <inheritdoc />
    public Task CaptureOnOffScale() => TriggerLprCaptureForAllAsync("OffScale");

    private async Task TriggerLprCaptureForAllAsync(string phase)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (!settings.SystemSettings.EnableTriggerLprCapture)
        {
            _logger.LogInformation("LPR 主动抓拍已禁用，跳过抓拍 ({Phase})", phase);
            return;
        }

        try
        {
            if (settings.SystemSettings.LprDeviceType != LprDeviceType.Vzvision)
                return;

            if (_vzvisionLprService == null)
            {
                _logger.LogWarning("IVzvisionLprService 未注入，无法抓拍 ({Phase})", phase);
                return;
            }

            var lprConfigs = settings.LicensePlateRecognitionConfigs;
            if (lprConfigs.Count == 0)
            {
                _logger.LogWarning("未配置 Vzvision 车牌设备，跳过抓拍 ({Phase})", phase);
                return;
            }

            _logger.LogInformation("触发 Vzvision 抓拍 ({Phase})，设备数 {Count}", phase, lprConfigs.Count);

            var tasks = lprConfigs
                .Where(config => config.IsValid())
                .Select(async config =>
                {
                    try
                    {
                        await _vzvisionLprService.TriggerCaptureAsync(config);
                        _logger.LogInformation("Vzvision 抓拍已发送: {Name} ({Ip}) [{Phase}]",
                            config.Name, config.Ip, phase);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Vzvision 抓拍失败: {Name} ({Ip}) [{Phase}]",
                            config.Name, config.Ip, phase);
                        return false;
                    }
                });

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            var failCount = results.Length - successCount;

            _logger.LogInformation("Vzvision 抓拍完成 ({Phase}): 成功 {SuccessCount}，失败 {FailCount}",
                phase, successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 Vzvision 抓拍异常 ({Phase})", phase);
        }
    }
}
