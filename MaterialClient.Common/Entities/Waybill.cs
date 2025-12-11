using System;
using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 运单实体
/// </summary>
public class Waybill : FullAuditedEntity<long>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected Waybill()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
    /// </summary>
    public Waybill(string orderNo)
    {
        OrderNo = orderNo;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public Waybill(long id, string orderNo, int providerId)
        : base(id)
    {
        OrderNo = orderNo;
        ProviderId = providerId;
    }

    /// <summary>
    /// 供应商ID (FK to Provider)
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 订单类型
    /// </summary>
    public int? OrderType { get; set; }

    /// <summary>
    /// 配送类型
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }

    /// <summary>
    /// 进场时间
    /// </summary>
    public DateTime? JoinTime { get; set; }

    /// <summary>
    /// 出场时间
    /// </summary>
    public DateTime? OutTime { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 计划重量
    /// </summary>
    public decimal? OrderPlanOnWeight { get; set; }

    /// <summary>
    /// 计划件数
    /// </summary>
    public decimal? OrderPlanOnPcs { get; set; }

    /// <summary>
    /// 实际件数
    /// </summary>
    public decimal? OrderPcs { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal? OrderTotalWeight { get; set; }

    /// <summary>
    /// 车辆重量
    /// </summary>
    public decimal? OrderTruckWeight { get; set; }

    /// <summary>
    /// 货物重量
    /// </summary>
    public decimal? OrderGoodsWeight { get; set; }

    /// <summary>
    /// 最后同步时间
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// 是否预警
    /// </summary>
    public bool IsEarlyWarn { get; set; } = false;

    /// <summary>
    /// 打印次数
    /// </summary>
    public int PrintCount { get; set; } = 0;

    /// <summary>
    /// 中止原因
    /// </summary>
    public string? AbortReason { get; set; }

    /// <summary>
    /// 偏移结果
    /// </summary>
    public OffsetResultType OffsetResult { get; set; } = OffsetResultType.Default;

    /// <summary>
    /// 预警类型
    /// </summary>
    public string? EarlyWarnType { get; set; }

    /// <summary>
    /// 订单来源
    /// </summary>
    public OrderSource OrderSource { get; set; }

    // Navigation properties
    /// <summary>
    /// 供应商导航属性
    /// </summary>
    public Provider? Provider { get; set; }


    /// <summary>
    /// 物料Id
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 物料单位Id
    /// </summary>
    public int? MaterialUnitId { get; set; }


    /// <summary>
    /// 物料单位
    /// </summary>
    public decimal? MaterialUnitRate { get; set; }


    public static string GenerateOrderNo(DeliveryType deliveryType, DateTime dateTime, int todayCount)
    {
        var content = deliveryType == Enums.DeliveryType.Receiving
            ? $"sl-{dateTime:yyyyMMddHHmmSS}-{todayCount:D4}"
            : $"fl-{dateTime:yyyyMMddHHmmSS}-{todayCount:D4}";
        return content;
    }


    public void SetWeight(WeighingRecord joinRecord, WeighingRecord outRecord, DeliveryType deliveryType)
    {
        if (deliveryType == MaterialClient.Common.Entities.Enums.DeliveryType.Sending)
        {
            OrderTruckWeight = joinRecord.Weight;
            OrderTotalWeight = outRecord.Weight;
            OrderGoodsWeight = outRecord.Weight - joinRecord.Weight;
        }
        else if (deliveryType == MaterialClient.Common.Entities.Enums.DeliveryType.Receiving)
        {
            OrderTruckWeight = outRecord.Weight;
            OrderTotalWeight = joinRecord.Weight;
            OrderGoodsWeight = joinRecord.Weight - outRecord.Weight;
        }
    }


    public void CalculateMaterialWeight(decimal lowerLimit, decimal upperLimit)
    {
        if (!OrderPlanOnPcs.HasValue || !MaterialUnitRate.HasValue || !OrderTruckWeight.HasValue ||
            !OrderTotalWeight.HasValue ||
            !OrderGoodsWeight.HasValue)
        {
            return;
        }

        var unitRate = MaterialUnitRate.Value;

        OrderPlanOnWeight = Math.Round(OrderPlanOnPcs.Value * unitRate, 2, MidpointRounding.AwayFromZero);

        var rowWeight = Math.Round(OrderGoodsWeight.Value * OrderPlanOnWeight.Value / OrderPlanOnWeight.Value, 4,
            MidpointRounding.AwayFromZero);

        OrderPcs = Math.Round(rowWeight / unitRate, 4, MidpointRounding.AwayFromZero);


        var rowOffSetRate = Math.Round((rowWeight - OrderPlanOnWeight.Value) * 100 / OrderPlanOnWeight.Value, 4,
            MidpointRounding.AwayFromZero);

        if (lowerLimit < 0 || upperLimit > 0)
        {
            if (rowOffSetRate < 0 && rowOffSetRate < lowerLimit)
            {
                OffsetResult = OffsetResultType.OverNegativeDeviation;
            }
            else if (rowOffSetRate > 0 && rowOffSetRate > upperLimit)
            {
                OffsetResult = OffsetResultType.OverPositiveDeviation;
            }
            else
            {
                OffsetResult = OffsetResultType.Normal;
            }
        }
    }
}