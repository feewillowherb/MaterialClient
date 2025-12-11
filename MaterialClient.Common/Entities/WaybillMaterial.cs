using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class WaybillMaterial : Entity<int>, IMaterialClientAuditedObject
{
    protected WaybillMaterial()
    {
    }

    public long OrderId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialName { get; set; }


    public string? Specifications { get; set; }

    public int? MaterialUnitId { get; set; }

    public decimal GoodsPlanOnWeight { get; set; }

    public decimal GoodsPlanOnPcs { get; set; }

    public decimal GoodsPcs { get; set; }

    public decimal GoodsWeight { get; set; }

    public decimal GoodsTakeWeight { get; set; }

    public int OffsetResult { get; set; }

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
}