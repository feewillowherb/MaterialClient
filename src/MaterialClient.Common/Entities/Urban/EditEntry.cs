namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Represents a complete snapshot of a weighing record at a point in time.
///     Each entry captures the full state after a modification, stored in
///     <see cref="UrbanWeighingExtension" /> <c>ExtraProperties["EditHistory"]</c>.
/// </summary>
public class EditEntry
{
    /// <summary>
    ///     UTC timestamp when the edit occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    ///     License plate number at the time of the snapshot.
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     Total weight (kg) at the time of the snapshot.
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    ///     Anomaly reason at the time of the snapshot, or <c>null</c> if no anomaly.
    /// </summary>
    public string? AnomalyReason { get; set; }
}
