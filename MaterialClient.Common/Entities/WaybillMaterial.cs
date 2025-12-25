using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class WaybillMaterial : Entity<int>, IMaterialClientAuditedObject
{
    public WaybillMaterial()
    {
    }

    public WaybillMaterial(long waybillId, int materialId, string materialName, string? specifications,
        int? materialUnitId, decimal goodsPlanOnPcs)
    {
        WaybillId = waybillId;
        MaterialId = materialId;
        MaterialName = materialName;
        Specifications = specifications;
        MaterialUnitId = materialUnitId;
        GoodsPlanOnPcs = goodsPlanOnPcs;
        GoodsPcs = goodsPlanOnPcs;
    }

    public long WaybillId { get; set; }

    public int MaterialId { get; set; }

    public string? MaterialName { get; set; }


    public string? Specifications { get; set; }

    public int? MaterialUnitId { get; set; }

    public decimal GoodsPlanOnWeight { get; set; }

    public decimal GoodsPlanOnPcs { get; set; }

    public decimal GoodsPcs { get; set; }

    public decimal GoodsWeight { get; set; }

    /// <summary>
    /// 扣重 暂不用
    /// </summary>
    public decimal GoodsTakeWeight { get; set; }

    public OffsetResultType OffsetResult { get; set; }

    public decimal OffsetWeight { get; set; }

    public decimal OffsetCount { get; set; }

    public decimal OffsetRate { get; set; }


    #region Audited Properties

    public int? LastEditUserId { get; set; }
    public string? LastEditor { get; set; }
    public int? CreateUserId { get; set; }
    public string? Creator { get; set; }
    public int? UpdateTime { get; set; }
    public int? AddTime { get; set; }
    public DateTime? UpdateDate { get; set; }
    public DateTime? AddDate { get; set; }

    #endregion


    public void UpdateOffsetFromWaybill(Waybill waybill)
    {
        OffsetWeight = waybill.OrderGoodsWeight!.Value - waybill.OrderPlanOnWeight!.Value;
        GoodsWeight = waybill.OrderGoodsWeight!.Value;
        OffsetCount = waybill.OffsetCount;
        OffsetRate = waybill.OffsetRate;
        OffsetResult = waybill.OffsetResult;
        OffsetRate = Math.Round(waybill.OffsetRate / 100, 2);
        GoodsPlanOnWeight = waybill.OrderPlanOnWeight!.Value;
    }
}