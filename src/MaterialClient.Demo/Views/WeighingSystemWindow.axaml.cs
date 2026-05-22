using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.Demo.ViewModels;

namespace MaterialClient.Demo.Views;

public partial class WeighingSystemWindow : Window
{
    public WeighingSystemWindow()
    {
        InitializeComponent();
        LoadMockData();
    }

    private void LoadMockData()
    {
        VehicleList.ItemsSource = WeighingSystemDataGenerator.GetWeighingRecords();
        DeviceStatusList.ItemsSource = WeighingSystemDataGenerator.GetDeviceStatuses();
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
    }
}
