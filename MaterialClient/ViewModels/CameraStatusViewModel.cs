using CommunityToolkit.Mvvm.ComponentModel;

namespace MaterialClient.ViewModels;

/// <summary>
/// 摄像头状态视图模型
/// </summary>
public partial class CameraStatusViewModel : ObservableObject
{
    /// <summary>
    /// 摄像头名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 摄像头IP地址
    /// </summary>
    [ObservableProperty]
    private string _ip = string.Empty;

    /// <summary>
    /// 摄像头端口
    /// </summary>
    [ObservableProperty]
    private string _port = string.Empty;

    /// <summary>
    /// 是否在线
    /// </summary>
    [ObservableProperty]
    private bool _isOnline;

    /// <summary>
    /// 显示地址（IP:Port）
    /// </summary>
    public string DisplayAddress => $"{Ip}:{Port}";

    partial void OnIpChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayAddress));
    }

    partial void OnPortChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayAddress));
    }
}
