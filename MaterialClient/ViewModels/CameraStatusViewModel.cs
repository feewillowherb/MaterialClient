using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
/// 摄像头状态视图模型
/// </summary>
public partial class CameraStatusViewModel : ReactiveObject
{
    /// <summary>
    /// 摄像头名称
    /// </summary>
    [Reactive]
    private string _name = string.Empty;

    /// <summary>
    /// 摄像头IP地址
    /// </summary>
    [Reactive]
    private string _ip = string.Empty;

    /// <summary>
    /// 摄像头端口
    /// </summary>
    [Reactive]
    private string _port = string.Empty;

    /// <summary>
    /// 是否在线
    /// </summary>
    [Reactive]
    private bool _isOnline;

    /// <summary>
    /// 显示地址（IP:Port）
    /// </summary>
    public string DisplayAddress => $"{Ip}:{Port}";

    partial void OnIpChanged(string value)
    {
        this.RaisePropertyChanged(nameof(DisplayAddress));
    }

    partial void OnPortChanged(string value)
    {
        this.RaisePropertyChanged(nameof(DisplayAddress));
    }
}
