using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

/// <summary>
///     SignalR UpdateClientLicense 推送 DTO
/// </summary>
public class ClientLicenseUpdateDto
{
    [JsonPropertyName("jwtToken")]
    public string JwtToken { get; set; } = string.Empty;
}
