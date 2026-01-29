using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Hikvision;
using ReactiveExtensions;

namespace MaterialClient.Common.Tests.Mocks;

/// <summary>
///     海康威视车牌识别服务的 Mock 实现
///     用于单元测试，可以模拟车牌识别事件
/// </summary>
public sealed class MockHikvisionLprService : IHikvisionLprService
{
    private readonly ConcurrentDictionary<string, LicensePlateRecognitionConfig> _deviceConfigs = new();
    private readonly Subject<LicensePlateRecognizedEvent> _plateRecognizedSubject = new();
    private bool _isStarted;

    /// <summary>
    ///     记录所有识别事件
    /// </summary>
    public List<LicensePlateRecognizedEvent> RecognizedEvents { get; } = new();

    /// <summary>
    ///     控制 IsOnline 方法的返回值
    /// </summary>
    public bool IsOnlineReturnValue { get; set; } = true;

    /// <summary>
    ///     控制 StartAsync 方法的返回值
    /// </summary>
    public bool StartAsyncReturnValue { get; set; } = true;

    /// <summary>
    ///     车牌识别事件流
    /// </summary>
    public IObservable<LicensePlateRecognizedEvent> PlateRecognized => _plateRecognizedSubject.AsObservable();

    /// <summary>
    ///     添加或更新设备配置
    /// </summary>
    public void AddOrUpdateDevice(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid())
        {
            throw new ArgumentException("设备配置无效", nameof(config));
        }

        _deviceConfigs.AddOrUpdate(config.Ip, config, (_, __) => config);
    }

    /// <summary>
    ///     检查设备是否在线
    /// </summary>
    public bool IsOnline(LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return IsOnlineReturnValue;
    }

    /// <summary>
    ///     启动监听服务
    /// </summary>
    public async Task<bool> StartAsync(string listenLocalIp, int listenLocalPort)
    {
        await Task.CompletedTask;

        if (_isStarted)
        {
            return false;
        }

        _isStarted = true;
        return StartAsyncReturnValue;
    }

    /// <summary>
    ///     停止监听服务
    /// </summary>
    public async Task StopAsync()
    {
        await Task.CompletedTask;
        _isStarted = false;
    }

    /// <summary>
    ///     模拟车牌识别事件
    /// </summary>
    public void SimulatePlateRecognition(LicensePlateRecognizedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // 记录事件
        RecognizedEvents.Add(@event);

        // 发布到事件流
        _plateRecognizedSubject.OnNext(@event);
    }

    /// <summary>
    ///     模拟车牌识别事件（简化版）
    /// </summary>
    public void SimulatePlateRecognition(string plateNumber, string deviceName,
        MaterialClient.Common.Entities.Enums.LicensePlateDirection direction =
            MaterialClient.Common.Entities.Enums.LicensePlateDirection.In)
    {
        var @event = new LicensePlateRecognizedEvent
        {
            PlateNumber = plateNumber,
            DeviceName = deviceName,
            Direction = direction,
            Timestamp = DateTime.Now
        };

        SimulatePlateRecognition(@event);
    }

    /// <summary>
    ///     清除所有识别事件
    /// </summary>
    public void ClearRecognizedEvents()
    {
        RecognizedEvents.Clear();
    }

    /// <summary>
    ///     获取指定设备名称的识别事件
    /// </summary>
    public List<LicensePlateRecognizedEvent> GetEventsByDevice(string deviceName)
    {
        return RecognizedEvents.Where(e => e.DeviceName == deviceName).ToList();
    }

    /// <summary>
    ///     获取指定车牌号的识别事件
    /// </summary>
    public List<LicensePlateRecognizedEvent> GetEventsByPlateNumber(string plateNumber)
    {
        return RecognizedEvents.Where(e => e.PlateNumber == plateNumber).ToList();
    }

    /// <summary>
    ///     获取指定方向的识别事件
    /// </summary>
    public List<LicensePlateRecognizedEvent> GetEventsByDirection(
        MaterialClient.Common.Entities.Enums.LicensePlateDirection direction)
    {
        return RecognizedEvents.Where(e => e.Direction == direction).ToList();
    }

    /// <summary>
    ///     获取设备配置数量
    /// </summary>
    public int DeviceCount => _deviceConfigs.Count;

    /// <summary>
    ///     检查是否包含指定设备
    /// </summary>
    public bool ContainsDevice(string deviceIp)
    {
        return _deviceConfigs.ContainsKey(deviceIp);
    }

    /// <summary>
    ///     获取指定设备的配置
    /// </summary>
    public LicensePlateRecognitionConfig? GetDeviceConfig(string deviceIp)
    {
        _deviceConfigs.TryGetValue(deviceIp, out var config);
        return config;
    }
}
