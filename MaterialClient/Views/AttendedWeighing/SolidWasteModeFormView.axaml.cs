using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.AttendedWeighing;

public partial class SolidWasteModeFormView : UserControl
{
    public SolidWasteModeFormView()
    {
        InitializeComponent();
    }

    private void StreetsSelectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            StreetsSelectionPopup != null &&
            StreetsSelectionPopupControl != null)
        {
            StreetsSelectionPopup.PlacementTarget = button;

            var popupWidth = StreetsSelectionPopupControl.Width > 0
                ? StreetsSelectionPopupControl.Width
                : 400;

            var buttonWidth = button.Bounds.Width > 0
                ? button.Bounds.Width
                : button.DesiredSize.Width;

            if (buttonWidth <= 0)
            {
                buttonWidth = 80;
            }

            StreetsSelectionPopup.HorizontalOffset = (popupWidth / 2) - (buttonWidth / 2);

            if (DataContext is AttendedWeighingDetailViewModel vm)
            {
                vm.IsStreetsPopupOpen = true;
            }
        }
    }

    private void MaterialsSelectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            MaterialsSelectionPopup != null &&
            MaterialsSelectionPopupControl != null)
        {
            MaterialsSelectionPopup.PlacementTarget = button;

            var popupWidth = MaterialsSelectionPopupControl.Width > 0
                ? MaterialsSelectionPopupControl.Width
                : 400;

            var buttonWidth = button.Bounds.Width > 0
                ? button.Bounds.Width
                : button.DesiredSize.Width;

            if (buttonWidth <= 0)
            {
                buttonWidth = 80;
            }

            MaterialsSelectionPopup.HorizontalOffset = (popupWidth / 2) - (buttonWidth / 2);

            if (DataContext is AttendedWeighingDetailViewModel vm)
            {
                vm.IsMaterialsPopupOpen = true;
            }
        }
    }

    private void ProvidersSelectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            ProvidersSelectionPopup != null &&
            ProvidersSelectionPopupControl != null)
        {
            ProvidersSelectionPopup.PlacementTarget = button;

            var popupWidth = ProvidersSelectionPopupControl.Width > 0
                ? ProvidersSelectionPopupControl.Width
                : 400;

            var buttonWidth = button.Bounds.Width > 0
                ? button.Bounds.Width
                : button.DesiredSize.Width;

            if (buttonWidth <= 0)
            {
                buttonWidth = 80;
            }

            ProvidersSelectionPopup.HorizontalOffset = (popupWidth / 2) - (buttonWidth / 2);

            if (DataContext is AttendedWeighingDetailViewModel vm)
            {
                vm.IsProvidersPopupOpen = true;
            }
        }
    }
}

