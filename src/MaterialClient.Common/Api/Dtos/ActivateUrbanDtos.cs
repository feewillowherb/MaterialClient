using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

public record ActivateUrbanRequest(int ProductCode, string Code, string MachineCode);

public class ActivateUrbanResponseData
{
    [JsonPropertyName("jwtToken")]
    public string? JwtToken { get; set; }

    [JsonPropertyName("proId")]
    public string? ProId { get; set; }

    [JsonPropertyName("proName")]
    public string? ProName { get; set; }

    [JsonPropertyName("authEndDate")]
    public string? AuthEndDate { get; set; }
}
