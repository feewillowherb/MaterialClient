using System.Windows.Input;
using MaterialClient.Views;
using ReactiveUI;

namespace MaterialClient.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public ICommand OpenAttendedWeighingCommand { get; }

    public MainWindowViewModel()
    {
        OpenAttendedWeighingCommand = ReactiveCommand.Create(OpenAttendedWeighing);
    }

    private void OpenAttendedWeighing()
    {
        var window = new AttendedWeighingWindow();
        window.Show();
    }
}