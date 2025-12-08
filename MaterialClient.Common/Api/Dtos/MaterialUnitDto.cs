namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 材料单位DTO（用于下拉列表）
/// </summary>
public class MaterialUnitDto
{
    /// <summary>
    /// 单位ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 材料ID
    /// </summary>
    public int MaterialId { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// 换算率
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// 换算率名称
    /// </summary>
    public string? RateName { get; set; }

    /// <summary>
    /// 供应商ID
    /// </summary>
    public int? ProviderId { get; set; }
}
