namespace MaterialClient.Common.Configuration;

/// <summary>
///     Camera configuration
/// </summary>
public class CameraConfig
{
    /// <summary>
    ///     Camera name (e.g., camera_1)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Camera IP address (e.g., 192.168.3.245)
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    ///     Camera port (e.g., 8000)
    /// </summary>
    public string Port { get; set; } = string.Empty;

    /// <summary>
    ///     Camera channel (e.g., 1)
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    ///     User name (e.g., admin)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    ///     Password (e.g., fdkj112233)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     判断配置是否有效
    ///     需要Ip、Port和Channel都不为空，且Port和Channel可以解析为整数
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Ip) ||
            string.IsNullOrWhiteSpace(Port) ||
            string.IsNullOrWhiteSpace(Channel))
            return false;

        return int.TryParse(Port, out _) && int.TryParse(Channel, out _);
    }
}