using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 供应商ID
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// 物料ID（兼容旧代码，取第一个物料的ID）
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 物料单位ID（兼容旧代码，取第一个物料的单位ID）
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    /// 收发料类型
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }

    /// <summary>
    /// 重量（称重记录的重量，或运单的总重量）
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// 订单号（仅运单有值）
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 运单数量（兼容旧代码，取第一个物料的数量）
    /// </summary>
    public decimal? WaybillQuantity { get; set; }


    public string? Operator { get; set; }

    /// <summary>
    /// 物料列表（支持多物料）
    /// </summary>
    public List<WeighingListItemMaterialDto> Materials { get; set; } = new();

    /// <summary>
    /// 从 WeighingRecord 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWeighingRecord(WeighingRecord record)
    {
        var materials = record.Materials ?? new List<WeighingRecordMaterial>();
        var firstMaterial = materials.FirstOrDefault();
        
        return new WeighingListItemDto
        {
            Id = record.Id,
            PlateNumber = record.PlateNumber,
            JoinTime = record.CreationTime,
            OutTime = null,
            IsCompleted = false,
            ItemType = WeighingListItemType.WeighingRecord,
            ProviderId = record.ProviderId,
            MaterialId = firstMaterial?.MaterialId,
            MaterialUnitId = firstMaterial?.MaterialUnitId,
            DeliveryType = record.DeliveryType,
            Weight = record.TotalWeight,
            OrderNo = null,
            WaybillQuantity = firstMaterial?.WaybillQuantity,
            Materials = materials.Select(m => new WeighingListItemMaterialDto
            {
                MaterialId = m.MaterialId,
                MaterialUnitId = m.MaterialUnitId,
                Weight = m.Weight,
                WaybillQuantity = m.WaybillQuantity
            }).ToList()
        };
    }

    /// <summary>
    /// 从 Waybill 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWaybill(Waybill waybill)
    {
        var dto = new WeighingListItemDto
        {
            Id = waybill.Id,
            PlateNumber = waybill.PlateNumber,
            JoinTime = waybill.JoinTime ?? waybill.CreationTime,
            OutTime = waybill.OutTime,
            IsCompleted = waybill.OrderType == OrderTypeEnum.Completed,
            ItemType = WeighingListItemType.Waybill,
            ProviderId = waybill.ProviderId,
            MaterialId = waybill.MaterialId,
            MaterialUnitId = waybill.MaterialUnitId,
            DeliveryType = waybill.DeliveryType,
            Weight = waybill.OrderTotalWeight,
            OrderNo = waybill.OrderNo,
            WaybillQuantity = waybill.OrderPlanOnPcs
        };
        
        // 如果有物料信息，添加到 Materials 列表
        if (waybill.MaterialId.HasValue)
        {
            dto.Materials.Add(new WeighingListItemMaterialDto
            {
                MaterialId = waybill.MaterialId,
                MaterialUnitId = waybill.MaterialUnitId,
                Weight = waybill.OrderGoodsWeight,
                WaybillQuantity = waybill.OrderPlanOnPcs
            });
        }
        
        return dto;
    }
}

/// <summary>
/// 称重列表项物料 DTO
/// </summary>
public class WeighingListItemMaterialDto
{
    /// <summary>
    /// 物料ID
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 物料单位ID
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    /// 重量
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// 运单数量
    /// </summary>
    public decimal? WaybillQuantity { get; set; }
}