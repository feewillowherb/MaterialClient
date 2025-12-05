using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AttendedWeighingWindow : Window
{
    private CancellationTokenSource? _closePopupCts;
    private bool _isMouseOverPopup;

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

    private void TestDetailWindow_Click(object? sender, RoutedEventArgs e)
    {
        var detailWindow = new DetailWindow();
        detailWindow.Show();
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
