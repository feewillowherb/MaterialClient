namespace MaterialClient.Common.Models;

public interface IDateRangeFilter
{
    DateTime? StartDate { get; set; }
    DateTime? EndDate { get; set; }
}

public static class DateRangeFilterExtensions
{
    /// <summary>
    ///     返回用于查询的有效截止日期。
    ///     将 EndDate 向后延一天，以包含截止日期当天的全部数据。
    ///     例如用户选择 2026-05-01，返回 2026-05-02，
    ///     配合 &lt;= 比较即可覆盖 2026-05-01 全天。
    /// </summary>
    public static DateTime? GetEffectiveEndDate(this IDateRangeFilter filter)
    {
        return filter.EndDate?.AddDays(1);
    }
}
