namespace MaterialClient.UI.Models;

/// <summary>
///     Per-camera status row for the camera hover detail popup.
/// </summary>
public record CameraStatusDetailItem(string Name, string Ip, string Port, bool IsOnline)
{
    public string DisplayAddress => string.IsNullOrWhiteSpace(Port) ? Ip : $"{Ip}:{Port}";
}
