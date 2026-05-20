using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;

namespace MaterialClient.Urban.Views;

public partial class WeighingSystemWindow : Window
{
    private readonly WeighingSystemViewModel _viewModel;

    public WeighingSystemWindow()
    {
        _viewModel = new WeighingSystemViewModel();
        DataContext = _viewModel;
        InitializeComponent();
        _viewModel.LoadMockData();

        // Bind data to controls
        VehicleList.ItemsSource = _viewModel.WeighingRecords;
        DeviceStatusList.ItemsSource = _viewModel.DeviceStatuses;
    }

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

        TabAll.Classes.Remove("active");
        TabNormal.Classes.Remove("active");
        TabAbnormal.Classes.Remove("active");

        clickedTab.Classes.Add("active");
    }

    private void OnRecordClick(object? sender, PointerPressedEventArgs e)
    {
        // TODO: Load photos for selected record
    }
}
