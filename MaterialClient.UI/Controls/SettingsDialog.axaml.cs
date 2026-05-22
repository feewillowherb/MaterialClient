using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.ViewModels;
using ReactiveUI;

namespace MaterialClient.UI.Controls;

/// <summary>
///     Shared settings dialog window with navigation sidebar and content panel.
/// </summary>
public partial class SettingsDialog : Window
{
    private SettingsViewModel? _viewModel;
    private IDisposable? _selectedSectionSubscription;

    public SettingsDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _selectedSectionSubscription?.Dispose();
        _selectedSectionSubscription = null;

        if (DataContext is SettingsViewModel vm)
        {
            _viewModel = vm;
            _selectedSectionSubscription = vm.WhenAnyValue(x => x.SelectedSection)
                .Subscribe(OnSelectedSectionChanged);

            RefreshSectionContent();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _selectedSectionSubscription?.Dispose();
        _selectedSectionSubscription = null;
        base.OnClosed(e);
    }

    private void OnSelectedSectionChanged(ISettingsSection? section)
    {
        RefreshSectionContent();
    }

    private void RefreshSectionContent()
    {
        var contentControl = this.FindControl<ContentControl>("SectionContent");
        if (contentControl is null || _viewModel is null) return;

        contentControl.Content = _viewModel.CreateSelectedSectionView();
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;

        WindowEdge edge;
        if (border.Name == "ResizeNorth") edge = WindowEdge.North;
        else if (border.Name == "ResizeSouth") edge = WindowEdge.South;
        else if (border.Name == "ResizeWest") edge = WindowEdge.West;
        else if (border.Name == "ResizeEast") edge = WindowEdge.East;
        else return;

        BeginResizeDrag(edge, e);
    }
}
