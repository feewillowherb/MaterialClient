using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Tests.Mocks;
using Microsoft.Extensions.Logging;
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
    private readonly List<LicensePlateRecognizedEvent> _receivedEvents = new();

    public HikvisionLprServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new MockHikvisionLprService();

        // 订阅事件流以接收事件
        _service.PlateRecognized.Subscribe(@event =>
        {
            _receivedEvents.Add(@event);
            _output.WriteLine($"收到事件: Device={@event.DeviceName}, Plate={@event.PlateNumber}, Direction={@event.Direction}, Time={@event.Timestamp}");
        });
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
        var config1 = CreateTestConfig("192.168.1.100", "Camera1", LicensePlateDirection.In);
        _service.AddOrUpdateDevice(config1);

        var config2 = CreateTestConfig("192.168.1.100", "Camera1Updated", LicensePlateDirection.Out);

        // Act
        _service.AddOrUpdateDevice(config2);

        // Assert
        Assert.Equal(1, _service.DeviceCount);
        var retrievedConfig = _service.GetDeviceConfig("192.168.1.100");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Camera1Updated", retrievedConfig.Name);
        Assert.Equal(LicensePlateDirection.Out, retrievedConfig.Direction);
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
            Direction = LicensePlateDirection.In,
            Timestamp = DateTime.Now
        };

        // Act
        _service.SimulatePlateRecognition(@event);

        // Assert
        Assert.Single(_service.RecognizedEvents);
        Assert.Single(_receivedEvents);
        Assert.Same(@event, _service.RecognizedEvents[0]);
        Assert.Same(@event, _receivedEvents[0]);
    }

    [Fact]
    public void SimulatePlateRecognition_ShouldPublishMultipleEvents()
    {
        // Arrange
        var event1 = CreateTestEvent("京A12345", "Camera1", LicensePlateDirection.In);
        var event2 = CreateTestEvent("沪B67890", "Camera2", LicensePlateDirection.Out);

        // Act
        _service.SimulatePlateRecognition(event1);
        _service.SimulatePlateRecognition(event2);

        // Assert
        Assert.Equal(2, _service.RecognizedEvents.Count);
        Assert.Equal(2, _receivedEvents.Count);
    }

    [Fact]
    public void SimulatePlateRecognition_SimplifiedVersion_ShouldPublishEvent()
    {
        // Act
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.In);

        // Assert
        Assert.Single(_service.RecognizedEvents);
        Assert.Single(_receivedEvents);
        Assert.Equal("京A12345", _service.RecognizedEvents[0].PlateNumber);
        Assert.Equal("Camera1", _service.RecognizedEvents[0].DeviceName);
        Assert.Equal(LicensePlateDirection.In, _service.RecognizedEvents[0].Direction);
    }

    [Fact]
    public void GetEventsByDevice_ShouldReturnCorrectEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.In);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.Out);
        _service.SimulatePlateRecognition("粤C11111", "Camera1", LicensePlateDirection.In);

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
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.In);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.Out);
        _service.SimulatePlateRecognition("京A12345", "Camera3", LicensePlateDirection.In);

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
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.In);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.Out);
        _service.SimulatePlateRecognition("粤C11111", "Camera3", LicensePlateDirection.In);

        // Act
        var inEvents = _service.GetEventsByDirection(LicensePlateDirection.In);
        var outEvents = _service.GetEventsByDirection(LicensePlateDirection.Out);

        // Assert
        Assert.Equal(2, inEvents.Count);
        Assert.Single(outEvents);
        Assert.All(inEvents, e => Assert.Equal(LicensePlateDirection.In, e.Direction));
        Assert.All(outEvents, e => Assert.Equal(LicensePlateDirection.Out, e.Direction));
    }

    [Fact]
    public void ClearRecognizedEvents_ShouldClearAllEvents()
    {
        // Arrange
        _service.SimulatePlateRecognition("京A12345", "Camera1", LicensePlateDirection.In);
        _service.SimulatePlateRecognition("沪B67890", "Camera2", LicensePlateDirection.Out);

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

    private LicensePlateRecognitionConfig CreateTestConfig(string ip, string name,
        LicensePlateDirection direction = LicensePlateDirection.In)
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
