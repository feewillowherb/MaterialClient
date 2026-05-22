namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     Waybill void scope enum - determines which weighing records to void
/// </summary>
public enum WaybillVoidScope
{
    /// <summary>
    ///     Void join (inbound) weighing record only
    /// </summary>
    JoinOnly = 0,

    /// <summary>
    ///     Void out (outbound) weighing record only
    /// </summary>
    OutOnly = 1,

    /// <summary>
    ///     Void both join and out weighing records
    /// </summary>
    Both = 2
}
