using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing
{
public partial class AttendedWeighingWindow : Window, ITransientDependency
{
    private CancellationTokenSource? _closePopupCts;
    private bool _isMouseOverPopup;

    public AttendedWeighingWindow() : this(null)
    {
    }

    public AttendedWeighingWindow(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;
        DataContext = serviceProvider?.GetService(typeof(AttendedWeighingViewModel)) as AttendedWeighingViewModel;

        // Set PlacementTarget for Popup
        if (CameraStatusPopup != null && CameraStatusPanel != null)
            CameraStatusPopup.PlacementTarget = CameraStatusPanel;

        // 窗口打开时启动轮询后台服务和创建 NotificationManager
        Opened += AttendedWeighingWindow_Opened;
    }

    public WindowNotificationManager? NotificationManager { get; private set; }

    private async void AttendedWeighingWindow_Opened(object? sender, EventArgs e)
    {
        // 确保 DataContext 已设置后再初始化
        if (DataContext is AttendedWeighingViewModel viewModel)
        {
            // 延迟初始化，确保窗口和绑定都已建立
            await Task.Delay(100); // 给 UI 绑定一些时间
            await viewModel.InitializeOnFirstLoadAsync();
        }

        // 创建 WindowNotificationManager（窗口打开后才能获取 TopLevel）
        if (NotificationManager == null)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel != null)
                NotificationManager = new WindowNotificationManager(topLevel)
                {
                    Position = NotificationPosition.TopCenter,
                    MaxItems = 1
                };
        }

        // 预热 DetailView：在空闲时创建一次以初始化样式和模板
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 创建一个临时的 DetailView 实例来预热控件模板
                //TODO
            }
            catch (Exception)
            {
                // ignore - prewarm failure should not affect normal usage
            }
        }, DispatcherPriority.Background);
    }

    private void CameraStatusPanel_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        // Cancel any pending close operation
        _closePopupCts?.Cancel();
        _closePopupCts = null;

        if (CameraStatusPopup != null) CameraStatusPopup.IsOpen = true;
    }

    private async void CameraStatusPanel_OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Only start closing timer if popup is open and mouse is not over popup
        if (CameraStatusPopup?.IsOpen == true && !_isMouseOverPopup)
        {
            _closePopupCts?.Cancel();
            _closePopupCts = new CancellationTokenSource();

            try
            {
                // Wait a bit to allow mouse to move to popup
                await Task.Delay(150, _closePopupCts.Token);
                // Only close if mouse is still not over popup
                if (!_isMouseOverPopup && CameraStatusPopup != null) CameraStatusPopup.IsOpen = false;
            }
            catch (TaskCanceledException)
            {
                // Cancelled, mouse moved to popup
            }
        }
    }

    private void CameraStatusPopup_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isMouseOverPopup = true;

        // Cancel any pending close operation when mouse enters popup
        _closePopupCts?.Cancel();
        _closePopupCts = null;
    }

    private async void CameraStatusPopup_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseOverPopup = false;

        // Delay closing when mouse leaves popup
        _closePopupCts?.Cancel();
        _closePopupCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(150, _closePopupCts.Token);
            // Only close if mouse is still not over popup
            if (!_isMouseOverPopup && CameraStatusPopup != null) CameraStatusPopup.IsOpen = false;
        }
        catch (TaskCanceledException)
        {
            // Cancelled, mouse moved back
        }
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnDataManagementClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new DataManagementDialogWindow();
        await dialog.ShowDialog<bool?>(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closePopupCts?.Cancel();

        // 不需要手动停止 PollingBackgroundService
        // ABP 框架会在应用退出时自动停止所有 BackgroundWorker

        if (DataContext is IDisposable disposable) disposable.Dispose();

        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime != null)
        {
            // 只有当不是 MainWindow 时才手动触发退出
            lifetime.Shutdown();
        }

        // 如果是 MainWindow，Avalonia 会自动触发 desktop.Exit，不需要手动处理
        base.OnClosed(e);
    }
}
}