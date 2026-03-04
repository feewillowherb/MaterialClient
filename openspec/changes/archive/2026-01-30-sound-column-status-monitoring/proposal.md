# 提案：在状态栏增加音柱设备状态监控

## 变更 ID
`sound-column-status-monitoring`

## 状态
**执行完成** - 实现已完成

## 实施摘要

### 已完成任务（阶段 1–3）：
- ✅ Task 1.1: Created SoundDeviceStatusDto.cs
- ✅ Task 1.2: Extended ISoundDeviceApi interface with GetDeviceStatusAsync()
- ✅ Task 1.3: Implemented SoundDeviceService.IsOnlineAsync()
- ✅ Task 2.1: Extended AttendedWeighingViewModel with sound device status fields
- ✅ Task 2.2: Implemented polling logic with Rx Observable.Interval
- ✅ Task 2.3: Implemented resource disposal in Dispose() method
- ✅ Task 3.1: Modified AttendedWeighingWindow.axaml status bar UI

### 已修改文件：
1. `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs` (NEW)
2. `MaterialClient.Common/Api/ISoundDeviceApi.cs` (MODIFIED)
3. `MaterialClient.Common/Services/SoundDeviceService.cs` (MODIFIED)
4. `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` (MODIFIED)
5. `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml` (MODIFIED)

### 待办任务（可选/测试）：
- Task 1.4: Write unit tests for SoundDeviceService.IsOnlineAsync()
- Task 2.4: Write memory leak tests
- Task 3.2: Integration testing (manual testing)
- Task 3.3: Regression testing
- Task 4.1: Add configuration items to appsettings.json
- Task 4.2: Update documentation

## 概览

在 `AttendedWeighingWindow` 的状态栏中增加音柱设备在线状态监控功能，使操作员能实时监控音柱设备工作状态。

## 问题陈述

### 当前问题

1. **Status bar missing sound column device status display**
   - Operators cannot visually see whether the sound column device is online on the main interface
   - The existing status bar displays status for cameras, USB cameras, printers, and other devices, but lacks sound column device status

2. **Missing device online status detection interface**
   - `ISoundDeviceService` only provides voice playback functionality, without device online status query methods
   - Cannot proactively detect whether the sound column device is working properly

3. **Missing remote API integration**
   - `ISoundDeviceApi` does not encapsulate remote device status query interface
   - Device status queries require calling HTTP GET `/api/devices/getDeviceBySN` endpoint

4. **Device failures cannot be detected in time**
   - When the sound column device goes offline, loses power, or experiences network interruption, the system cannot proactively alert
   - Affects the timeliness of voice broadcasting in production workflows

### 影响范围

- **User Experience**: Operators cannot discover sound column device failures in time
- **Operations Efficiency**: Device troubleshooting requires manual inspection, increasing maintenance costs
- **Production Workflow**: Sound column device failures affect the normal use of voice broadcasting functionality

## 提议方案

### 核心功能

1. **Add device online status detection service**
   - Add `Task<bool> IsOnlineAsync()` method to `ISoundDeviceService`
   - Retrieve device serial number from `ISettingsService`, no parameters needed
   - Call remote API to query device online status

2. **Extend remote API interface**
   - Add `GET /api/devices/getDeviceBySN` endpoint to `ISoundDeviceApi`
   - Support querying sound column device status (online/offline/in-task/power-off)

3. **Status bar UI integration**
   - Add sound column status indicator to `AttendedWeighingWindow` status bar
   - Use color coding to identify device status (green=online, gray=offline, yellow=in-task, red=power-off)
   - Poll device status periodically (recommended interval 5-10 seconds)
   - Display warning alert when device is offline

4. **Reactive state management**
   - Add sound column device status properties to `AttendedWeighingViewModel`
   - Use Rx Observable for periodic status polling
   - Ensure proper subscription disposal to avoid memory leaks

### 技术实现要点

#### API 规范

**请求格式：**
```http
GET /api/devices/getDeviceBySN?type=req&app=ls20&sn=ls20://020021EA63AC
```

**响应格式：**
```json
{
  "status": 1,
  "tasks": []
}
```

**状态码定义：**
- `0` - Offline
- `1` - Online
- `2` - In Task
- `3` - Power Off

#### 服务层设计

**ISoundDeviceService Interface Extension:**
```csharp
Task<bool> IsOnlineAsync();
```
- Retrieve device serial number from `ISettingsService`
- Call `ISoundDeviceApi.GetDeviceStatusAsync()`
- Return whether device is online (status code 1 or 2 considered online)

**ISoundDeviceApi Interface Extension:**
```csharp
[Get("/api/devices/getDeviceBySN")]
Task<SoundDeviceStatusDto> GetDeviceStatusAsync(
    [Query] string type,
    [Query] string app,
    [Query] string sn,
    CancellationToken cancellationToken = default);
```

#### DTO Definition

```csharp
public record SoundDeviceStatusDto
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("tasks")]
    public IList<DeviceTaskInfo> Tasks { get; init; } = new List<DeviceTaskInfo>();
}
```

#### UI 状态管理

**AttendedWeighingViewModel Extension:**
```csharp
// Device online status
public bool IsSoundDeviceOnline => _soundDeviceStatus.Value == 1 || _soundDeviceStatus.Value == 2;

// Device status code (0=offline, 1=online, 2=in-task, 3=power-off)
private readonly BehaviorSubject<int> _soundDeviceStatus;

// Status polling Observable
private readonly IDisposable _statusPollingDisposable;
```

**Status Bar XAML:**
```xml
<!-- Sound Device Status Indicator -->
<StackPanel Orientation="Horizontal" Spacing="8" IsVisible="{Binding IsSoundDeviceEnabled}">
    <!-- Status Indicator Ellipse with color binding -->
    <Ellipse Width="10" Height="10" Fill="{Binding SoundDeviceStatusBrush}" />
    <TextBlock Text="Sound" FontSize="13" Foreground="#666" />
    <TextBlock Text="{Binding SoundDeviceStatusText}" FontSize="13" FontWeight="SemiBold" />
</StackPanel>
```

#### 轮询机制

- Use `Observable.Interval()` to create timer
- Polling interval: 5-10 seconds (configurable in `appsettings.json`)
- Use `SelectMany` to flatten async API calls
- Use `Retry()` for enhanced fault tolerance
- Use `ObserveOn(RxApp.MainThreadScheduler)` to ensure UI updates on main thread

#### 内存泄漏预防

- Dispose polling subscription in `AttendedWeighingViewModel` destructor or `Dispose()` method
- Use `DisposeWith()` for subscription lifecycle management
- Follow project Rx memory management guidelines (see `project.md`)

## 影响分析

### 功能影响

#### 正面影响
- **Improved User Experience**: Operators can monitor sound column device status in real-time
- **Enhanced Operations Efficiency**: Reduce device troubleshooting time
- **Better System Observability**: Unified status bar displays all critical device statuses

#### 潜在风险
- **Network Dependency**: Status queries depend on network connectivity; network failures may cause false negatives
- **Polling Overhead**: Periodic polling increases API request frequency; polling interval needs to be controlled
- **Performance Impact**: Frequent HTTP requests may affect system performance

### 技术影响

#### 代码变更

**新文件：**
- `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs`

**已修改文件：**
- `MaterialClient.Common/Api/ISoundDeviceApi.cs` - Add status query interface
- `MaterialClient.Common/Services/SoundDeviceService.cs` - Add online detection method
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` - Add device status properties and polling logic
- `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml` - Add status bar UI elements

#### 兼容性
- **Backward Compatible**: New interfaces do not affect existing `PlayTextAsync()` and `PlayTextV2Async()` functionality
- **Optional Feature**: Sound column device status query failure does not affect other business workflows
- **Configuration Compatible**: Add polling interval configuration item with default value (8 seconds)

#### 性能影响
- **Network Overhead**: Each polling generates 1 HTTP GET request
- **Memory Overhead**: Add `BehaviorSubject<int>` and `IDisposable` subscription objects
- **CPU Overhead**: Rx timer scheduling and JSON parsing overhead

**优化建议：**
- Polling interval configurable, default 8 seconds
- Use Polly retry policy for enhanced fault tolerance
- Consider adding response cache to avoid repeated queries within short timeframes

### 依赖影响

#### 外部依赖
- **Remote API**: Depends on availability of sound column device `/api/devices/getDeviceBySN` endpoint
- **Network Connection**: Depends on stable network connection to sound column device

#### 内部依赖
- **ISettingsService**: Needs to retrieve sound column device serial number (`SoundSN`)
- **ISoundDeviceApi**: Needs to encapsulate HTTP client calls

## 考虑的替代方案

### Alternative 1: WebSocket Real-time Push (Not Adopted)
**Pros:**
- High real-time performance, device status changes pushed immediately
- Reduce unnecessary polling requests

**Cons:**
- Requires sound column device WebSocket protocol support
- Increased server-side implementation complexity
- Complex connection management, need to handle disconnection and reconnection

**Conclusion**: Current sound column device does not support WebSocket; polling solution is more feasible

### Alternative 2: Local Heartbeat Detection (Not Adopted)
**Pros:**
- No dependency on remote API
- Fast response time

**Cons:**
- Requires sound column device SDK support
- Cannot detect remote device status
- Increased local network load

**Conclusion**: Sound column device uses remote control architecture; remote API query should be used

### Alternative 3: Detect Only During Playback (Not Adopted)
**Pros:**
- Reduce API request count
- Simple implementation

**Cons:**
- Cannot proactively discover device failures
- High user perception delay

**Conclusion**: Does not meet "real-time monitoring" requirement

## 成功标准

### 功能验收标准

1. **Service Layer**
   - [ ] `ISoundDeviceService.IsOnlineAsync()` correctly calls remote API
   - [ ] API response parsing is accurate, status code mapping is correct
   - [ ] Returns `false` when device is disabled or configuration is invalid

2. **API Layer**
   - [ ] `ISoundDeviceApi.GetDeviceStatusAsync()` correctly encapsulates HTTP requests
   - [ ] Request parameters match API specification (`type=req`, `app=ls20`)
   - [ ] Response deserialization is correct, includes status code and task list

3. **UI Layer**
   - [ ] Status bar displays sound column device status indicator
   - [ ] Color coding is correct: green (online), gray (offline), yellow (in-task), red (power-off)
   - [ ] Status text is correct: "Online", "Offline", "In Task", "Power Off"
   - [ ] Status indicator is hidden when device is disabled

4. **Polling Mechanism**
   - [ ] Timer starts normally, interval matches configuration (default 8 seconds)
   - [ ] UI automatically refreshes after status update
   - [ ] Timer is properly disposed when window is closed

5. **Exception Handling**
   - [ ] Network failures do not affect other functionality
   - [ ] API timeout triggers automatic retry
   - [ ] Logs record key operations and errors

### 性能验收标准

1. **Memory Leak Testing**
   - [ ] Memory usage shows no significant growth after 24 hours of operation
   - [ ] No subscription leaks after multiple window open/close cycles
   - [ ] Passes `AttendedWeighingServiceMemoryLeakTests`

2. **Response Time**
   - [ ] Status query response time < 2 seconds
   - [ ] UI status update latency < 100ms

3. **Network Overhead**
   - [ ] Polling interval >= 5 seconds to avoid excessive requests
   - [ ] Single request response size < 1KB

### 回归测试

1. **Existing Features Unaffected**
   - [ ] Voice playback functionality works normally
   - [ ] Other device status displays work normally
   - [ ] Window loading performance shows no significant degradation

## 工期估算

**Total Estimated Effort**: 2-3 working days

### Phase Breakdown

**Day 1 - API and Service Layer**
- Create `SoundDeviceStatusDto`
- Extend `ISoundDeviceApi` interface
- Implement `SoundDeviceService.IsOnlineAsync()`
- Write unit tests

**Day 2 - ViewModel and State Management**
- Extend `AttendedWeighingViewModel`
- Implement polling logic and state management
- Add memory leak tests

**Day 3 - UI Integration and Testing**
- Modify `AttendedWeighingWindow.axaml`
- Implement status indicator UI
- Integration testing and regression testing
- Documentation updates

## 相关规范

### 相关能力

- `attended-weighing` - Attended weighing functionality
- `sound-device-broadcast` - Sound column voice broadcasting functionality (to be created)

### 相关文档

- `openspec/project.md` - Project context and tech stack
- `docs/AttendedWeighingService-MemoryLeak-Testing-Guide.md` - Memory leak testing guide
- `MaterialClient.Common/Configuration/SoundDeviceSettings.cs` - Sound column device configuration

## 待决问题

1. **API Format Confirmation**: Does the remote API strictly follow the described format? Needs actual testing verification
2. **Polling Interval**: Is 5-10 seconds polling interval reasonable? Should it be configurable?
3. **Task Information**: What is the complete structure of the `tasks` field? Is it necessary to display task details?
4. **Error Handling**: When device status query fails, should we display an error message or maintain the last known status?

## 参考

- Existing status bar implementation: `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml:400-500`
- Sound column service implementation: `MaterialClient.Common/Services/SoundDeviceService.cs`
- API interface definition: `MaterialClient.Common/Api/ISoundDeviceApi.cs`
- Rx state management guidelines: `openspec/project.md` - Architecture Patterns
