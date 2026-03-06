using System;
using Avalonia.Controls;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.UI.Views.Controls;

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

}