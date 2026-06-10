namespace MaterialClient.Demo.Models;

public class DeviceStatus
{
    public string DeviceName { get; init; } = "";
    public bool IsOnline { get; init; }
    public string StatusText => IsOnline ? "在线" : "离线";
    public string StatusColor => IsOnline ? "#4ADE80" : "#EF4444";
    public string DotColor => IsOnline ? "#4ADE80" : "#EF4444";
}
