using System.Text.Json;
using Volo.Abp.Data;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Extension methods for accessing edit history stored in
///     <see cref="UrbanWeighingExtension" /> <c>ExtraProperties["EditHistory"]</c>.
///     Follows the same pattern as <see cref="SolidWasteInfoExtensions" />.
/// </summary>
public static class EditHistoryExtensions
{
    private const string EditHistoryKey = "EditHistory";

    /// <summary>
    ///     Reads edit history from <see cref="UrbanWeighingExtension.ExtraProperties" />.
    ///     Returns an empty list if the key is missing or deserialization fails.
    /// </summary>
    public static List<EditEntry> GetEditHistory(this UrbanWeighingExtension ext)
    {
        if (ext == null) throw new ArgumentNullException(nameof(ext));

        var json = ext.GetProperty<string>(EditHistoryKey);
        if (string.IsNullOrEmpty(json))
            return new List<EditEntry>();

        try
        {
            var entries = JsonSerializer.Deserialize<List<EditEntry>>(json) ?? new List<EditEntry>();
            return NormalizeEntries(entries);
        }
        catch
        {
            return new List<EditEntry>();
        }
    }

    /// <summary>
    ///     Writes edit history to <see cref="UrbanWeighingExtension.ExtraProperties" />.
    ///     Removes the key when <paramref name="entries" /> is <c>null</c> or empty.
    /// </summary>
    public static void SetEditHistory(this UrbanWeighingExtension ext, List<EditEntry>? entries)
    {
        if (ext == null) throw new ArgumentNullException(nameof(ext));

        if (entries == null || entries.Count == 0)
        {
            ext.ExtraProperties.Remove(EditHistoryKey);
        }
        else
        {
            var normalized = NormalizeEntries(entries);
            ext.SetProperty(EditHistoryKey, JsonSerializer.Serialize(normalized));
        }
    }

    private static List<EditEntry> NormalizeEntries(IEnumerable<EditEntry> entries) =>
        entries
            .Select(entry => new EditEntry
            {
                ChangedAt = NormalizeChangedAt(entry.ChangedAt),
                Before = entry.Before,
                After = entry.After,
                Source = entry.Source,
                IsImagesModified = entry.IsImagesModified
            })
            .ToList();

    /// <summary>
    ///     修改历史统一按本地时间存储；兼容旧版 UTC（Z）写入。
    /// </summary>
    private static DateTime NormalizeChangedAt(DateTime changedAt)
    {
        if (changedAt == default)
        {
            return changedAt;
        }

        return changedAt.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(changedAt.ToLocalTime(), DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(changedAt, DateTimeKind.Unspecified),
            _ => changedAt
        };
    }
}
