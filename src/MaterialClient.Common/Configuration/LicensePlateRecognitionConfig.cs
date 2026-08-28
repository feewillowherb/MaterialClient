using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     License plate recognition configuration
/// </summary>
public class LicensePlateRecognitionConfig
{
    /// <summary>
    ///     Recognition device name (e.g., camera_1)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Device IP address (e.g., 192.168.3.245)
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    ///     设备物理侧别（A=入口，B=出口）
    ///     注意：A/B 表示物理位置侧别，与会话运行时角色（Entry/Exit）区分
    /// </summary>
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.A;

    /// <summary>
    ///     设备认证用户名（海康威视、Vzvision 臻识 SDK 连接使用）
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    ///     设备认证密码（海康威视、Vzvision 臻识 SDK 连接使用）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     设备服务端口：海康为设备服务端口字符串；Vzvision 为 <c>VzLPRClient_Open</c> 的 TCP 端口（常见 80）
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    ///     通道号（仅海康威视使用，默认 "1"）
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    ///     是否启用道闸 I/O 联动功能（统一建模，当前仅 Vzvision 执行）
    /// </summary>
    public bool EnableGateIo { get; set; } = false;

    /// <summary>
    ///     道闸 I/O 通道号（统一建模，当前仅 Vzvision 执行）
    /// </summary>
    public string? IoChannel { get; set; }

    /// <summary>
    ///     厂商类型。旧 JSON 缺省时为 null，加载后由 <see cref="ApplyLegacyDeviceType"/> 回填。
    /// </summary>
    public LprDeviceType? DeviceType { get; set; }

    public LprSiteType SiteType { get; set; } = LprSiteType.Scale;

    public void CoerceToScale() => SiteType = LprSiteType.Scale;

    /// <summary>
    ///     运行时使用的厂商（回填后的权威值）
    /// </summary>
    public LprDeviceType ResolvedDeviceType => DeviceType ?? LprDeviceType.Hikvision;

    /// <summary>
    ///     旧数据缺少行级类型时写入全局遗留值；已有 <see cref="DeviceType"/> 则不覆盖。
    /// </summary>
    public void ApplyLegacyDeviceType(LprDeviceType legacyGlobal)
    {
        DeviceType ??= legacyGlobal;
    }

    /// <summary>
    ///     按当前 <see cref="ResolvedDeviceType"/> 填空字段默认值。
    /// </summary>
    public void ApplyVendorDefaults()
    {
        var type = ResolvedDeviceType;
        if (HikvisionLprDefaults.ShouldApply(type))
            HikvisionLprDefaults.ApplyDefaults(this);
        else if (VzvisionLprDefaults.ShouldApply(type))
            VzvisionLprDefaults.ApplyDefaults(this);
    }

    public static LicensePlateRecognitionConfig FromUi(
        string name,
        string ip,
        LicensePlateDirection direction,
        string? userName,
        string? password,
        string? port,
        string? channel,
        bool enableGateIo,
        string? ioChannel,
        LprDeviceType deviceType,
        LprSiteType siteType = LprSiteType.Scale)
    {
        var config = new LicensePlateRecognitionConfig();
        config.ReplaceFromUi(
            name, ip, direction, userName, password, port, channel, enableGateIo, ioChannel, deviceType, siteType);
        return config;
    }

    public void ReplaceFromUi(
        string name,
        string ip,
        LicensePlateDirection direction,
        string? userName,
        string? password,
        string? port,
        string? channel,
        bool enableGateIo,
        string? ioChannel,
        LprDeviceType deviceType,
        LprSiteType siteType = LprSiteType.Scale)
    {
        Name = name;
        Ip = ip;
        Direction = direction;
        UserName = userName;
        Password = password;
        Port = port;
        Channel = deviceType == LprDeviceType.Hikvision
            ? (channel ?? HikvisionLprDefaults.DefaultChannel)
            : null;
        EnableGateIo = enableGateIo;
        IoChannel = string.IsNullOrWhiteSpace(ioChannel) ? "1" : ioChannel;
        DeviceType = deviceType;
        SiteType = siteType;
        ApplyVendorDefaults();
    }

    public static bool AnyValidOfType(
        IReadOnlyList<LicensePlateRecognitionConfig> configs,
        LprDeviceType deviceType)
    {
        if (configs.Count == 0)
            return false;
        foreach (var config in configs)
        {
            if (config.IsValid() && config.ResolvedDeviceType == deviceType)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     判断配置是否有效
    ///     需要Name和Ip都不为空
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Ip);
    }
}