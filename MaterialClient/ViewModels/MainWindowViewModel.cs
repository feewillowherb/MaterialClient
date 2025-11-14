using System;
using System.Windows.Input;
using MaterialClient.Views;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    
    public string Greeting { get; } = "Welcome to Avalonia!";

    public ICommand OpenAttendedWeighingCommand { get; }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        OpenAttendedWeighingCommand = ReactiveCommand.Create(OpenAttendedWeighing);
    }

    private void OpenAttendedWeighing()
    {
        // Resolve window from Autofac container (ViewModel is injected via constructor)
        var window = _serviceProvider.GetRequiredService<AttendedWeighingWindow>();
        window.Show();
    }
}