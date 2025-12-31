using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class AttendedWeighingMainView : UserControl
{
    public AttendedWeighingMainView() : this(null)
    {
        InitializeComponent();
    }

    public AttendedWeighingMainView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModel>();
    }
}