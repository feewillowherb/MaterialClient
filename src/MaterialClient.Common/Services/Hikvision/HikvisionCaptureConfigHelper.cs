using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     解析 UrbanPhoto / 监控 JPEG 抓拍所需的相机配置。
///     仅使用 <see cref="SettingsEntity.CameraConfigs"/>，不以 LPR 设备代偿。
/// </summary>
internal static class HikvisionCaptureConfigHelper
{
    internal static List<CameraConfig> ResolveCameraConfigs(SettingsEntity settings)
    {
        return settings.CameraConfigs
            .Where(config => config.IsValid())
            .ToList();
    }
}
