using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MaterialClient.Demo.Models;

public class CandidateRecord : INotifyPropertyChanged
{
    private string _licensePlate = "";
    private string _supplier = "";
    private double _weight;
    private DateTime _entryTime;
    private string _elapsedTime = "";

    public string LicensePlate
    {
        get => _licensePlate;
        set => SetField(ref _licensePlate, value);
    }

    public string Supplier
    {
        get => _supplier;
        set => SetField(ref _supplier, value);
    }

    public double Weight
    {
        get => _weight;
        set => SetField(ref _weight, value);
    }

    public DateTime EntryTime
    {
        get => _entryTime;
        set => SetField(ref _entryTime, value);
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetField(ref _elapsedTime, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
