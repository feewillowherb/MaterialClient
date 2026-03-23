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
    ///     Recognition direction (In or Out)
    /// </summary>
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.In;

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
    ///     判断配置是否有效
    ///     需要Name和Ip都不为空
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Ip);
    }
}