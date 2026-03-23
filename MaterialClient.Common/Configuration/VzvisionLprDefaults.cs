using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     臻识 Vz SDK 车牌设备默认配置（与 UI 添加对话框、保存时补全空字段一致）
/// </summary>
public static class VzvisionLprDefaults
{
    public const string DefaultUserName = "admin";

    /// <summary>SDK 常见 HTTP 端口</summary>
    public const string DefaultPort = "80";

    public static bool ShouldApply(LprDeviceType deviceType) => deviceType == LprDeviceType.Vzvision;

    /// <summary>
    ///     对配置应用默认值：仅当对应字段为空或空白时写入
    /// </summary>
    public static void ApplyDefaults(LicensePlateRecognitionConfig config)
    {
        if (config == null) return;

        if (string.IsNullOrWhiteSpace(config.UserName))
            config.UserName = DefaultUserName;
        if (string.IsNullOrWhiteSpace(config.Port))
            config.Port = DefaultPort;
        if (config.Password == null)
            config.Password = string.Empty;
    }
}
