using System.Collections.ObjectModel;
using MaterialClient.Urban.Models;
using ReactiveUI;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     称重系统主界面 ViewModel
///     管理称重记录列表、设备状态、照片显示等功能
/// </summary>
public class WeighingSystemViewModel : ReactiveObject
{
    private ObservableCollection<WeighingRecord> _weighingRecords = [];
    private ObservableCollection<DeviceStatus> _deviceStatuses = [];
    private WeighingRecord? _selectedRecord;
    private string _currentWeight = "0.00";
    private string _weightStatus = "称重已结束";
    private string _weightStatusColor = "#4ADE80";

    /// <summary>
    ///     称重记录列表
    /// </summary>
    public ObservableCollection<WeighingRecord> WeighingRecords
    {
        get => _weighingRecords;
        set => this.RaiseAndSetIfChanged(ref _weighingRecords, value);
    }

    /// <summary>
    ///     设备状态列表
    /// </summary>
    public ObservableCollection<DeviceStatus> DeviceStatuses
    {
        get => _deviceStatuses;
        set => this.RaiseAndSetIfChanged(ref _deviceStatuses, value);
    }

    /// <summary>
    ///     当前选中的称重记录
    /// </summary>
    public WeighingRecord? SelectedRecord
    {
        get => _selectedRecord;
        set => this.RaiseAndSetIfChanged(ref _selectedRecord, value);
    }

    /// <summary>
    ///     当前重量显示
    /// </summary>
    public string CurrentWeight
    {
        get => _currentWeight;
        set => this.RaiseAndSetIfChanged(ref _currentWeight, value);
    }

    /// <summary>
    ///     称重状态文本
    /// </summary>
    public string WeightStatus
    {
        get => _weightStatus;
        set => this.RaiseAndSetIfChanged(ref _weightStatus, value);
    }

    /// <summary>
    ///     称重状态颜色
    /// </summary>
    public string WeightStatusColor
    {
        get => _weightStatusColor;
        set => this.RaiseAndSetIfChanged(ref _weightStatusColor, value);
    }

    /// <summary>
    ///     加载模拟数据（首期占位，后续接入真实数据服务）
    /// </summary>
    public void LoadMockData()
    {
        WeighingRecords =
        [
            new() { LicensePlate = "浙A06L07", WeighingTime = "05-06 16:30", Weight = 9.81, Status = "正常" },
            new() { LicensePlate = "浙A98J22", WeighingTime = "05-06 16:27", Weight = 8.20, Status = "正常" },
            new() { LicensePlate = "浙A96H93", WeighingTime = "05-06 16:16", Weight = 11.03, Status = "正常" },
            new() { LicensePlate = "浙A62J79", WeighingTime = "05-06 15:43", Weight = 7.55, Status = "异常" },
            new() { LicensePlate = "浙A02G55", WeighingTime = "05-06 15:13", Weight = 10.40, Status = "正常" },
            new() { LicensePlate = "浙A06L07", WeighingTime = "05-06 13:47", Weight = 6.78, Status = "异常" },
        ];

        DeviceStatuses =
        [
            new() { DeviceName = "地磅设备", IsOnline = true },
            new() { DeviceName = "摄像头", IsOnline = true },
            new() { DeviceName = "车牌识别", IsOnline = false },
        ];
    }
}
