using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Models;

public class SolidWasteExportFilter
{
    /// <summary>
    ///     AddDate 起始日期
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    ///     AddDate 截止日期
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    ///     车牌号（模糊匹配）
    /// </summary>
    public string? PlateNumber { get; set; }

    /// <summary>
    ///     货名（模糊匹配，匹配 Material.Name）
    /// </summary>
    public string? GoodsName { get; set; }

    /// <summary>
    ///     发货单位（模糊匹配，匹配 Provider.ProviderName）
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    ///     称重类型（收料/发料），null 表示不过滤
    /// </summary>
    public DeliveryType? WeighingType { get; set; }
}
