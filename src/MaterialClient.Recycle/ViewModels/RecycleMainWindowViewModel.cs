using MaterialClient.Recycle.Models;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Recycle.ViewModels;

/// <summary>
///     Recycle 主窗口 ViewModel（最小可运行外壳）。
/// </summary>
public partial class RecycleMainWindowViewModel : ViewModelBase, ITransientDependency
{
    private readonly RecycleSyncOptions _options;

    public RecycleMainWindowViewModel(
        IOptions<RecycleSyncOptions> options,
        ILogger<RecycleMainWindowViewModel>? logger = null)
        : base(logger)
    {
        _options = options.Value;

        var enabled = _options.Enabled ? "已启用" : "已停用";
        StatusLine = $"数据上报管线：{enabled}　|　厂标识：{_options.PointNumber ?? "(未配置)"}　|　成品：取自称重物料 Material.Name";
        SyncHint = "本客户端将定时扫描未上报的称重记录，按 §2.2 接口要求（HMAC-SHA256 签名、图片 Base64 内嵌、重量 kg→吨、JSON Array 批量提交）直连资源化利用厂平台。";
        FooterLine = $"轮询间隔 {_options.PollIntervalSeconds}s　|　最大重试 {_options.MaxFailCount} 次　|　超时 {_options.TimeoutSeconds}s";
    }

    [Reactive] private string _statusLine = string.Empty;
    [Reactive] private string _syncHint = string.Empty;
    [Reactive] private string _footerLine = string.Empty;
}
