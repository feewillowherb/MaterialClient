using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Csv;

namespace MaterialClientToolkit.Models;

/// <summary>
/// Material_OrderGoods CSV数据模型
/// </summary>
public class MaterialOrderGoodsCsv
{
    [ImporterHeader(Name = "OGId")]
    public int OGId { get; set; }

    [ImporterHeader(Name = "OrderId")]
    public long OrderId { get; set; }

    [ImporterHeader(Name = "GoodsId")]
    public int GoodsId { get; set; }

    [ImporterHeader(Name = "UnitId")]
    public int? UnitId { get; set; }

    [ImporterHeader(Name = "GoodsPlanOnWeight")]
    public decimal GoodsPlanOnWeight { get; set; }

    [ImporterHeader(Name = "GoodsPlanOnPcs")]
    public decimal GoodsPlanOnPcs { get; set; }

    [ImporterHeader(Name = "GoodsPcs")]
    public decimal GoodsPcs { get; set; }

    [ImporterHeader(Name = "GoodsWeight")]
    public decimal GoodsWeight { get; set; }

    [ImporterHeader(Name = "GoodsTakeWeight")]
    public decimal? GoodsTakeWeight { get; set; }

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

    [ImporterHeader(Name = "AddTime")]
    public int? AddTime { get; set; }

    [ImporterHeader(Name = "UpdateDate")]
    public string? UpdateDate { get; set; }

    [ImporterHeader(Name = "AddDate")]
    public string? AddDate { get; set; }

    [ImporterHeader(Name = "OffsetResult")]
    public int OffsetResult { get; set; }

    [ImporterHeader(Name = "OffsetWeight")]
    public decimal OffsetWeight { get; set; }

    [ImporterHeader(Name = "OffsetCount")]
    public decimal OffsetCount { get; set; }

    [ImporterHeader(Name = "OffsetRate")]
    public decimal OffsetRate { get; set; }
}

