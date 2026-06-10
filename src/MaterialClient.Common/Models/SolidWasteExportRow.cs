namespace MaterialClient.Common.Models;

/// <summary>
///     固废运单导出行 DTO，与 sample.csv 模板 18 列一一对应
/// </summary>
public class SolidWasteExportRow
{
    public string SerialNumber { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string WeighingType { get; set; } = string.Empty;
    public string ShippingUnit { get; set; } = string.Empty;
    public string ReceivingUnit { get; set; } = string.Empty;
    public string GoodsName { get; set; } = string.Empty;
    public decimal? GrossWeight { get; set; }
    public decimal? TareWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public string Remark { get; set; } = string.Empty;
    public string GrossWeightTime { get; set; } = string.Empty;
    public string TareWeightTime { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string SolidWasteType { get; set; } = string.Empty;
    public string ManifestNumber { get; set; } = string.Empty;
    public string UploadResult { get; set; } = string.Empty;
    public string UploadStatus { get; set; } = string.Empty;
    public string UploadTime { get; set; } = string.Empty;
}
