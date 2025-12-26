using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using MaterialClient.Backgrounds;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Views.AttendedWeighing;

public partial class AttendedWeighingWindow : Window
{
    private WindowNotificationManager? _notificationManager;
    private CancellationTokenSource? _closePopupCts;
    private bool _isMouseOverPopup;
    private readonly IServiceProvider? _serviceProvider;
    private AttendedWeighingDetailView? _warmupDetailView;

    public WindowNotificationManager? NotificationManager => _notificationManager;

    public AttendedWeighingWindow(AttendedWeighingViewModel viewModel, IServiceProvider? serviceProvider = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _serviceProvider = serviceProvider;

        // Set PlacementTarget for Popup
        if (CameraStatusPopup != null && CameraStatusPanel != null)
        {
            CameraStatusPopup.PlacementTarget = CameraStatusPanel;
        }

        // 窗口打开时启动轮询后台服务和创建 NotificationManager
        Opened += AttendedWeighingWindow_Opened;
    }

    private async void AttendedWeighingWindow_Opened(object? sender, EventArgs e)
    {
        // 创建 WindowNotificationManager（窗口打开后才能获取 TopLevel）
        if (_notificationManager == null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                _notificationManager = new WindowNotificationManager(topLevel)
                {
                    Position = NotificationPosition.TopCenter,
                    MaxItems = 3
                };
            }
        }

        if (_serviceProvider != null)
        {
            try
            {
                var pollingService = _serviceProvider.GetService<PollingBackgroundService>();
                if (pollingService != null)
                {
                    await pollingService.StartAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILogger<AttendedWeighingWindow>>();
                logger?.LogError(ex, "启动轮询后台服务失败");
            }
        }

        // 预热 DetailView：在空闲时创建一次以初始化样式和模板
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 创建一个临时的 DetailView 实例来预热控件模板
                _warmupDetailView = new AttendedWeighingDetailView();
                // 不需要设置 DataContext，只是为了触发控件和样式的初始化
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILogger<AttendedWeighingWindow>>();
                logger?.LogWarning(ex, "预热 DetailView 失败，不影响正常使用");
            }
        }, DispatcherPriority.Background);
    }

    private void CameraStatusPanel_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        // Cancel any pending close operation
        _closePopupCts?.Cancel();
        _closePopupCts = null;

        if (CameraStatusPopup != null)
        {
            CameraStatusPopup.IsOpen = true;
        }
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
                if (!_isMouseOverPopup && CameraStatusPopup != null)
                {
                    CameraStatusPopup.IsOpen = false;
                }
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
            if (!_isMouseOverPopup && CameraStatusPopup != null)
            {
                CameraStatusPopup.IsOpen = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Cancelled, mouse moved back
        }
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closePopupCts?.Cancel();

        // 不需要手动停止 PollingBackgroundService
        // ABP 框架会在应用退出时自动停止所有 BackgroundWorker

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}