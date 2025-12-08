namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 称重记录明细项DTO（用于表格行显示）
/// </summary>
public class WeighingRecordItemDto
{
    /// <summary>
    /// 材料ID
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 材料名称
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// 材料单位ID
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string? UnitName { get; set; }

    /// <summary>
    /// 换算率(吨)
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// 运单数量
    /// </summary>
    public decimal? WaybillQuantity { get; set; }

    /// <summary>
    /// 运单重量
    /// </summary>
    public decimal WaybillWeight { get; set; }

    /// <summary>
    /// 实际数量
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// 实际重量
    /// </summary>
    public decimal ActualWeight { get; set; }

    /// <summary>
    /// 正负差
    /// </summary>
    public decimal Difference { get; set; }

    /// <summary>
    /// 偏差率
    /// </summary>
    public decimal DeviationRate { get; set; }

    /// <summary>
    /// 偏差结果
    /// </summary>
    public string? DeviationResult { get; set; }
}
