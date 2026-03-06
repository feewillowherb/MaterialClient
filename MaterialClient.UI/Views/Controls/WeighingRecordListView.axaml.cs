using System;
using Avalonia.Controls;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.UI.Views.Controls;

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