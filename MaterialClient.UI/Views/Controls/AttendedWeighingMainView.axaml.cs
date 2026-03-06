using System;
using Avalonia.Controls;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.UI.Views.Controls;

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