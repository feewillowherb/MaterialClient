using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     将 LPR 设备配置映射为 JPEG 抓拍所需的相机配置。
/// </summary>
internal static class HikvisionCaptureConfigHelper
{
    internal static List<CameraConfig> ResolveCameraConfigs(SettingsEntity settings)
    {
        if (settings.CameraConfigs.Count > 0)
            return settings.CameraConfigs;

        if (settings.SystemSettings.LprDeviceType != LprDeviceType.Hikvision)
            return settings.CameraConfigs;

        return settings.LicensePlateRecognitionConfigs
            .Where(config => config.IsValid())
            .Select(config => new CameraConfig
            {
                Name = config.Name,
                Ip = config.Ip,
                Port = string.IsNullOrWhiteSpace(config.Port) ? "8000" : config.Port,
                Channel = string.IsNullOrWhiteSpace(config.Channel) ? "1" : config.Channel,
                UserName = config.UserName ?? string.Empty,
                Password = config.Password ?? string.Empty
            })
            .Where(config => config.IsValid())
            .ToList();
    }
}
