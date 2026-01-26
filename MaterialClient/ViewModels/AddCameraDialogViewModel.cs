using System;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     ViewModel for Add Camera Dialog
/// </summary>
public partial class AddCameraDialogViewModel : ViewModelBase
{
    [Reactive] private string _name = string.Empty;
    [Reactive] private string _ip = string.Empty;
    [Reactive] private string _port = string.Empty;
    [Reactive] private string _channel = string.Empty;
    [Reactive] private string _userName = string.Empty;
    [Reactive] private string _password = string.Empty;

    public CameraConfigViewModel? Result { get; private set; }

    [ReactiveCommand]
    private void Save()
    {
        Result = new CameraConfigViewModel
        {
            Name = Name,
            Ip = Ip,
            Port = Port,
            Channel = Channel,
            UserName = UserName,
            Password = Password
        };
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}
