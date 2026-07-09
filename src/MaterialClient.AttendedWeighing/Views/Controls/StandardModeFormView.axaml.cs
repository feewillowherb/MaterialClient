using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views.Controls;

public partial class StandardModeFormView : UserControl
{
    private readonly SerialDisposable _vmSubscription = new();

    public StandardModeFormView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _vmSubscription.Disposable = null;

        if (DataContext is not StandardWeighingDetailViewModel vm || MaterialSelectionPopup == null)
        {
            return;
        }

        _vmSubscription.Disposable = vm
            .WhenAnyValue(x => x.IsMaterialPopupOpen)
            .Subscribe(isOpen => MaterialSelectionPopup.IsOpen = isOpen);
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

            if (DataContext is StandardWeighingDetailViewModel vm && button.DataContext is MaterialItemRow row)
            {
                vm.OpenMaterialSelectionCommand.Execute(row).Subscribe();
            }
        }
    }
}

