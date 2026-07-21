using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     Recycle 专用 Waybill 扩展实体。
///     存储资源化利用厂 §2.2 上报所需的扩展字段：<see cref="UnitPrice" />（单价）、<see cref="SaleContractNo" />（销售合同编号）、
///     <see cref="ReceivingTime" />（收货时间）、<see cref="IsReceived" />（是否已提交收货）。
///     遵循既有 <see cref="Urban.UrbanWeighingExtension" /> 约定：按 <see cref="WaybillId" /> 逻辑关联，
///     无数据库外键、无 EF 导航属性，由服务层显式按 <see cref="WaybillId" /> 查询/upsert（每个 Waybill 至多一条扩展）。
///     不扩展 Waybill 主表，保持主表精简、与 Urban 模式一致。
/// </summary>
public class RecycleWaybillExtension : Entity<Guid>
{
    /// <summary>
    ///     构造函数（用于 EF Core）。
    /// </summary>
    protected RecycleWaybillExtension()
    {
    }

    /// <summary>
    ///     构造函数（用于指定 WaybillId）。
    /// </summary>
    public RecycleWaybillExtension(long waybillId)
    {
        WaybillId = waybillId;
    }

    /// <summary>
    ///     关联 <see cref="Waybill" /> 的 Id（逻辑关联，非数据库外键；每个 Waybill 至多一条扩展）。
    /// </summary>
    public long WaybillId { get; set; }

    /// <summary>
    ///     单价（元/吨，可选）。§2.2 <c>unitPrice</c> 数据源。
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    ///     销售合同编号（可选）。§2.2 <c>saleContractNo</c> 数据源。
    /// </summary>
    public string? SaleContractNo { get; set; }

    /// <summary>
    ///     收货时间（可选）。§2.2 <c>receivingTime</c> 数据源，由收货动作写入。
    /// </summary>
    public DateTime? ReceivingTime { get; set; }

    /// <summary>
    ///     是否已提交过收货。由收货动作置为 true；不影响收货按钮是否可点。
    /// </summary>
    public bool IsReceived { get; set; }
}
