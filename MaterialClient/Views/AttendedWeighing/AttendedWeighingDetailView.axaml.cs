using System;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

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

}