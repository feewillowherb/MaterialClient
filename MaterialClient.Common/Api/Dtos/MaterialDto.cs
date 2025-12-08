namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 材料DTO（用于下拉列表）
/// </summary>
public class MaterialDto
{
    /// <summary>
    /// 材料ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 材料名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 品牌
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 规格尺寸
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string? UnitName { get; set; }

    /// <summary>
    /// 单位换算率
    /// </summary>
    public decimal UnitRate { get; set; } = 1;
}
