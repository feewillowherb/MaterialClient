using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     海康威视车牌识别服务单元测试
/// </summary>
public class HikvisionLprServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly MockHikvisionLprService _service;

    public HikvisionLprServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new MockHikvisionLprService();
    }

    [Fact]
    public void AddOrUpdateDevice_ShouldAddNewDevice()
    {
        // Arrange
        var config = CreateTestConfig("192.168.1.100", "Camera1");

        // Act
        _service.AddOrUpdateDevice(config);

        // Assert
        Assert.Equal(1, _service.DeviceCount);
        Assert.True(_service.ContainsDevice("192.168.1.100"));
        var retrievedConfig = _service.GetDeviceConfig("192.168.1.100");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Camera1", retrievedConfig.Name);
        Assert.Equal("192.168.1.100", retrievedConfig.Ip);
    }

    [Fact]
    public void AddOrUpdateDevice_ShouldUpdateExistingDevice()
    {
        // Arrange
        var config1 = CreateTestConfig("192.168.1.100", "Camera1", LicensePlateDirection.A);
        _service.AddOrUpdateDevice(config1);

        var config2 = CreateTestConfig("192.168.1.100", "Camera1Updated", LicensePlateDirection.B);

        // Act
        _service.AddOrUpdateDevice(config2);

        // Assert
        Assert.Equal(1, _service.DeviceCount);
        var retrievedConfig = _service.GetDeviceConfig("192.168.1.100");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Camera1Updated", retrievedConfig.Name);
        Assert.Equal(LicensePlateDirection.B, retrievedConfig.Direction);
    }

    [Fact]
    public void AddOrUpdateDevice_ShouldThrowOnNullConfig()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.AddOrUpdateDevice(null!));
    }

    [Fact]
    public void AddOrUpdateDevice_ShouldThrowOnInvalidConfig()
    {
        // Arrange
        var invalidConfig = new LicensePlateRecognitionConfig
        {
            Name = "",
            Ip = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.AddOrUpdateDevice(invalidConfig));
    }

    [Fact]
    public void IsOnline_ShouldReturnConfiguredValue()
    {
        // Arrange
        var config = CreateTestConfig("192.168.1.100", "Camera1");
        _service.IsOnlineReturnValue = true;

        // Act
        var result = _service.IsOnline(config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOnline_ShouldReturnFalseWhenConfigured()
    {
        // Arrange
        var config = CreateTestConfig("192.168.1.100", "Camera1");
        _service.IsOnlineReturnValue = false;

        // Act
        var result = _service.IsOnline(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOnline_ShouldThrowOnNullConfig()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.IsOnline(null!));
    }

    [Fact]
    public async Task StartAsync_ShouldReturnConfiguredValue()
    {
        // Arrange
        _service.StartAsyncReturnValue = true;

        // Act
        var result = await _service.StartAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task StartAsync_ShouldReturnFalseWhenAlreadyStarted()
    {
        // Arrange
        _service.StartAsyncReturnValue = true;
        await _service.StartAsync();

        // Act
        var result = await _service.StartAsync();

        // Assert
        Assert.False(result); // 已启动，应返回 false
    }

    [Fact]
    public async Task StopAsync_ShouldStopSuccessfully()
    {
        // Arrange
        await _service.StartAsync();

        // Act
        await _service.StopAsync();

        // Assert
        // 再次启动应该成功（如果停止成功）
        var result = await _service.StartAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrowWhenNotStarted()
    {
        // Act & Assert (should not throw)
        await _service.StopAsync();
    }

    [Fact]
    public void SimulatePlateRecognition_ShouldPublishEvent()
    {
        // Arrange
        var @event = new LicensePlateRecognizedEvent
        {
            PlateNumber = "京A12345",
            DeviceName = "Camera1",
            Direction = LicensePlateDirection.A,
            Timestamp = DateTime.Now
        };

        // Act
        _service.SimulatePlateRecognition(@event);

        // Assert (Mock records to RecognizedEvents and sends LicensePlateRecognizedMessage via MessageBus)
        Assert.Single(_service.RecognizedEvents);
        Assert.Same(@event, _service.RecognizedEvents[0]);
    }

    [Fact]
    public void SimulatePlateRecognition_ShouldPublishMultipleEvents()
    {
        // Arrange
        var event1 = CreateTestEvent("京A12345", "Camera1", LicensePlateDirection.A);
        var event2 = CreateTestEvent("沪B67890", "Camera2", LicensePlateDirection.B);

        // Act
        _service.SimulatePlateRecognition(event1);
        _service.SimulatePlateRecognition(event2);

        // Assert
        Assert.Equal(2, _service.RecognizedEvents.Count);
    }

    [Fact]
    public void SimulatePlateRecognition_SimplifiedVersion_ShouldPublishEvent()
    {
        // Act
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.A);

        // Assert
        Assert.Single(_service.RecognizedEvents);
        Assert.Equal("京A12345", _service.RecognizedEvents[0].PlateNumber);
        Assert.Equal("Camera1", _service.RecognizedEvents[0].DeviceName);
        Assert.Equal(LicensePlateDirection.A, _service.RecognizedEvents[0].Direction);
    }

    [Fact]
    public void GetEventsByDevice_ShouldReturnCorrectEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.A);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.B);
        _service.SimulatePlateRecognition("粤C11111", "Camera1", LicensePlateDirection.A);

        // Act
        var camera1Events = _service.GetEventsByDevice("Camera1");
        var camera2Events = _service.GetEventsByDevice("Camera2");

        // Assert
        Assert.Equal(2, camera1Events.Count);
        Assert.Single(camera2Events);
        Assert.All(camera1Events, e => Assert.Equal("Camera1", e.DeviceName));
        Assert.All(camera2Events, e => Assert.Equal("Camera2", e.DeviceName));
    }

    [Fact]
    public void GetEventsByPlateNumber_ShouldReturnCorrectEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.A);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.B);
        _service.SimulatePlateRecognition("京A12345", "Camera3", LicensePlateDirection.A);

        // Act
        var events = _service.GetEventsByPlateNumber("京A12345");

        // Assert
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal("京A12345", e.PlateNumber));
    }

    [Fact]
    public void GetEventsByDirection_ShouldReturnCorrectEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.A);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.B);
        _service.SimulatePlateRecognition("粤C11111", "Camera3", LicensePlateDirection.A);

        // Act
        var inEvents = _service.GetEventsByDirection(LicensePlateDirection.A);
        var outEvents = _service.GetEventsByDirection(LicensePlateDirection.B);

        // Assert
        Assert.Equal(2, inEvents.Count);
        Assert.Single(outEvents);
        Assert.All(inEvents, e => Assert.Equal(LicensePlateDirection.A, e.Direction));
        Assert.All(outEvents, e => Assert.Equal(LicensePlateDirection.B, e.Direction));
    }

    [Fact]
    public void ClearRecognizedEvents_ShouldClearAllEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.A);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.B);

        // Act
        _service.ClearRecognizedEvents();

        // Assert
        Assert.Empty(_service.RecognizedEvents);
    }

    [Fact]
    public void SimulatePlateRecognition_ShouldThrowOnNullEvent()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.SimulatePlateRecognition(null!));
    }

    [Fact]
    public async Task TriggerCaptureAsync_ShouldCompleteWithoutThrowing()
    {
        var config = CreateTestConfig("192.168.1.100", "Camera1");

        await _service.TriggerCaptureAsync(config);
    }

    private LicensePlateRecognitionConfig CreateTestConfig(string ip, string name,
        LicensePlateDirection direction = LicensePlateDirection.A)
    {
        return new LicensePlateRecognitionConfig
        {
            Ip = ip,
            Name = name,
            Direction = direction,
            UserName = "admin",
            Password = "admin123",
            Port = "8000",
            Channel = "1"
        };
    }

    private LicensePlateRecognizedEvent CreateTestEvent(string plateNumber, string deviceName,
        LicensePlateDirection direction)
    {
        return new LicensePlateRecognizedEvent
        {
            PlateNumber = plateNumber,
            DeviceName = deviceName,
            Direction = direction,
            Timestamp = DateTime.Now
        };
    }

    public void Dispose()
    {
        // 清理资源
        _service?.Dispose();
    }
}
