using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views.Controls;

public partial class StandardModeFormView : UserControl
{
    private readonly SerialDisposable _vmSubscription = new();

    public StandardModeFormView()
    {
        InitializeComponent();

        if (MaterialSelectionPopup != null)
        {
            MaterialSelectionPopup.Closed += OnMaterialSelectionPopupClosed;
        }
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

    private void OnMaterialSelectionPopupClosed(object? sender, EventArgs e)
    {
        if (DataContext is StandardWeighingDetailViewModel vm)
        {
            vm.CloseMaterialPopup();
        }
    }

    private void MaterialSelectionCell_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || MaterialSelectionPopup == null || MaterialsSelectionPopupControl == null)
        {
            return;
        }

        e.Handled = true;
        MaterialSelectionPopup.PlacementTarget = control;

        // Placement="Bottom" centers the popup on the target; offset so left edges align.
        var popupWidth = MaterialsSelectionPopupControl.Width > 0
            ? MaterialsSelectionPopupControl.Width
            : 400;

        var targetWidth = control.Bounds.Width > 0
            ? control.Bounds.Width
            : control.DesiredSize.Width;

        if (targetWidth <= 0)
        {
            targetWidth = 72;
        }

        MaterialSelectionPopup.HorizontalOffset = (popupWidth / 2) - (targetWidth / 2);

        if (DataContext is StandardWeighingDetailViewModel vm && control.DataContext is MaterialItemRow row)
        {
            vm.OpenMaterialSelectionCommand.Execute(row).Subscribe();
        }
    }
}
