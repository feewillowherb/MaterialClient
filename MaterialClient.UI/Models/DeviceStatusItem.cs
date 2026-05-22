namespace MaterialClient.UI.Models;

/// <summary>
///     Data model representing a single device status entry in the status bar.
/// </summary>
public record DeviceStatusItem(string Name, bool IsOnline)
{
    public string StatusText => IsOnline ? "在线" : "离线";
    public string StatusColor => IsOnline ? "#22C55E" : "#EF4444";
}
