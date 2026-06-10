namespace MaterialClient.Common.Dtos.Urban;

/// <summary>
///     Paged list query input for Urban attended weighing records.
/// </summary>
public class GetUrbanWeighingListInput
{
    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    /// <summary>
    ///     Tab filter: 全部 / 正常 / 异常.
    /// </summary>
    public string? TabFilter { get; init; }

    public string? SearchText { get; init; }

    public DateTime? StartTime { get; init; }

    public DateTime? EndTime { get; init; }
}
