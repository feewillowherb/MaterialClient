using System;
using Volo.Abp.Data;

namespace MaterialClient.Common.Entities;

/// <summary>
///     固体废料信息扩展方法
///     为WeighingRecord和Waybill提供ISolidWasteInfo接口的实现
///     这些实体通过Entity&lt;T&gt;基类实现IHasExtraProperties
/// </summary>

/// <summary>
///     固体废料信息扩展方法
///     为WeighingRecord和Waybill提供ISolidWasteInfo接口的实现
/// </summary>
public static class SolidWasteInfoExtensions
{
    /// <summary>
    ///     默认发货单位
    /// </summary>
    private const string DefaultShipper = "固废资源化综合体";

    /// <summary>
    ///     联单编号最大长度
    /// </summary>
    private const int MaxSolidWasteOrderNumberLength = 100;

    #region Property Keys

    private const string SolidWasteTypeKey = "SolidWasteInfo.SolidWasteType";
    private const string StreetKey = "SolidWasteInfo.Street";
    private const string SolidWasteOrderNumberKey = "SolidWasteInfo.SolidWasteOrderNumber";
    private const string ShipperKey = "SolidWasteInfo.Shipper";

    #endregion

    #region WeighingRecord Extensions

    /// <summary>
    ///     设置固废类型
    /// </summary>
    public static void SetSolidWasteType(this WeighingRecord record, string? solidWasteType)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        record.SetProperty(SolidWasteTypeKey, solidWasteType);
    }

    /// <summary>
    ///     获取固废类型
    /// </summary>
    public static string? GetSolidWasteType(this WeighingRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return record.GetProperty<string>(SolidWasteTypeKey);
    }

    /// <summary>
    ///     设置街道
    /// </summary>
    public static void SetStreet(this WeighingRecord record, string? street)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        record.SetProperty(StreetKey, street);
    }

    /// <summary>
    ///     获取街道
    /// </summary>
    public static string? GetStreet(this WeighingRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return record.GetProperty<string>(StreetKey);
    }

    /// <summary>
    ///     设置联单编号
    /// </summary>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    public static void SetSolidWasteOrderNumber(this WeighingRecord record, string? solidWasteOrderNumber)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        
        if (solidWasteOrderNumber != null && solidWasteOrderNumber.Length > MaxSolidWasteOrderNumberLength)
        {
            throw new ArgumentException(
                $"联单编号长度不能超过{MaxSolidWasteOrderNumberLength}字符，当前长度为{solidWasteOrderNumber.Length}。",
                nameof(solidWasteOrderNumber));
        }

        record.SetProperty(SolidWasteOrderNumberKey, solidWasteOrderNumber);
    }

    /// <summary>
    ///     获取联单编号
    /// </summary>
    public static string? GetSolidWasteOrderNumber(this WeighingRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return record.GetProperty<string>(SolidWasteOrderNumberKey);
    }

    /// <summary>
    ///     设置发货单位
    /// </summary>
    public static void SetShipper(this WeighingRecord record, string? shipper = null)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        record.SetProperty(ShipperKey, shipper ?? DefaultShipper);
    }

    /// <summary>
    ///     获取发货单位
    /// </summary>
    public static string GetShipper(this WeighingRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return record.GetProperty<string>(ShipperKey) ?? DefaultShipper;
    }

    /// <summary>
    ///     设置所有固体废料信息
    /// </summary>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    public static void SetSolidWasteInfo(
        this WeighingRecord record,
        string? solidWasteType,
        string? street,
        string? solidWasteOrderNumber,
        string? shipper = null)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        record.SetSolidWasteType(solidWasteType);
        record.SetStreet(street);
        record.SetSolidWasteOrderNumber(solidWasteOrderNumber);
        record.SetShipper(shipper);
    }

    #endregion

    #region Waybill Extensions

    /// <summary>
    ///     设置固废类型
    /// </summary>
    public static void SetSolidWasteType(this Waybill waybill, string? solidWasteType)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        waybill.SetProperty(SolidWasteTypeKey, solidWasteType);
    }

    /// <summary>
    ///     获取固废类型
    /// </summary>
    public static string? GetSolidWasteType(this Waybill waybill)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        return waybill.GetProperty<string>(SolidWasteTypeKey);
    }

    /// <summary>
    ///     设置街道
    /// </summary>
    public static void SetStreet(this Waybill waybill, string? street)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        waybill.SetProperty(StreetKey, street);
    }

    /// <summary>
    ///     获取街道
    /// </summary>
    public static string? GetStreet(this Waybill waybill)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        return waybill.GetProperty<string>(StreetKey);
    }

    /// <summary>
    ///     设置联单编号
    /// </summary>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    public static void SetSolidWasteOrderNumber(this Waybill waybill, string? solidWasteOrderNumber)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        
        if (solidWasteOrderNumber != null && solidWasteOrderNumber.Length > MaxSolidWasteOrderNumberLength)
        {
            throw new ArgumentException(
                $"联单编号长度不能超过{MaxSolidWasteOrderNumberLength}字符，当前长度为{solidWasteOrderNumber.Length}。",
                nameof(solidWasteOrderNumber));
        }

        waybill.SetProperty(SolidWasteOrderNumberKey, solidWasteOrderNumber);
    }

    /// <summary>
    ///     获取联单编号
    /// </summary>
    public static string? GetSolidWasteOrderNumber(this Waybill waybill)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        return waybill.GetProperty<string>(SolidWasteOrderNumberKey);
    }

    /// <summary>
    ///     设置发货单位
    /// </summary>
    public static void SetShipper(this Waybill waybill, string? shipper = null)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        waybill.SetProperty(ShipperKey, shipper ?? DefaultShipper);
    }

    /// <summary>
    ///     获取发货单位
    /// </summary>
    public static string GetShipper(this Waybill waybill)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));
        return waybill.GetProperty<string>(ShipperKey) ?? DefaultShipper;
    }

    /// <summary>
    ///     设置所有固体废料信息
    /// </summary>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    public static void SetSolidWasteInfo(
        this Waybill waybill,
        string? solidWasteType,
        string? street,
        string? solidWasteOrderNumber,
        string? shipper = null)
    {
        if (waybill == null) throw new ArgumentNullException(nameof(waybill));

        waybill.SetSolidWasteType(solidWasteType);
        waybill.SetStreet(street);
        waybill.SetSolidWasteOrderNumber(solidWasteOrderNumber);
        waybill.SetShipper(shipper);
    }

    #endregion
}
