using MaterialClient.Common.Entities;
using MaterialClient.Recycle.Models;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     WeighingRecord（+ 关联 Waybill）→ <see cref="RecycleTransportRecord" /> 字段映射。
///     字段来源说明（设计假设，见变更说明）：
///     §2.2 文档以 <c>WeighingRecord.OrderGoodsWeight/OrderTruckWeight/OrderTotalWeight/OrderNo/OutTime</c>
///     描述字段，但这些字段实际位于关联的 <see cref="Waybill" />（<see cref="WeighingRecord.WaybillId" />）。
///     本映射从 Waybill 取数，Waybill 缺失时回退到 WeighingRecord 自身字段。
/// </summary>
public class RecycleWeightMapper : ITransientDependency
{
    private readonly RecycleSyncOptions _options;

    public RecycleWeightMapper(IOptions<RecycleSyncOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    ///     映射为 §2.2 运输记录。
    /// </summary>
    /// <param name="record">称重记录</param>
    /// <param name="waybill">关联运单（可为 null）</param>
    /// <param name="outPhotos">出场照片 Base64（不带标识头，逗号分隔）</param>
    /// <param name="productName">成品名称（§2.2 productName，来自 <see cref="Material.Name"/>）</param>
    public RecycleTransportRecord Map(WeighingRecord record, Waybill? waybill, string outPhotos, string productName)
    {
        // 重量：kg → 吨（÷1000），保持 decimal 精度。
        var netWeightKg = waybill?.OrderGoodsWeight ?? 0m;
        var tareWeightKg = waybill?.OrderTruckWeight;
        var grossWeightKg = waybill?.OrderTotalWeight;

        // 出场时间：优先 Waybill.OutTime，回退 WeighingRecord.AddDate。
        var outTime = waybill?.OutTime ?? record.AddDate;

        // DataNo：优先 Waybill.OrderNo，缺失则按记录 Id 生成唯一标识。
        var dataNo = !string.IsNullOrWhiteSpace(waybill?.OrderNo)
            ? waybill!.OrderNo
            : $"R-{record.Id}";

        // 车牌号：优先 Waybill.PlateNumber，回退 WeighingRecord.PlateNumber。
        var carNo = !string.IsNullOrWhiteSpace(waybill?.PlateNumber)
            ? waybill!.PlateNumber!
            : record.PlateNumber ?? string.Empty;

        // 重量为零或负值时，净重置 0（调用方据此跳过/标记失败，见 RecycleDataSyncService）。
        var netWeightTons = netWeightKg > 0 ? netWeightKg / 1000m : 0m;

        return new RecycleTransportRecord
        {
            DataNo = dataNo,
            PointNumber = _options.PointNumber ?? string.Empty,
            CarNo = carNo,
            ProductName = productName,
            NetWeight = netWeightTons,
            TareWeight = tareWeightKg.HasValue && tareWeightKg.Value > 0 ? tareWeightKg.Value / 1000m : (decimal?)null,
            GrossWeight = grossWeightKg.HasValue && grossWeightKg.Value > 0 ? grossWeightKg.Value / 1000m : (decimal?)null,
            OutTime = outTime.ToString("yyyy-MM-dd HH:mm:ss"),
            OutPhotos = outPhotos
        };
    }
}
