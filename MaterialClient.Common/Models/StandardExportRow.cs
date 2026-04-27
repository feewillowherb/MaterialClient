namespace MaterialClient.Common.Models;

/// <summary>
///     标准模式运单导出行 DTO，与台账对话框 15 列一一对应
/// </summary>
public class StandardExportRow
{
    public string PlateNumber { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public decimal? PlanQuantity { get; set; }
    public decimal? PlanWeight { get; set; }
    public decimal? OffsetCount { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal? ActualWeight { get; set; }
    public decimal? UnitConversion { get; set; }
    public string JoinTime { get; set; } = string.Empty;
    public string OutTime { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}
