using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.UI.Views.Controls;

public partial class StandardModeFormView : UserControl
{
    public StandardModeFormView()
    {
        InitializeComponent();
    }

    private void MaterialSelectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && MaterialSelectionPopup != null && MaterialsSelectionPopupControl != null)
        {
            MaterialSelectionPopup.PlacementTarget = button;

            // Placement="Bottom" 默认将 Popup 中心对齐到 Button 中心
            // 要让左边缘对齐，需要向右偏移：(PopupWidth / 2) - (ButtonWidth / 2)
            var popupWidth = MaterialsSelectionPopupControl.Width > 0
                ? MaterialsSelectionPopupControl.Width
                : 400; // MaterialsSelectionPopup 的默认宽度

            var buttonWidth = button.Bounds.Width > 0
                ? button.Bounds.Width
                : button.DesiredSize.Width;

            if (buttonWidth <= 0)
            {
                // 如果 Button 宽度还未测量，使用列宽 80
                buttonWidth = 80;
            }

            MaterialSelectionPopup.HorizontalOffset = (popupWidth / 2) - (buttonWidth / 2);
        }
    }
}

