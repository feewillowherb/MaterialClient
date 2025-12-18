using System;
using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;

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
    public int? ProviderId { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 订单类型
    /// </summary>
    public OrderTypeEnum? OrderType { get; set; }

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
    /// 是否需要同步
    /// </summary>
    public bool IsPendingSync { get; set; } = false;

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

    public decimal? OffsetRate { get; set; }

    /// <summary>
    /// 预警类型
    /// </summary>
    public string? EarlyWarnType { get; set; }

    /// <summary>
    /// 订单来源
    /// </summary>
    public OrderSource OrderSource { get; set; }

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


    public void SyncCompleted(DateTime now)
    {
        LastSyncTime = now;
    }

    public void OrderTypeCompleted()
    {
        OrderType = OrderTypeEnum.Completed;
    }


    public decimal? GetJoinWeight()
    {
        if (DeliveryType == Enums.DeliveryType.Sending)
        {
            return OrderTruckWeight ?? 0;
        }
        else if (DeliveryType == Enums.DeliveryType.Receiving)
        {
            return OrderTotalWeight ?? 0;
        }

        return null;
    }

    public decimal? GetOutWeight()
    {
        if (DeliveryType == Enums.DeliveryType.Sending)
        {
            return OrderTotalWeight ?? 0;
        }
        else if (DeliveryType == Enums.DeliveryType.Receiving)
        {
            return OrderTruckWeight ?? 0;
        }

        return null;
    }

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
            OrderTruckWeight = joinRecord.TotalWeight;
            OrderTotalWeight = outRecord.TotalWeight;
            OrderGoodsWeight = outRecord.TotalWeight - joinRecord.TotalWeight;
        }
        else if (deliveryType == MaterialClient.Common.Entities.Enums.DeliveryType.Receiving)
        {
            OrderTruckWeight = outRecord.TotalWeight;
            OrderTotalWeight = joinRecord.TotalWeight;
            OrderGoodsWeight = joinRecord.TotalWeight - outRecord.TotalWeight;
        }
    }


    public void CalculateMaterialWeight(decimal? lowerLimit, decimal? upperLimit)
    {
        var calc = new MaterialCalculation(
            OrderPlanOnPcs,
            OrderGoodsWeight,
            MaterialUnitRate,
            lowerLimit,
            upperLimit);

        if (!calc.IsValid) return;

        OrderPlanOnWeight = calc.PlanWeight;
        OrderPcs = calc.ActualQuantity;
        OffsetRate = calc.DeviationRate;
        OffsetResult = calc.OffsetResult;
    }


    public void ResetPendingSync()
    {
        IsPendingSync = true;
    }

    public void SetPendingSync()
    {
        IsPendingSync = false;
    }
}

public enum OrderTypeEnum
{
    FirstWeight = 0, //收料/发料中
    Completed = 1, //完成收料/发料
    Esc = 2, //已取消
}