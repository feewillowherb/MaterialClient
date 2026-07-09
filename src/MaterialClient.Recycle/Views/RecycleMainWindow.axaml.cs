using Avalonia.Controls;
using MaterialClient.Recycle.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Recycle.Views;

/// <summary>
///     Recycle 客户端主窗口。
///     注：SolidWaste 称重详情 UI（SolidWasteWeighingDetailViewModel / AttendedWeighingWindow）
///     当前位于主程序 MaterialClient 项目中，Recycle 作为独立项目（仅引用 Common + UI）无法直接复用。
///     完整称重界面复用需要将共享称重 ViewModel/Window 迁移到 MaterialClient.UI 共享层（见变更说明）。
///     本窗口作为独立项目下的最小可运行外壳，负责承载同步管线状态。
/// </summary>
public partial class RecycleMainWindow : Window, ITransientDependency
{
    public RecycleMainWindow(RecycleMainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public RecycleMainWindowViewModel? ViewModel => DataContext as RecycleMainWindowViewModel;
}
