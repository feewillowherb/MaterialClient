namespace MaterialClient.Common.Entities;

/// <summary>
///     固体废料信息接口
///     专门用于向IHasExtraProperties写入固体废料信息
/// </summary>
public interface ISolidWasteInfo
{
    /// <summary>
    ///     设置固废类型
    /// </summary>
    /// <param name="solidWasteType">固废类型（来自SolidWasteTypeConfig）</param>
    void SetSolidWasteType(string? solidWasteType);

    /// <summary>
    ///     获取固废类型
    /// </summary>
    /// <returns>固废类型，如果未设置则返回null</returns>
    string? GetSolidWasteType();

    /// <summary>
    ///     设置街道
    /// </summary>
    /// <param name="street">街道（来自StreetsConfig）</param>
    void SetStreet(string? street);

    /// <summary>
    ///     获取街道
    /// </summary>
    /// <returns>街道，如果未设置则返回null</returns>
    string? GetStreet();

    /// <summary>
    ///     设置联单编号
    /// </summary>
    /// <param name="solidWasteOrderNumber">联单编号（小于100字符的字符串）</param>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    void SetSolidWasteOrderNumber(string? solidWasteOrderNumber);

    /// <summary>
    ///     获取联单编号
    /// </summary>
    /// <returns>联单编号，如果未设置则返回null</returns>
    string? GetSolidWasteOrderNumber();

    /// <summary>
    ///     设置发货单位
    /// </summary>
    /// <param name="shipper">发货单位，如果为null则使用默认值"固废资源化综合体"</param>
    void SetShipper(string? shipper = null);

    /// <summary>
    ///     获取发货单位
    /// </summary>
    /// <returns>发货单位，如果未设置则返回默认值"固废资源化综合体"</returns>
    string GetShipper();

    /// <summary>
    ///     设置所有固体废料信息
    /// </summary>
    /// <param name="solidWasteType">固废类型</param>
    /// <param name="street">街道</param>
    /// <param name="solidWasteOrderNumber">联单编号</param>
    /// <param name="shipper">发货单位，如果为null则使用默认值"固废资源化综合体"</param>
    /// <exception cref="ArgumentException">当联单编号超过100字符时抛出</exception>
    void SetSolidWasteInfo(string? solidWasteType, string? street, string? solidWasteOrderNumber, string? shipper = null);
}
