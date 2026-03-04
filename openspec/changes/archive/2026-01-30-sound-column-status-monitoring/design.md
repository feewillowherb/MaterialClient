# 设计：在状态栏增加音柱设备状态监控

## 概览

本文说明音柱设备状态监控功能的技术设计，包括架构设计、接口定义、数据流与实现要点。

## 架构设计

### 系统架构图

```
┌──────────────────────────────────────────────────────────────────────┐
│                        AttendedWeighingWindow                         │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │                      Status Bar                                 │  │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐  │  │
│  │  │ Camera  │ │USB Cam  │ │ Printer │ │  Sound  │ │  ...    │  │  │
│  │  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘  │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Binding
                                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    AttendedWeighingViewModel                          │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │                    Rx State Management                          │  │
│  │  • IsSoundDeviceOnline (bool)                                  │  │
│  │  • SoundDeviceStatusBrush (Brush)                              │  │
│  │  • SoundDeviceStatusText (string)                              │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                  │                                     │
│                                  │ Observable.Interval()              │
│                                  ▼                                     │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │                    Status Polling Logic                         │  │
│  │  • Interval: 8 seconds (configurable)                          │  │
│  │  • Retry Policy: 3 attempts                                    │  │
│  │  • ObserveOn: MainThreadScheduler                              │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ Method Call
                                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      SoundDeviceService                               │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Task<bool> IsOnlineAsync()                                    │  │
│  │    1. Get SoundSN from ISettingsService                        │  │
│  │    2. Call ISoundDeviceApi.GetDeviceStatusAsync()              │  │
│  │    3. Return status == 1 || status == 2                       │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ HTTP GET
                                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│                        ISoundDeviceApi                                │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  GET /api/devices/getDeviceBySN                                │  │
│  │  Query Params:                                                  │  │
│  │    • type: "req"                                                │  │
│  │    • app: "ls20"                                                │  │
│  │    • sn: "ls20://020021EA63AC"                                 │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ HTTP Response
                                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      Sound Column Device                              │
│  Response: { "status": 1, "tasks": [] }                              │
└──────────────────────────────────────────────────────────────────────┘
```

### 组件职责

#### UI 层（AttendedWeighingWindow / ViewModel）
- **职责**：展示设备状态、管理用户交互
- **依赖**：`AttendedWeighingViewModel`
- **实现**：
  - XAML bindings to device status properties
  - Use data triggers to switch colors and text based on status code
  - Hide status indicator when device is disabled

#### ViewModel 层（AttendedWeighingViewModel）
- **职责**：状态管理、轮询调度、异常处理
- **依赖**：`ISoundDeviceService`、`ISettingsService`
- **实现**：
  - `BehaviorSubject<int>` for device status code management
  - `Observable.Interval()` to create periodic polling
  - `SelectMany` to flatten async API calls
  - `Retry()` for retry policy
  - `ObserveOn(RxApp.MainThreadScheduler)` to ensure UI thread updates
  - `Dispose()` to release subscriptions, prevent memory leaks

#### 服务层（SoundDeviceService）
- **职责**：业务逻辑、API 调用、数据转换
- **依赖**：`ISoundDeviceApi`、`ISettingsService`、`ILogger`
- **实现**：
  - Retrieve device serial number from `ISettingsService`
  - Call `ISoundDeviceApi.GetDeviceStatusAsync()`
  - Parse response DTO, determine if device is online
  - Exception handling and logging

#### API 层（ISoundDeviceApi）
- **职责**：封装 HTTP 客户端调用
- **依赖**：Refit
- **实现**：
  - Refit interface definition
  - Request parameter serialization
  - Response deserialization to DTO

#### 数据层（DTOs）
- **职责**：数据传输对象定义
- **实现**：
  - `SoundDeviceStatusDto` - Device status response
  - `DeviceTaskInfo` - Task information (optional)

## 接口定义

### ISoundDeviceService Extension

```csharp
public interface ISoundDeviceService
{
    /// <summary>
    ///     Check if sound column device is online
    ///     Retrieves device serial number from ISettingsService, no parameters needed
    /// </summary>
    /// <returns>
    ///     Returns true if device is online (status code 1 or 2), otherwise false
    ///     Returns false if device is disabled or configuration is invalid
    /// </returns>
    Task<bool> IsOnlineAsync();

    // Existing methods...
    Task PlayTextAsync(string text, CancellationToken cancellationToken = default);
    Task PlayTextV2Async(string text, CancellationToken cancellationToken = default);
}
```

### ISoundDeviceApi Extension

```csharp
public interface ISoundDeviceApi
{
    /// <summary>
    ///     Get sound column device status
    /// </summary>
    /// <param name="type">Request type, fixed value "req"</param>
    /// <param name="app">Application identifier, fixed value "ls20"</param>
    /// <param name="sn">Device serial number, format: "ls20://020021EA63AC"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device status response DTO</returns>
    [Get("/api/devices/getDeviceBySN")]
    Task<SoundDeviceStatusDto> GetDeviceStatusAsync(
        [Query] string type,
        [Query] string app,
        [Query] string sn,
        CancellationToken cancellationToken = default);

    // Existing methods...
    [Get("/tts_xf.single")]
    Task<Stream> GetTtsAudioAsync(...);

    [Post("")]
    Task<string> PlayAudioAsync(...);
}
```

### DTO Definition

```csharp
namespace MaterialClient.Common.Api.Dtos;

using System.Text.Json.Serialization;

/// <summary>
///     Sound column device status response DTO
/// </summary>
public record SoundDeviceStatusDto
{
    /// <summary>
    ///     Device status code
    ///     0 - Offline
    ///     1 - Online
    ///     2 - In Task
    ///     3 - Power Off
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>
    ///     Current task list
    /// </summary>
    [JsonPropertyName("tasks")]
    public IList<DeviceTaskInfo> Tasks { get; init; } = new List<DeviceTaskInfo>();
}

/// <summary>
///     Device task information
/// </summary>
public record DeviceTaskInfo
{
    // Task information structure (define based on actual API response)
    // Reserved field, may not be used in current version
}
```

## Implementation Details

### SoundDeviceService.IsOnlineAsync() Implementation

```csharp
/// <inheritdoc />
public async Task<bool> IsOnlineAsync()
{
    try
    {
        // 1. Get configuration
        var settings = await _settingsService.GetSettingsAsync();
        var soundDeviceSettings = settings.SoundDeviceSettings;

        // 2. Check if device is enabled
        if (!soundDeviceSettings.Enabled)
        {
            _logger?.LogDebug("Sound device is disabled, treating as offline");
            return false;
        }

        // 3. Check if configuration is valid
        if (!soundDeviceSettings.IsValid())
        {
            _logger?.LogWarning(
                "Sound device settings are incomplete: LocalIP={LocalIP}, SoundIP={SoundIP}, SoundSN={SoundSN}",
                soundDeviceSettings.LocalIP, soundDeviceSettings.SoundIP, soundDeviceSettings.SoundSN);
            return false;
        }

        // 4. Build device serial number (add prefix)
        var deviceSn = $"ls20://{soundDeviceSettings.SoundSN}";

        // 5. Call remote API
        var baseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(5);
        var api = RestService.For<ISoundDeviceApi>(httpClient);

        var statusResponse = await api.GetDeviceStatusAsync(
            type: "req",
            app: "ls20",
            sn: deviceSn);

        // 6. Parse status code
        var isOnline = statusResponse.Status == 1 || statusResponse.Status == 2;

        _logger?.LogDebug(
            "Sound device status check completed: DeviceSN={DeviceSN}, Status={Status}, IsOnline={IsOnline}",
            soundDeviceSettings.SoundSN, statusResponse.Status, isOnline);

        return isOnline;
    }
    catch (HttpRequestException ex)
    {
        _logger?.LogError(ex, "HTTP error while checking sound device status");
        return false;
    }
    catch (TaskCanceledException ex)
    {
        _logger?.LogWarning(ex, "Timeout while checking sound device status");
        return false;
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Unexpected error while checking sound device status");
        return false;
    }
}
```

### AttendedWeighingViewModel Extension

#### Fields and Properties

```csharp
// Device status code (0=offline, 1=online, 2=in-task, 3=power-off, -1=unknown)
private readonly BehaviorSubject<int> _soundDeviceStatus = new(-1);

// Polling subscription
private readonly IDisposable _statusPollingDisposable;

/// <summary>
///     Whether sound column device is online
/// </summary>
public bool IsSoundDeviceOnline => _soundDeviceStatus.Value == 1 || _soundDeviceStatus.Value == 2;

/// <summary>
///     Whether sound column device is enabled
/// </summary>
public bool IsSoundDeviceEnabled => _settingsService.GetSettingsAsync()
    .ContinueWith(t => t.Result.SoundDeviceSettings.Enabled, TaskScheduler.Default)
    .Result; // Or use Reactive property

/// <summary>
///     Sound column device status brush
/// </summary>
public ISolidColorBrush SoundDeviceStatusBrush => _soundDeviceStatus.Value switch
{
    1 => new SolidColorBrush(Color.Parse("#10B981")), // Online - Green
    2 => new SolidColorBrush(Color.Parse("#F59E0B")), // In Task - Yellow
    3 => new SolidColorBrush(Color.Parse("#EF4444")), // Power Off - Red
    _ => new SolidColorBrush(Color.Parse("#9CA3AF"))  // Offline/Unknown - Gray
};

/// <summary>
///     Sound column device status text
/// </summary>
public string SoundDeviceStatusText => _soundDeviceStatus.Value switch
{
    0 => "Offline",
    1 => "Online",
    2 => "In Task",
    3 => "Power Off",
    _ => "Unknown"
};
```

#### Polling Logic

```csharp
private void InitializeSoundDeviceStatusPolling()
{
    _statusPollingDisposable = Observable
        .Interval(TimeSpan.FromSeconds(8)) // Poll every 8 seconds
        .SelectMany(_ => Observable.FromAsync(cancellationToken =>
            _soundDeviceService.IsOnlineAsync()))
        .Select(isOnline => isOnline ? 1 : 0) // Simplified logic: only distinguish online/offline
        .Retry(3) // Retry 3 times on failure
        .Catch(Observable.Return(-1)) // Return unknown status on exception
        .Subscribe(
            status =>
            {
                _soundDeviceStatus.OnNext(status);
                this.RaisePropertyChanged(nameof(IsSoundDeviceOnline));
                this.RaisePropertyChanged(nameof(SoundDeviceStatusBrush));
                this.RaisePropertyChanged(nameof(SoundDeviceStatusText));
            },
            ex => _logger?.LogError(ex, "Error in sound device status polling"));
}
```

#### Resource Disposal

```csharp
public void Dispose()
{
    // Release other subscriptions...

    // Release sound column device status polling subscription
    _statusPollingDisposable?.Dispose();
    _soundDeviceStatus?.Dispose();
}
```

### XAML UI Implementation

```xml
<!-- Sound Device Status Indicator -->
<StackPanel Grid.Column="4"
            Orientation="Horizontal"
            Spacing="8"
            VerticalAlignment="Center"
            Margin="24,0,0,0"
            IsVisible="{Binding IsSoundDeviceEnabled}"
            ToolTip.Tip="Sound column device status">
    <!-- Status Indicator Ellipse -->
    <Ellipse Width="10" Height="10" VerticalAlignment="Center">
        <Ellipse.Fill>
            <SolidColorBrush Color="{Binding SoundDeviceStatusColor}" />
        </Ellipse.Fill>
    </Ellipse>

    <TextBlock Text="Sound"
               FontSize="13"
               Foreground="#666"
               VerticalAlignment="Center" />

    <!-- Status Text -->
    <TextBlock Text="{Binding SoundDeviceStatusText}"
               FontSize="13"
               VerticalAlignment="Center"
               FontWeight="SemiBold"
               Foreground="{Binding SoundDeviceStatusBrush}" />
</StackPanel>
```

## Error Handling Strategy

### Exception Categories

#### 1. Configuration Exception
- **Scenario**: Device serial number not configured or invalid format
- **Handling**: Return `false`, log warning
- **User Notification**: No notification (status indicator hidden when device disabled)

#### 2. Network Exception
- **Scenario**: HTTP request timeout or connection failure
- **Handling**: Catch exception, return `false`, log error
- **Retry Strategy**: Use Rx `Retry()` to retry 3 times
- **User Notification**: No notification, status bar shows "Offline"

#### 3. API Exception
- **Scenario**: API returns non-200 status code or response format error
- **Handling**: Catch exception, return `false`, log error
- **User Notification**: No notification, status bar shows "Offline"

#### 4. JSON Parsing Exception
- **Scenario**: Response JSON format does not match expectation
- **Handling**: Catch exception, return `false`, log warning
- **User Notification**: No notification, status bar shows "Offline"

### Log Levels

| Level | Scenario | Example |
|------|----------|---------|
| Debug | Normal status polling | "Sound device status check completed: Status=1, IsOnline=True" |
| Information | API call successful | "Playing audio on sound device" (existing) |
| Warning | Invalid configuration, timeout | "Sound device settings are incomplete" |
| Error | Network exception, JSON parsing failure | "HTTP error while checking sound device status" |

## Performance Considerations

### Memory Optimization

#### Subscription Management
- Use `DisposeWith()` or explicit `Dispose()` to release subscriptions
- Avoid caching large objects in `BehaviorSubject`
- Ensure all resources are released when window is closed

#### Object Lifecycle
- `SoundDeviceStatusDto` uses `record` type (immutable)
- `SolidColorBrush` uses static instances or caching
- Avoid creating new objects in polling loop

### Network Optimization

#### Request Optimization
- Polling interval >= 5 seconds, default 8 seconds
- HTTP timeout set to 5 seconds
- Use connection pooling (`IHttpClientFactory`)

#### Response Caching
- No caching (real-time status required)
- Consider adding 1-2 second cache if status changes infrequently

### CPU Optimization

#### Thread Scheduling
- Use `ObserveOn(RxApp.MainThreadScheduler)` to ensure UI updates on main thread
- API calls execute on thread pool threads
- Avoid CPU-intensive operations in polling loop

#### JSON Parsing
- Use `System.Text.Json` (high performance)
- Avoid reflection, use `JsonPropertyName` attributes

## Testing Strategy

### Unit Tests

#### SoundDeviceServiceTests
```csharp
[Fact]
public async Task IsOnlineAsync_ShouldReturnTrue_WhenDeviceIsOnline()
{
    // Arrange
    var mockApi = new Mock<ISoundDeviceApi>();
    mockApi.Setup(x => x.GetDeviceStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync(new SoundDeviceStatusDto { Status = 1 });

    var service = new SoundDeviceService(..., mockApi.Object, ...);

    // Act
    var result = await service.IsOnlineAsync();

    // Assert
    Assert.True(result);
}

[Fact]
public async Task IsOnlineAsync_ShouldReturnFalse_WhenDeviceIsDisabled()
{
    // Arrange
    var settings = new Settings { SoundDeviceSettings = new() { Enabled = false } };
    // ... mock setup

    // Act
    var result = await service.IsOnlineAsync();

    // Assert
    Assert.False(result);
}
```

### Integration Tests

#### End-to-End Testing
1. Launch application, open attended weighing window
2. Wait 8 seconds, check status bar displays sound column device status
3. Disconnect sound column device network, wait 8 seconds, check status bar shows "Offline"
4. Restore network connection, wait 8 seconds, check status bar shows "Online"

### Memory Leak Testing

#### Test Steps
1. Create `AttendedWeighingViewModel` instance
2. Simulate 1000 status polling cycles
3. Dispose ViewModel instance
4. Use dotMemory or Visual Studio Profiler to check for memory leaks
5. Ensure all subscriptions are released

#### Test Code
```csharp
[Fact]
public async Task SoundDeviceStatusPolling_ShouldNotLeakMemory()
{
    // Arrange
    var viewModel = new AttendedWeighingViewModel(...);

    // Act
    await Task.Delay(TimeSpan.FromSeconds(80)); // Simulate 10 polling cycles
    viewModel.Dispose();

    // Assert
    // Use dotMemory to check for memory leaks
    GC.Collect();
    GC.WaitForPendingFinalizers();
    // Verify: No subscription leaks, no event handler leaks
}
```

## Configuration

### New appsettings.json Configuration Items

```json
{
  "SoundDevice": {
    "StatusPollingIntervalSeconds": 8,
    "StatusQueryTimeoutSeconds": 5,
    "StatusRetryAttempts": 3
  }
}
```

### Configuration Description

| Configuration Item | Type | Default Value | Description |
|--------------------|------|---------------|-------------|
| `StatusPollingIntervalSeconds` | int | 8 | Status polling interval (seconds), minimum 5 |
| `StatusQueryTimeoutSeconds` | int | 5 | Status query timeout (seconds) |
| `StatusRetryAttempts` | int | 3 | Failure retry count |

## Migration Path

### Version Compatibility

- **v1.0**: Basic implementation, only supports online/offline status
- **v1.1**: Enhanced status display, distinguish online/in-task/power-off status
- **v1.2**: Add task list display (optional)

### Upgrade Steps

1. Update `MaterialClient.Common` project, add new interfaces and DTOs
2. Update `MaterialClient` project, modify ViewModel and XAML
3. Update `appsettings.json`, add polling configuration (optional)
4. Deploy and verify functionality

### Rollback Plan

If rollback is needed, only:
1. Restore `ISoundDeviceService` and `ISoundDeviceApi` interfaces
2. Remove polling logic from ViewModel
3. Restore XAML file

## Security Considerations

### Security Risks

1. **Device Serial Number Leakage**: Logs may record device serial number
   - **Mitigation**: Log desensitization, avoid recording full device serial number

2. **Denial of Service Attack**: Frequent polling may be identified as attack by device
   - **Mitigation**: Polling interval >= 5 seconds, add request rate limiting

3. **Man-in-the-Middle Attack**: HTTP requests not encrypted
   - **Mitigation**: LAN communication, device IP isolation

### Best Practices

- Do not log sensitive information (device serial numbers, IP addresses)
- Use `IHttpClientFactory` to manage HTTP client lifecycle
- Add request timeout to prevent long-term blocking

## Future Enhancements

### Short-term Improvements

1. **Task List Display**: Show current task details on mouse hover
2. **Status History**: Record device status change history for troubleshooting
3. **Alert Feature**: Popup alert when device offline exceeds threshold

### Long-term Improvements

1. **WebSocket Real-time Push**: Replace polling, reduce network overhead
2. **Multi-device Support**: Support multiple sound column device status monitoring
3. **Device Control Integration**: Restart device or clear task queue directly from status bar

## References

- Rx.NET Memory Leak Best Practices: https://msdn.microsoft.com/en-us/library/hh242985(v=vs.103).aspx
- Refit Documentation: https://github.com/reactiveui/refit
- Project Rx State Management Guidelines: `openspec/project.md` - Architecture Patterns
