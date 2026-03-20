using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using System.Reactive.Linq;

namespace MaterialClient.Views.Controls;

public partial class SolidWasteModeFormView : UserControl
{
    public SolidWasteModeFormView()
    {
        InitializeComponent();
        this.GetObservable(DataContextProperty).Subscribe(OnDataContextChanged);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(SetPopupPlacementTargets, DispatcherPriority.Loaded);
    }

    private void OnDataContextChanged(object? _)
    {
        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= Vm_PropertyChanged;
            npc.PropertyChanged += Vm_PropertyChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SolidWasteModeDetailViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(SolidWasteModeDetailViewModel.IsStreetsPopupOpen):
                if (vm.IsStreetsPopupOpen)
                    Dispatcher.UIThread.Post(() => ApplyPopupOffset(StreetsSelectionPopup, StreetsSelectionPopupControl, StreetsSelectionBox), DispatcherPriority.Loaded);
                break;
            case nameof(SolidWasteModeDetailViewModel.IsMaterialsPopupOpen):
                if (vm.IsMaterialsPopupOpen)
                    Dispatcher.UIThread.Post(() => ApplyPopupOffset(MaterialsSelectionPopup, MaterialsSelectionPopupControl, MaterialsSelectionBox), DispatcherPriority.Loaded);
                break;
            case nameof(SolidWasteModeDetailViewModel.IsProvidersPopupOpen):
                if (vm.IsProvidersPopupOpen)
                    Dispatcher.UIThread.Post(() => ApplyPopupOffset(ProvidersSelectionPopup, ProvidersSelectionPopupControl, ProvidersSelectionBox), DispatcherPriority.Loaded);
                break;
        }
    }

    private void SetPopupPlacementTargets()
    {
        if (StreetsSelectionPopup != null && StreetsSelectionBox != null)
            StreetsSelectionPopup.PlacementTarget = StreetsSelectionBox;
        if (MaterialsSelectionPopup != null && MaterialsSelectionBox != null)
            MaterialsSelectionPopup.PlacementTarget = MaterialsSelectionBox;
        if (ProvidersSelectionPopup != null && ProvidersSelectionBox != null)
            ProvidersSelectionPopup.PlacementTarget = ProvidersSelectionBox;
    }

    private static void ApplyPopupOffset(Popup? popup, Control? popupContent, Control? trigger)
    {
        if (popup == null || popupContent == null || trigger == null) return;

        var popupWidth = popupContent.Width > 0 ? popupContent.Width : 400;
        var triggerWidth = trigger.Bounds.Width > 0 ? trigger.Bounds.Width : trigger.DesiredSize.Width;
        if (triggerWidth <= 0) triggerWidth = 80;
        popup.HorizontalOffset = (popupWidth / 2) - (triggerWidth / 2);
    }
}
