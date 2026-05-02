using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Models;

public class StandardExportFilter : IDateRangeFilter
{
    public WeighingMode WeighingMode { get; set; }

    /// <summary>
    ///     JoinTime 起始日期
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    ///     JoinTime 截止日期
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    ///     车牌号（模糊匹配）
    /// </summary>
    public string? PlateNumber { get; set; }

    /// <summary>
    ///     材料名称（模糊匹配，匹配 Material.Name）
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    ///     称重类型（收料/发料），null 表示不过滤
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }

    /// <summary>
    ///     单据状态，null 表示不过滤
    /// </summary>
    public OrderTypeEnum? OrderType { get; set; }
}
