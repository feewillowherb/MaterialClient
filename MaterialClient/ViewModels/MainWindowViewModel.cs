using CommunityToolkit.Mvvm.Input;
using MaterialClient.Views;

namespace MaterialClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    [RelayCommand]
    private void OpenAttendedWeighing()
    {
        var window = new AttendedWeighingWindow();
        window.Show();
    }
}