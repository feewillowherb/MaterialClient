using System;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string Greeting { get; } = "Welcome to Avalonia!";

    [ReactiveCommand]
    private void OpenAttendedWeighing()
    {
        // Resolve window from Autofac container (ViewModel is injected via constructor)
        var window = _serviceProvider.GetRequiredService<AttendedWeighingWindow>();
        window.Show();
    }
}