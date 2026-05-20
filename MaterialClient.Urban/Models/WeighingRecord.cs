using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MaterialClient.Urban.Models;

/// <summary>
///     称重记录数据模型
/// </summary>
public class WeighingRecord : INotifyPropertyChanged
{
    private string _licensePlate = "";
    private string _weighingTime = "";
    private double _weight;
    private string _status = "正常";

    public string LicensePlate
    {
        get => _licensePlate;
        set => SetField(ref _licensePlate, value);
    }

    public string WeighingTime
    {
        get => _weighingTime;
        set => SetField(ref _weighingTime, value);
    }

    public double Weight
    {
        get => _weight;
        set => SetField(ref _weight, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public bool IsNormal => Status == "正常";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
