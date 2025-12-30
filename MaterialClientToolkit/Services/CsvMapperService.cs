using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClientToolkit.Models;
using Volo.Abp.DependencyInjection;

namespace MaterialClientToolkit.Services;

/// <summary>
/// CSV数据映射服务
/// 所有映射逻辑封装在此类中，便于统一修改
/// </summary>
public class CsvMapperService : ITransientDependency
{
    /// <summary>
    /// 判断Material_Order记录是否为Waybill（有OutTime则为Waybill）
    /// </summary>
    public bool IsWaybill(MaterialOrderCsv csv)
    {
        return !string.IsNullOrWhiteSpace(csv.OutTime);
    }

    /// <summary>
    /// Material_Order → Waybill 映射
    /// </summary>
    public Waybill MapToWaybill(MaterialOrderCsv csv)
    {
        var waybill = new Waybill(csv.OrderId, csv.OrderNo)
        {
            ProviderId = csv.ProviderId,
            OrderType = MapOrderType(csv.OrderType),
            DeliveryType = MapDeliveryType(csv.DeliveryType),
            PlateNumber = csv.TruckNo,
            JoinTime = MapDateTime(csv.JoinTime),
            OutTime = MapDateTime(csv.OutTime),
            Remark = csv.Remark,
            OrderPlanOnWeight = csv.OrderPlanOnWeight,
            OrderPlanOnPcs = csv.OrderPlanOnPcs,
            OrderPcs = csv.OrderPcs,
            OrderTotalWeight = csv.OrderTotalWeight,
            OrderTruckWeight = csv.OrderTruckWeight,
            OrderGoodsWeight = csv.OrderGoodsWeight,
            LastSyncTime = MapDateTime(csv.LastSyncTime),
            IsEarlyWarn = MapBool(csv.EarlyWarnStatus),
            PrintCount = csv.PrintCount,
            AbortReason = csv.AbortReason,
            OffsetResult = MapOffsetResultType(csv.OffsetResult),
            EarlyWarnType = csv.EarlyWarnType,
            OrderSource = MapOrderSource(csv.OrderSource),
            IsDeleted = MapDeleteStatus(csv.DeleteStatus),
            LastEditUserId = csv.LastEditUserId,
            LastEditor = csv.LastEditor,
            CreateUserId = csv.CreateUserId,
            Creator = csv.Creator,
            UpdateTime = csv.UpdateTime,
            AddTime = csv.AddTime ?? 0,
            UpdateDate = MapDateTime(csv.UpdateDate),
            AddDate = MapDateTime(csv.AddDate) ?? DateTime.Now
        };

        return waybill;
    }

    /// <summary>
    /// Material_Order → WeighingRecord 映射
    /// </summary>
    public WeighingRecord MapToWeighingRecord(MaterialOrderCsv csv)
    {
        var weighingRecord = new WeighingRecord(
            csv.OrderId,
            csv.OrderTotalWeight ?? 0m)
        {
            PlateNumber = csv.TruckNo,
            ProviderId = csv.ProviderId,
            DeliveryType = MapDeliveryType(csv.DeliveryType),
            IsDeleted = MapDeleteStatus(csv.DeleteStatus),
            LastEditUserId = csv.LastEditUserId,
            LastEditor = csv.LastEditor,
            CreateUserId = csv.CreateUserId,
            Creator = csv.Creator,
            UpdateTime = csv.UpdateTime,
            AddTime = csv.AddTime ?? 0,
            UpdateDate = MapDateTime(csv.UpdateDate),
            AddDate = MapDateTime(csv.AddDate) ?? MapDateTime(csv.JoinTime) ?? DateTime.Now
        };

        return weighingRecord;
    }

    /// <summary>
    /// Material_OrderGoods → WaybillMaterial 映射
    /// </summary>
    /// <param name="csv">CSV数据</param>
    /// <param name="waybillId">Waybill ID</param>
    /// <param name="materialName">物料名称（从Material表查询，如果查询不到则为null）</param>
    public WaybillMaterial MapToWaybillMaterial(MaterialOrderGoodsCsv csv, long waybillId, string? materialName = null)
    {
        var waybillMaterial = new WaybillMaterial
        {
            WaybillId = waybillId,
            MaterialId = csv.GoodsId, // GoodsId映射到MaterialId
            MaterialName = materialName, // 从Material表查询的物料名称，查询不到则为null
            MaterialUnitId = csv.UnitId,
            GoodsPlanOnWeight = csv.GoodsPlanOnWeight,
            GoodsPlanOnPcs = csv.GoodsPlanOnPcs,
            GoodsPcs = csv.GoodsPcs,
            GoodsWeight = csv.GoodsWeight,
            GoodsTakeWeight = csv.GoodsTakeWeight ?? 0,
            OffsetResult = MapOffsetResultType(csv.OffsetResult),
            OffsetWeight = csv.OffsetWeight,
            OffsetCount = csv.OffsetCount,
            OffsetRate = csv.OffsetRate,
            IsDeleted = MapDeleteStatus(csv.DeleteStatus),
            LastEditUserId = csv.LastEditUserId,
            LastEditor = csv.LastEditor,
            CreateUserId = csv.CreateUserId,
            Creator = csv.Creator,
            UpdateTime = csv.UpdateTime,
            AddTime = csv.AddTime ?? 0,
            UpdateDate = MapDateTime(csv.UpdateDate),
            AddDate = MapDateTime(csv.AddDate) ?? DateTime.Now
        };

        return waybillMaterial;
    }

    /// <summary>
    /// Material_Attaches → AttachmentFile 映射
    /// </summary>
    public AttachmentFile MapToAttachmentFile(MaterialAttachesCsv csv)
    {
        var attachmentFile = new AttachmentFile(
            csv.FileId,
            csv.FileName,
            csv.BucketKey ?? string.Empty, // BucketKey映射到LocalPath（待确认）
            MapBizTypeToAttachType(csv.BizType))
        {
            OssFullPath = csv.BucketKey, // BucketKey也映射到OssFullPath（待确认）
            LastSyncTime = MapDateTime(csv.LastSyncTime),
            IsDeleted = MapDeleteStatus(csv.DeleteStatus),
            LastEditUserId = csv.LastEditUserId,
            LastEditor = csv.LastEditor,
            CreateUserId = csv.CreateUserId,
            Creator = csv.Creator,
            UpdateTime = csv.UpdateTime,
            AddTime = csv.AddTime ?? 0,
            UpdateDate = MapDateTime(csv.UpdateDate),
            AddDate = MapDateTime(csv.AddDate) ?? DateTime.Now
        };

        return attachmentFile;
    }

    /// <summary>
    /// BizType → AttachType 映射
    /// 注意：此映射规则需要根据实际业务需求调整
    /// </summary>
    public AttachType MapBizTypeToAttachType(int bizType)
    {
        // 根据CSV数据分析，BizType=1可能表示Waybill相关的附件
        // 这里需要根据实际业务规则调整映射逻辑
        // 暂时使用默认值，具体映射规则待确认
        return bizType switch
        {
            0 => AttachType.UnmatchedEntryPhoto,
            1 => AttachType.EntryPhoto, // 假设1表示进场照片
            2 => AttachType.ExitPhoto, // 假设2表示出场照片
            3 => AttachType.TicketPhoto, // 假设3表示票据照片
            _ => AttachType.UnmatchedEntryPhoto
        };
    }

    /// <summary>
    /// 判断BizType是否对应Waybill（BizType=1可能表示Waybill）
    /// </summary>
    public bool IsBizTypeForWaybill(int bizType)
    {
        // 根据CSV数据分析，BizType=1可能表示Waybill
        // 此映射规则需要根据实际业务需求调整
        return bizType == 1;
    }

    /// <summary>
    /// OrderSource 值映射
    /// null或0都映射为OrderSource.MannedStation（有人值守）
    /// </summary>
    public OrderSource MapOrderSource(int? orderSource)
    {
        if (!orderSource.HasValue || orderSource.Value == 0)
            return OrderSource.MannedStation; // 默认值：有人值守

        return orderSource.Value switch
        {
            1 => OrderSource.MannedStation,
            2 => OrderSource.ManualEntry,
            3 => OrderSource.MobileAcceptance,
            4 => OrderSource.UnmannedStation,
            _ => OrderSource.MannedStation // 未知值也使用默认值
        };
    }

    /// <summary>
    /// OrderType 映射（保持原值，因为Waybill.OrderType是int?）
    /// </summary>
    public OrderTypeEnum? MapOrderType(int? orderType)
    {
        if (!orderType.HasValue)
            return null;

        return orderType.Value switch
        {
            0 => OrderTypeEnum.FirstWeight,
            1 => OrderTypeEnum.Completed,
            2 => OrderTypeEnum.Esc,
            _ => null
        };
    }

    /// <summary>
    /// DeliveryType 映射
    /// </summary>
    public DeliveryType? MapDeliveryType(int? deliveryType)
    {
        if (!deliveryType.HasValue)
            return null;

        return deliveryType.Value switch
        {
            0 => DeliveryType.Receiving,
            1 => DeliveryType.Sending,
            _ => null
        };
    }

    /// <summary>
    /// OffsetResultType 映射
    /// </summary>
    public OffsetResultType MapOffsetResultType(int? offsetResult)
    {
        if (!offsetResult.HasValue)
            return OffsetResultType.Default;

        return offsetResult.Value switch
        {
            0 => OffsetResultType.Default,
            1 => OffsetResultType.OverPositiveDeviation,
            2 => OffsetResultType.Normal,
            3 => OffsetResultType.OverNegativeDeviation,
            _ => OffsetResultType.Default
        };
    }

    /// <summary>
    /// 日期时间转换
    /// </summary>
    public DateTime? MapDateTime(string? dateTimeStr)
    {
        if (string.IsNullOrWhiteSpace(dateTimeStr))
            return null;

        if (DateTime.TryParse(dateTimeStr, out var dateTime))
            return dateTime;

        return null;
    }

    /// <summary>
    /// Decimal转换
    /// </summary>
    public decimal? MapDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (decimal.TryParse(value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Int转换
    /// </summary>
    public int? MapInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// 删除状态转换（DeleteStatus: 0=未删除, 1=已删除）
    /// </summary>
    public bool MapDeleteStatus(int deleteStatus)
    {
        return deleteStatus == 1;
    }

    /// <summary>
    /// Bool转换（EarlyWarnStatus等）
    /// </summary>
    public bool MapBool(int? status)
    {
        return status.HasValue && status.Value != 0;
    }
}