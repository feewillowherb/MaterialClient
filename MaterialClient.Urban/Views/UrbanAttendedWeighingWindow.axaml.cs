using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.UI.Controls;
using MaterialClient.UI.ViewModels;
using MaterialClient.Urban.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views;

public partial class UrbanAttendedWeighingWindow : Window, ITransientDependency
{
    private IServiceProvider? _serviceProvider;

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
    public UrbanAttendedWeighingWindow(UrbanAttendedWeighingViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _serviceProvider = serviceProvider;

        // Bind data to controls
        VehicleList.ItemsSource = viewModel.WeighingRecords;
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

    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var edge = ResolveResizeEdge(sender);
        if (edge is null)
            return;

        BeginResizeDrag(edge.Value, e);
    }

    private static WindowEdge? ResolveResizeEdge(object? sender) =>
        sender switch
        {
            Border { Name: "ResizeNorth" } => WindowEdge.North,
            Border { Name: "ResizeSouth" } => WindowEdge.South,
            Border { Name: "ResizeWest" } => WindowEdge.West,
            Border { Name: "ResizeEast" } => WindowEdge.East,
            Border { Name: "ResizeNorthWest" } => WindowEdge.NorthWest,
            Border { Name: "ResizeNorthEast" } => WindowEdge.NorthEast,
            Border { Name: "ResizeSouthWest" } => WindowEdge.SouthWest,
            Border { Name: "ResizeSouthEast" } => WindowEdge.SouthEast,
            _ => null,
        };

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
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

    private void OnSystemSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_serviceProvider == null) return;

        var settingsViewModel = _serviceProvider.GetService<SettingsViewModel>();
        if (settingsViewModel == null) return;

        var dialog = new SettingsDialog
        {
            DataContext = settingsViewModel,
        };
        dialog.Show(this);
    }
}
