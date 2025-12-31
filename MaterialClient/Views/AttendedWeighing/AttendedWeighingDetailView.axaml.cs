using System;
using Avalonia.Controls;
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
    }
}