using System;
using System.Windows.Input;
using MaterialClient.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    
    public string Greeting { get; } = "Welcome to Avalonia!";

    [RelayCommand]
    private void OpenAttendedWeighing()
    {
        // Resolve window from Autofac container (ViewModel is injected via constructor)
        var window = _serviceProvider.GetRequiredService<AttendedWeighingWindow>();
        window.Show();
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}