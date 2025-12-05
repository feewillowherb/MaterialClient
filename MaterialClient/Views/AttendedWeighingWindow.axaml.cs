using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AttendedWeighingWindow : Window
{
    private CancellationTokenSource? _closePopupCts;

    public AttendedWeighingWindow(AttendedWeighingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Set PlacementTarget for Popup
        if (CameraStatusPopup != null && CameraStatusPanel != null)
        {
            CameraStatusPopup.PlacementTarget = CameraStatusPanel;
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
        // Delay closing to allow mouse to move to popup
        _closePopupCts?.Cancel();
        _closePopupCts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(200, _closePopupCts.Token);
            if (CameraStatusPopup != null)
            {
                CameraStatusPopup.IsOpen = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Cancelled, mouse moved back or to popup
        }
    }

    private void CameraStatusPopup_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        // Cancel any pending close operation when mouse enters popup
        _closePopupCts?.Cancel();
        _closePopupCts = null;
    }

    private async void CameraStatusPopup_OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Delay closing when mouse leaves popup
        _closePopupCts?.Cancel();
        _closePopupCts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(200, _closePopupCts.Token);
            if (CameraStatusPopup != null)
            {
                CameraStatusPopup.IsOpen = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Cancelled, mouse moved back
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closePopupCts?.Cancel();
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnClosed(e);
    }
}
