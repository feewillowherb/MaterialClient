namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Represents a single edit entry in the modification history of a weighing record.
///     Serialized as part of the JSON array stored in <see cref="UrbanWeighingExtension.EditHistoryJson" />.
/// </summary>
public class EditEntry
{
    /// <summary>
    ///     Name of the field that was modified (e.g. "PlateNumber", "TotalWeight").
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    ///     Value before the edit.
    /// </summary>
    public string OldValue { get; set; } = string.Empty;

    /// <summary>
    ///     Value after the edit.
    /// </summary>
    public string NewValue { get; set; } = string.Empty;

    /// <summary>
    ///     UTC timestamp when the edit occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
