using System;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Models;

/// <summary>
/// 称重列表项统一展示模型
/// </summary>
public class WeighingListItemDto
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }
    
    /// <summary>
    /// 进场时间（不为空，用于排序）
    /// </summary>
    public DateTime JoinTime { get; set; }
    
    /// <summary>
    /// 出场时间
    /// </summary>
    public DateTime? OutTime { get; set; }
    
    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// 数据来源类型
    /// </summary>
    public WeighingListItemType ItemType { get; set; }
    
    /// <summary>
    /// 原始称重记录（当 ItemType == WeighingRecord 时有值）
    /// </summary>
    public WeighingRecord? WeighingRecord { get; set; }
    
    /// <summary>
    /// 原始运单（当 ItemType == Waybill 时有值）
    /// </summary>
    public Waybill? Waybill { get; set; }
    
    /// <summary>
    /// 从 WeighingRecord 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWeighingRecord(WeighingRecord record)
    {
        return new WeighingListItemDto
        {
            Id = record.Id,
            PlateNumber = record.PlateNumber,
            JoinTime = record.CreationTime,
            OutTime = null,
            IsCompleted = false,
            ItemType = WeighingListItemType.WeighingRecord,
            WeighingRecord = record,
            Waybill = null
        };
    }
    
    /// <summary>
    /// 从 Waybill 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWaybill(Waybill waybill)
    {
        return new WeighingListItemDto
        {
            Id = waybill.Id,
            PlateNumber = waybill.PlateNumber,
            JoinTime = waybill.JoinTime ?? waybill.CreationTime,
            OutTime = waybill.OutTime,
            IsCompleted = waybill.OrderType == OrderTypeEnum.Completed,
            ItemType = WeighingListItemType.Waybill,
            WeighingRecord = null,
            Waybill = waybill
        };
    }
}
