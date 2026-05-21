using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;

namespace MaterialClient.Urban.Views;

public partial class UrbanAttendedWeighingWindow : Window
{
    /// <summary>
    ///     Creates the window (for ABP DI mode - ViewModel resolved from container)
    /// </summary>
    public UrbanAttendedWeighingWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Creates the window with a pre-configured ViewModel (for DI mode)
    /// </summary>
    public UrbanAttendedWeighingWindow(UrbanAttendedWeighingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Bind data to controls
        VehicleList.ItemsSource = viewModel.WeighingRecords;
        DeviceStatusList.ItemsSource = viewModel.DeviceStatuses;
    }

    /// <summary>
    ///     Gets the current ViewModel
    /// </summary>
    public UrbanAttendedWeighingViewModel? ViewModel => DataContext as UrbanAttendedWeighingViewModel;

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnCloseClick(object? sender, RoutedEventArgs e)
        => Close();

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button clickedTab) return;
        if (ViewModel == null) return;

        TabAll.Classes.Remove("active");
        TabNormal.Classes.Remove("active");
        TabAbnormal.Classes.Remove("active");

        clickedTab.Classes.Add("active");

        // Trigger filter based on clicked tab
        var tabText = clickedTab.Content?.ToString();
        if (tabText != null)
        {
            ViewModel.SetFilterTab(tabText);
        }
    }

    private void OnRecordClick(object? sender, PointerPressedEventArgs e)
    {
        // TODO: Load photos for selected record
    }
}
