using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class AttendedWeighingDetailView : UserControl
{

    public AttendedWeighingDetailView()
        : this(null)
    {
    }

    public AttendedWeighingDetailView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModel>();
        // if (DataContext is AttendedWeighingDetailViewModel viewModel)
        // {
        //     viewModel.WhenAnyValue(x => x.MaterialsSelectionPopupViewModel)
        //         .Subscribe(popupViewModel =>
        //         {
        //             if (popupViewModel != null)
        //             {
        //                 MaterialsSelectionPopupControl.DataContext = popupViewModel;
        //             }
        //         });
        // }

    }

    private void MaterialSelectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && MaterialSelectionPopup != null && MaterialsSelectionPopupControl != null)
        {
            MaterialSelectionPopup.PlacementTarget = button;
            
            // 计算 HorizontalOffset 使 Popup 的左边缘对齐到 Button 的左边缘
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