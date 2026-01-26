using System;
using MaterialClient.Common.Entities.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     ViewModel for Add LPR Dialog
/// </summary>
public partial class AddLprDialogViewModel : ViewModelBase
{
    [Reactive] private string _name = string.Empty;
    [Reactive] private string _ip = string.Empty;
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.In;

    public int DirectionIndex
    {
        get => (int)_direction;
        set
        {
            if (value is >= 0 and <= 1)
            {
                Direction = (LicensePlateDirection)value;
                this.RaisePropertyChanged();
            }
        }
    }

    public string DirectionText => _direction == LicensePlateDirection.In ? "进场" : "出场";

    public LicensePlateRecognitionConfigViewModel? Result { get; private set; }

    public AddLprDialogViewModel()
    {
        this.WhenAnyValue(x => x.Direction)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(DirectionIndex));
                this.RaisePropertyChanged(nameof(DirectionText));
            });
    }

    [ReactiveCommand]
    private void Save()
    {
        Result = new LicensePlateRecognitionConfigViewModel
        {
            Name = Name,
            Ip = Ip,
            Direction = Direction
        };
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}
