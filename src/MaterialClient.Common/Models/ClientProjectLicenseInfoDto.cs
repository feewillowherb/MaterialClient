using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

/// <summary>
///     从 UrbanManagement DeviceStatusHub 同步的项目授权信息。
/// </summary>
public class ClientProjectLicenseInfoDto
{
    [JsonPropertyName("proName")]
    public string ProName { get; set; } = string.Empty;

    [JsonPropertyName("buildLicenseNo")]
    public string BuildLicenseNo { get; set; } = string.Empty;

    [JsonPropertyName("fdBuildLicenseNo")]
    public string FdBuildLicenseNo { get; set; } = string.Empty;

    [JsonPropertyName("authEndTime")]
    public DateTime? AuthEndTime { get; set; }
}
