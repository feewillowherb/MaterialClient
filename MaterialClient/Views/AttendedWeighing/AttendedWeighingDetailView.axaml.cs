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
    private readonly IServiceProvider? _serviceProvider;

    public AttendedWeighingDetailView()
        : this(null)
    {
    }

    public AttendedWeighingDetailView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModel>();
    }
}