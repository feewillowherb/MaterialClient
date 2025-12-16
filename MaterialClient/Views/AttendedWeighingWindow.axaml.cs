using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;
using MaterialClient.Backgrounds;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Views;

public partial class AttendedWeighingWindow : Window
{
    private CancellationTokenSource? _closePopupCts;
    private bool _isMouseOverPopup;
    private readonly IServiceProvider? _serviceProvider;

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
        
        // 窗口打开时启动轮询后台服务
        Opened += AttendedWeighingWindow_Opened;
    }
    
    private async void AttendedWeighingWindow_Opened(object? sender, EventArgs e)
    {
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
                System.Diagnostics.Debug.WriteLine($"启动轮询后台服务失败: {ex.Message}");
            }
        }
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

    protected override async void OnClosed(EventArgs e)
    {
        _closePopupCts?.Cancel();
        
        // 窗口关闭时停止轮询后台服务
        if (_serviceProvider != null)
        {
            try
            {
                var pollingService = _serviceProvider.GetService<PollingBackgroundService>();
                if (pollingService != null)
                {
                    await pollingService.StopAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止轮询后台服务失败: {ex.Message}");
            }
        }
        
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnClosed(e);
    }
}
