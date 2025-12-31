using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class WeighingRecordListView : UserControl
{
    public WeighingRecordListView(): this(null)
    {
        InitializeComponent();
    }
    
    public WeighingRecordListView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModel>();
    }
}