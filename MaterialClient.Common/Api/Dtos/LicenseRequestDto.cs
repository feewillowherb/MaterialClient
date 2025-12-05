namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 授权请求DTO
/// </summary>
public class LicenseRequestDto
{
    /// <summary>
    /// 产品代码（固定为"5000"）
    /// </summary>
    public string ProductCode { get; set; }

    /// <summary>
    /// 用户输入的授权码
    /// </summary>
    public string Code { get; set; }
}

