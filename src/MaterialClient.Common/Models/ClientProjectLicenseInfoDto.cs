using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

/// <summary>
///     服务端项目授权信息 DTO。buildLicenseNo 映射到 LicenseInfo.AccessCode。
/// </summary>
public class ClientProjectLicenseInfoDto
{
    public string ProName { get; set; } = string.Empty;

    [JsonPropertyName("buildLicenseNo")]
    public string BuildLicenseNo { get; set; } = string.Empty;

    public DateTime AuthEndTime { get; set; }
}
