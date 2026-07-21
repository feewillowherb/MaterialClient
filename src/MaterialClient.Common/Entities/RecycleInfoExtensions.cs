using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Data;

namespace MaterialClient.Common.Entities;

/// <summary>
///     Recycle 运单前暂存字段的解析结果（join 优先、out fallback）。
/// </summary>
/// <param name="UnitPrice">单价（元/吨）</param>
/// <param name="SaleContractNo">销售合同编号</param>
public record RecycleInfoValues(decimal? UnitPrice, string? SaleContractNo)
{
    /// <summary>
    ///     是否至少有一个非空值。
    /// </summary>
    public bool HasAnyValue =>
        UnitPrice.HasValue || !string.IsNullOrWhiteSpace(SaleContractNo);
}

/// <summary>
///     Recycle 运单前暂存字段扩展方法。
///     在 <see cref="WeighingRecord.ExtraProperties" /> 中读写单价与合同号，
///     风格对齐 <see cref="SolidWasteInfoExtensions" />。
///     Waybill 侧对应字段仍存 <see cref="RecycleWaybillExtension" />，不使用本扩展。
/// </summary>
public static class RecycleInfoExtensions
{
    #region Property Keys

    private const string UnitPriceKey = "RecycleInfo.UnitPrice";
    private const string SaleContractNoKey = "RecycleInfo.SaleContractNo";

    #endregion

    #region WeighingRecord Extensions

    /// <summary>
    ///     设置单价（元/吨，可选）。
    /// </summary>
    public static void SetUnitPrice(this WeighingRecord record, decimal? unitPrice)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.SetProperty(UnitPriceKey, unitPrice);
    }

    /// <summary>
    ///     获取单价（元/吨）。
    /// </summary>
    public static decimal? GetUnitPrice(this WeighingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.GetProperty<decimal?>(UnitPriceKey);
    }

    /// <summary>
    ///     设置销售合同编号（可选）。
    /// </summary>
    public static void SetSaleContractNo(this WeighingRecord record, string? saleContractNo)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.SetProperty(
            SaleContractNoKey,
            string.IsNullOrWhiteSpace(saleContractNo) ? null : saleContractNo);
    }

    /// <summary>
    ///     获取销售合同编号。
    /// </summary>
    public static string? GetSaleContractNo(this WeighingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.GetProperty<string>(SaleContractNoKey);
    }

    /// <summary>
    ///     批量设置 Recycle 运单前暂存字段。
    /// </summary>
    public static void SetRecycleInfo(
        this WeighingRecord record,
        decimal? unitPrice,
        string? saleContractNo)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.SetUnitPrice(unitPrice);
        record.SetSaleContractNo(saleContractNo);
    }

    /// <summary>
    ///     从匹配的 join/out 记录解析 Recycle 暂存字段（join 优先、缺失 fallback 到 out）。
    ///     两侧均非 Recycle 时返回 null。
    /// </summary>
    public static RecycleInfoValues? ResolveFromWeighingRecords(
        WeighingRecord joinRecord,
        WeighingRecord outRecord)
    {
        ArgumentNullException.ThrowIfNull(joinRecord);
        ArgumentNullException.ThrowIfNull(outRecord);

        if (joinRecord.WeighingMode != WeighingMode.Recycle &&
            outRecord.WeighingMode != WeighingMode.Recycle)
        {
            return null;
        }

        var primary = joinRecord.WeighingMode == WeighingMode.Recycle ? joinRecord : outRecord;
        var fallback = ReferenceEquals(primary, joinRecord) ? outRecord : joinRecord;

        var unitPrice = primary.GetUnitPrice();
        var saleContractNo = primary.GetSaleContractNo();

        if (fallback.WeighingMode == WeighingMode.Recycle)
        {
            unitPrice ??= fallback.GetUnitPrice();
            saleContractNo ??= fallback.GetSaleContractNo();
        }

        return new RecycleInfoValues(unitPrice, saleContractNo);
    }

    #endregion
}
