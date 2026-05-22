using Avalonia.Media;

namespace MaterialClient.UI.Models;

/// <summary>
///     Data model representing a single device status entry in the status bar.
/// </summary>
public record DeviceStatusItem(string Name, bool IsOnline)
{
    public string StatusText => IsOnline ? "在线" : "离线";

    /// <summary>
    ///     Brush for status indicator (Avalonia cannot bind hex strings to Color directly in all templates).
    /// </summary>
    public IBrush StatusBrush => new SolidColorBrush(IsOnline
        ? Color.Parse("#22C55E")
        : Color.Parse("#EF4444"));
}
