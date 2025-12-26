namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     供应商DTO（用于下拉列表）
/// </summary>
public class ProviderDto
{
    /// <summary>
    ///     供应商ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     供应商类型
    /// </summary>
    public int ProviderType { get; set; }

    /// <summary>
    ///     供应商名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    ///     联系人姓名
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    ///     联系人电话
    /// </summary>
    public string? ContactPhone { get; set; }
}