using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Models;

/// <summary>
///     称重列表项统一展示模型
/// </summary>
public class WeighingListItemDto
{
    /// <summary>
    ///     记录ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     车牌号
    /// </summary>
    public string? PlateNumber { get; set; }

    /// <summary>
    ///     进场时间（不为空，用于排序）
    /// </summary>
    public DateTime JoinTime { get; set; }

    /// <summary>
    ///     出场时间
    /// </summary>
    public DateTime? OutTime { get; set; }

    /// <summary>
    ///     是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    ///     数据来源类型
    /// </summary>
    public WeighingListItemType ItemType { get; set; }

    /// <summary>
    ///     供应商ID
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    ///     物料ID（兼容旧代码，取第一个物料的ID）
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    ///     物料单位ID（兼容旧代码，取第一个物料的单位ID）
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    ///     收发料类型
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }

    /// <summary>
    ///     重量（称重记录的重量，或运单的总重量）
    /// </summary>
    public decimal? Weight { get; set; }


    /// <summary>
    ///     皮重
    /// </summary>
    public decimal? TruckWeight { get; set; }

    /// <summary>
    ///     订单号（仅运单有值）
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    ///     运单数量（兼容旧代码，取第一个物料的数量）
    /// </summary>
    public decimal? WaybillQuantity { get; set; }


    /// <summary>
    ///     操作员
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    ///     备注（仅运单有值）
    /// </summary>
    public string? Remark { get; set; }


    /// <summary>
    ///     订单类型
    /// </summary>
    public OrderTypeEnum? OrderType { get; set; }

    /// <summary>
    ///     进场重量（预计算）
    /// </summary>
    public decimal? JoinWeight { get; set; }

    /// <summary>
    ///     出场重量（预计算）
    /// </summary>
    public decimal? OutWeight { get; set; }

    /// <summary>
    ///     供应商名称（预计算）
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    ///     物料信息（预计算，格式：{Rate}/{Unit} {MaterialName}）
    /// </summary>
    public string? MaterialInfo { get; set; }

    /// <summary>
    ///     偏差率（预计算，百分比格式）
    /// </summary>
    public string? OffsetInfo { get; set; }

    /// <summary>
    ///     物料列表（支持多物料）
    /// </summary>
    public List<WeighingListItemMaterialDto> Materials { get; set; } = new();

    /// <summary>
    ///     从 WeighingRecord 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWeighingRecord(WeighingRecord record)
    {
        return FromWeighingRecord(record, null, null);
    }

    /// <summary>
    ///     从 WeighingRecord 创建 DTO（带 Material 和 MaterialUnit 信息）
    /// </summary>
    public static WeighingListItemDto FromWeighingRecord(
        WeighingRecord record,
        Dictionary<int, Material>? materialsDict,
        Dictionary<int, MaterialUnit>? materialUnitsDict)
    {
        var materials = record.Materials;
        var firstMaterial = materials.FirstOrDefault();

        return new WeighingListItemDto
        {
            Id = record.Id,
            PlateNumber = record.PlateNumber,
            JoinTime = record.AddDate,
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
            Materials = materials.Select(m =>
            {
                var dto = new WeighingListItemMaterialDto
                {
                    MaterialId = m.MaterialId,
                    MaterialUnitId = m.MaterialUnitId,
                    Weight = m.Weight,
                    WaybillQuantity = m.WaybillQuantity
                };

                // 填充渲染字段
                if (m.MaterialId.HasValue && materialsDict != null &&
                    materialsDict.TryGetValue(m.MaterialId.Value, out var material))
                    dto.MaterialName = material.Name;

                if (m.MaterialUnitId.HasValue && materialUnitsDict != null &&
                    materialUnitsDict.TryGetValue(m.MaterialUnitId.Value, out var materialUnit))
                {
                    dto.MaterialUnitName = materialUnit.UnitName;
                    dto.MaterialUnitRate = materialUnit.Rate ?? 0m;
                }

                return dto;
            }).ToList()
        };
    }

    /// <summary>
    ///     从 Waybill 创建 DTO
    /// </summary>
    public static WeighingListItemDto FromWaybill(Waybill waybill)
    {
        return FromWaybill(waybill, null, null);
    }

    /// <summary>
    ///     从 Waybill 创建 DTO（带 Material 和 MaterialUnit 信息）
    /// </summary>
    public static WeighingListItemDto FromWaybill(
        Waybill waybill,
        Dictionary<int, Material>? materialsDict,
        Dictionary<int, MaterialUnit>? materialUnitsDict)
    {
        var dto = new WeighingListItemDto
        {
            Id = waybill.Id,
            PlateNumber = waybill.PlateNumber,
            JoinTime = waybill.JoinTime ?? waybill.AddDate,
            OutTime = waybill.OutTime,
            IsCompleted = waybill.OrderType == OrderTypeEnum.Completed,
            ItemType = WeighingListItemType.Waybill,
            ProviderId = waybill.ProviderId,
            MaterialId = waybill.MaterialId,
            MaterialUnitId = waybill.MaterialUnitId,
            DeliveryType = waybill.DeliveryType,
            Weight = waybill.OrderTotalWeight,
            TruckWeight = waybill.OrderTruckWeight,
            OrderNo = waybill.OrderNo,
            WaybillQuantity = waybill.OrderPlanOnPcs,
            OrderType = waybill.OrderType,
            Remark = waybill.Remark,
            // 预计算偏差信息
            OffsetInfo = $"{waybill.OffsetRate:F2}%"
        };

        // 如果有物料信息，添加到 Materials 列表
        if (waybill.MaterialId.HasValue)
        {
            var materialDto = new WeighingListItemMaterialDto
            {
                MaterialId = waybill.MaterialId,
                MaterialUnitId = waybill.MaterialUnitId,
                Weight = waybill.OrderGoodsWeight,
                WaybillQuantity = waybill.OrderPlanOnPcs
            };

            // 填充渲染字段
            if (materialsDict != null && materialsDict.TryGetValue(waybill.MaterialId.Value, out var material))
                materialDto.MaterialName = material.Name;

            if (waybill.MaterialUnitId.HasValue && materialUnitsDict != null &&
                materialUnitsDict.TryGetValue(waybill.MaterialUnitId.Value, out var materialUnit))
            {
                materialDto.MaterialUnitName = materialUnit.UnitName;
                materialDto.MaterialUnitRate = materialUnit.Rate ?? 0m;
            }

            dto.Materials.Add(materialDto);
        }

        return dto;
    }
}

/// <summary>
///     称重列表项物料 DTO
/// </summary>
public class WeighingListItemMaterialDto
{
    /// <summary>
    ///     物料ID
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    ///     物料单位ID
    /// </summary>
    public int? MaterialUnitId { get; set; }

    /// <summary>
    ///     重量
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    ///     运单数量
    /// </summary>
    public decimal? WaybillQuantity { get; set; }

    /// <summary>
    ///     物料名称（用于渲染，避免查询）
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    ///     物料单位名称（用于渲染，避免查询）
    /// </summary>
    public string? MaterialUnitName { get; set; }

    /// <summary>
    ///     物料单位换算率（用于渲染，避免查询）
    /// </summary>
    public decimal? MaterialUnitRate { get; set; }

    /// <summary>
    ///     物料单位显示名称（格式：Rate/UnitName，用于渲染，避免查询）
    /// </summary>
    public string MaterialUnitDisplayName =>
        string.IsNullOrEmpty(MaterialUnitName)
            ? string.Empty
            : $"{MaterialUnitRate ?? 0:F2}/{MaterialUnitName}";
}