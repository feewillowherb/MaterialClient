namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     称重记录某一时刻的字段快照（修改前或修改后）。
/// </summary>
public class EditEntrySnapshot
{
    /// <summary>
    ///     车牌号
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     总重量（kg）
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    ///     异常原因；无异常时为 <c>null</c>
    /// </summary>
    public string? AnomalyReason { get; set; }
}
