using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Abstractions;
using ReactiveUI;

namespace MaterialClient.UI.Controls;

/// <summary>
///     Shared settings dialog window with navigation sidebar and content panel.
/// </summary>
public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is SettingsViewModel vm)
        {
            // Listen for selected section changes
            vm.WhenAnyValue(x => x.SelectedSection)
                .Subscribe(OnSelectedSectionChanged);

            // Show initial section
            if (vm.SelectedSection is not null)
            {
                OnSelectedSectionChanged(vm.SelectedSection);
            }
        }
    }

    private void OnSelectedSectionChanged(ISettingsSection? section)
    {
        var contentControl = this.FindControl<ContentControl>("SectionContent");
        if (contentControl is not null && section is not null)
        {
            contentControl.Content = section.CreateView();
        }
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
