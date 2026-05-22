using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     海康威视车牌识别设备默认配置（唯一数据源）
///     用于添加/编辑对话框的 UI 默认显示与保存时填充空字段
/// </summary>
public static class HikvisionLprDefaults
{
    /// <summary>默认用户名</summary>
    public const string DefaultUserName = "admin";

    /// <summary>默认端口</summary>
    public const string DefaultPort = "8000";

    /// <summary>默认通道号</summary>
    public const string DefaultChannel = "1";

    /// <summary>
    ///     对配置应用海康威视默认值：仅当对应字段为空或空白时写入默认值
    /// </summary>
    public static void ApplyDefaults(LicensePlateRecognitionConfig config)
    {
        if (config == null) return;

        if (string.IsNullOrWhiteSpace(config.UserName))
            config.UserName = DefaultUserName;
        if (string.IsNullOrWhiteSpace(config.Port))
            config.Port = DefaultPort;
        if (string.IsNullOrWhiteSpace(config.Channel))
            config.Channel = DefaultChannel;
        if (config.Password == null)
            config.Password = string.Empty;
    }

    /// <summary>
    ///     是否应对该设备类型应用海康默认值
    /// </summary>
    public static bool ShouldApply(LprDeviceType deviceType) => deviceType == LprDeviceType.Hikvision;
}
