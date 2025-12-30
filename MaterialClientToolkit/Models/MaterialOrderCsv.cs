using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Csv;

namespace MaterialClientToolkit.Models;

/// <summary>
/// Material_Order CSV数据模型
/// </summary>
[CsvImporter(IsLabelingError = true)]
public class MaterialOrderCsv
{
    [ImporterHeader(Name = "OrderId")]
    public long OrderId { get; set; }

    [ImporterHeader(Name = "ProviderId")]
    public int? ProviderId { get; set; }

    [ImporterHeader(Name = "OrderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [ImporterHeader(Name = "OrderType")]
    public int? OrderType { get; set; }

    [ImporterHeader(Name = "DeliveryType")]
    public int? DeliveryType { get; set; }

    [ImporterHeader(Name = "TruckNo")]
    public string? TruckNo { get; set; }

    [ImporterHeader(Name = "DispatchNo")]
    public string? DispatchNo { get; set; }

    [ImporterHeader(Name = "OrderPlanOnWeight")]
    public decimal? OrderPlanOnWeight { get; set; }

    [ImporterHeader(Name = "OrderPlanOnPcs")]
    public decimal? OrderPlanOnPcs { get; set; }

    [ImporterHeader(Name = "OrderPcs")]
    public decimal? OrderPcs { get; set; }

    [ImporterHeader(Name = "JoinTime")]
    public string? JoinTime { get; set; }

    [ImporterHeader(Name = "OutTime")]
    public string? OutTime { get; set; }

    [ImporterHeader(Name = "Remark")]
    public string? Remark { get; set; }

    [ImporterHeader(Name = "OrderTotalWeight")]
    public decimal? OrderTotalWeight { get; set; }

    [ImporterHeader(Name = "OrderTruckWeight")]
    public decimal? OrderTruckWeight { get; set; }

    [ImporterHeader(Name = "OrderGoodsWeight")]
    public decimal? OrderGoodsWeight { get; set; }

    [ImporterHeader(Name = "DeleteStatus")]
    public int DeleteStatus { get; set; }

    [ImporterHeader(Name = "LastEditUserId")]
    public int? LastEditUserId { get; set; }

    [ImporterHeader(Name = "LastEditor")]
    public string? LastEditor { get; set; }

    [ImporterHeader(Name = "CreateUserId")]
    public int? CreateUserId { get; set; }

    [ImporterHeader(Name = "Creator")]
    public string? Creator { get; set; }

    [ImporterHeader(Name = "UpdateTime")]
    public int? UpdateTime { get; set; }

    [ImporterHeader(Name = "AddDate")]
    public string? AddDate { get; set; }

    [ImporterHeader(Name = "UpdateDate")]
    public string? UpdateDate { get; set; }

    [ImporterHeader(Name = "AddTime")]
    public int? AddTime { get; set; }

    [ImporterHeader(Name = "LastSyncTime")]
    public string? LastSyncTime { get; set; }

    [ImporterHeader(Name = "EarlyWarnStatus")]
    public int? EarlyWarnStatus { get; set; }

    [ImporterHeader(Name = "PrintCount")]
    public int PrintCount { get; set; }

    [ImporterHeader(Name = "AbortReason")]
    public string? AbortReason { get; set; }

    [ImporterHeader(Name = "ReceivederId")]
    public int? ReceivederId { get; set; }

    [ImporterHeader(Name = "OffsetResult")]
    public int? OffsetResult { get; set; }

    [ImporterHeader(Name = "EarlyWarnType")]
    public string? EarlyWarnType { get; set; }

    [ImporterHeader(Name = "OrderSource")]
    public int? OrderSource { get; set; }

    [ImporterHeader(Name = "TruckNum")]
    public string? TruckNum { get; set; }
}

