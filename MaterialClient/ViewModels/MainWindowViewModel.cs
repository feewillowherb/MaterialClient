using System;
using MaterialClient.Views;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

    public string Greeting { get; } = "Welcome to Avalonia!";

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [ReactiveCommand]
    private void OpenAttendedWeighing()
    {
        // Resolve window from Autofac container (ViewModel is injected via constructor)
        var window = _serviceProvider.GetRequiredService<AttendedWeighingWindow>();
        window.Show();
    }
}
