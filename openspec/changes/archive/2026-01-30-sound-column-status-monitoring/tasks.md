# 实施任务

## 概览

本文列出「在状态栏增加音柱设备状态监控」功能的所有任务，按优先级与依赖排序。

---

## 阶段 1：API 与数据层（第 1 天）

### 任务 1.1：创建 DTO 类
**优先级**：高
**工作量**：30 分钟
**依赖**：无

**描述**：
创建音柱设备状态响应 DTO。

**验收标准**：
- [x] Create `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs`
- [x] Define `Status` property (`int`, JsonPropertyName "status")
- [x] Define `Tasks` property (`IList<DeviceTaskInfo>`, JsonPropertyName "tasks")
- [x] Create `DeviceTaskInfo` record (reserved field)

**文件**：
- `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs`

**相关需求**：
- Device Status API Integration

---

### 任务 1.2：扩展 ISoundDeviceApi 接口
**优先级**：高
**工作量**：30 分钟
**依赖**：任务 1.1

**描述**：
在 `ISoundDeviceApi` 中增加设备状态查询接口。

**验收标准**：
- [x] Add `GetDeviceStatusAsync()` method to `ISoundDeviceApi`
- [x] Use Refit `[Get("/api/devices/getDeviceBySN")]` attribute
- [x] Define query parameters: `type`, `app`, `sn`
- [x] Return type is `Task<SoundDeviceStatusDto>`
- [x] Add XML comment documentation

**文件**：
- `MaterialClient.Common/Api/ISoundDeviceApi.cs`

**相关需求**：
- Device Status API Integration

---

### 任务 1.3：实现 SoundDeviceService.IsOnlineAsync()
**优先级**：高
**工作量**：2 小时
**依赖**：任务 1.1、1.2

**描述**：
在 `SoundDeviceService` 中实现设备在线状态检测方法。

**验收标准**：
- [x] Add `Task<bool> IsOnlineAsync()` method to `ISoundDeviceService` interface
- [x] Implement the method in `SoundDeviceService`
- [x] Retrieve `SoundDeviceSettings` from `ISettingsService`
- [x] Check if device is enabled (`Enabled`), return `false` if not enabled
- [x] Check if configuration is valid (`IsValid()`), return `false` if invalid
- [x] Build device serial number format: `"ls20://{SoundSN}"`
- [x] Create `HttpClient`, BaseURL is `"http://{SoundIP}:8888"`
- [x] Call `ISoundDeviceApi.GetDeviceStatusAsync()`
- [x] Parse response: return `true` if `status == 1 || status == 2`, otherwise return `false`
- [x] Exception handling: catch `HttpRequestException`, `TaskCanceledException`, return `false`
- [x] Logging: Debug level for normal queries, Warning level for invalid configuration, Error level for exceptions

**文件**：
- `MaterialClient.Common/Services/SoundDeviceService.cs`

**相关需求**：
- Device Status Polling
- Device Status API Integration

---

### 任务 1.4：编写单元测试
**优先级**：中
**工作量**：2 小时
**依赖**：任务 1.3

**描述**：
为 `SoundDeviceService.IsOnlineAsync()` 编写单元测试。

**验收标准**：
- [ ] Create `SoundDeviceServiceTests.cs` (if not exists)
- [ ] Test case: Returns `true` when device is online (mock API returns `status=1`)
- [ ] Test case: Returns `true` when device is in-task (mock API returns `status=2`)
- [ ] Test case: Returns `false` when device is offline (mock API returns `status=0`)
- [ ] Test case: Returns `false` when device is powered off (mock API returns `status=3`)
- [ ] Test case: Returns `false` when device is disabled (mock Settings)
- [ ] Test case: Returns `false` when configuration is invalid (mock Settings)
- [ ] Test case: Returns `false` when network exception occurs (mock API throws exception)
- [ ] Use Moq or NSubstitute framework
- [ ] All tests pass

**文件**：
- `MaterialClient.Common.Tests/Services/SoundDeviceServiceTests.cs`

**相关需求**：
- Device Status Polling
- Device Status API Integration

---

## 阶段 2：ViewModel 与状态管理（第 2 天）

### 任务 2.1：扩展 AttendedWeighingViewModel 字段
**Priority**: High
**Effort**: 1 hour
**Dependencies**: Task 1.3

**Description**:
Add sound column device status management fields and properties to `AttendedWeighingViewModel`.

**Acceptance Criteria**:
- [x] Add `BehaviorSubject<int> _soundDeviceStatus` field (initial value -1)
- [x] Add `IDisposable _statusPollingDisposable` field
- [x] Add property `IsSoundDeviceOnline` => `_soundDeviceStatus.Value == 1 || _soundDeviceStatus.Value == 2`
- [x] Add property `IsSoundDeviceEnabled` => get from `ISettingsService`
- [x] Add property `SoundDeviceStatusColor` => return corresponding `Color` based on `_soundDeviceStatus.Value`
  - `1` => `#10B981` (Green)
  - `2` => `#F59E0B` (Yellow)
  - `3` => `#EF4444` (Red)
  - Other => `#9CA3AF` (Gray)
- [x] Add property `SoundDeviceStatusText` => return corresponding text based on `_soundDeviceStatus.Value`
  - `0` => "离线"
  - `1` => "在线"
  - `2` => "任务中"
  - `3` => "断电"
  - Other => "未知"

**文件**：
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**相关需求**：
- Status Bar UI Display

---

### 任务 2.2：实现轮询逻辑
**Priority**: High
**Effort**: 2 hours
**Dependencies**: Task 2.1

**Description**:
Implement sound column device status polling logic in `AttendedWeighingViewModel`.

**Acceptance Criteria**:
- [x] Create private method `InitializeSoundDeviceStatusPolling()`
- [x] Use `Observable.Interval(TimeSpan.FromSeconds(8))` to create timer
- [x] Use `SelectMany` to flatten async API calls: `_soundDeviceService.IsOnlineAsync()`
- [x] Use `Select` to convert `bool` to status code: `isOnline ? 1 : 0`
- [x] Use `Retry(3)` to retry up to 3 times
- [x] Use `Catch(Observable.Return(-1))` to catch exceptions, return unknown status
- [x] Use `Subscribe` to update `_soundDeviceStatus`
- [x] Call `RaisePropertyChanged` in `Subscribe` to notify UI updates
- [x] Call `InitializeSoundDeviceStatusPolling()` in constructor
- [x] Log errors at Error level in polling exception handler

**文件**：
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**相关需求**：
- Device Status Polling
- Status Bar UI Display

---

### 任务 2.3：实现资源释放
**Priority**: High
**Effort**: 30 minutes
**Dependencies**: Task 2.2

**Description**:
Release polling subscriptions in `AttendedWeighingViewModel`'s `Dispose()` method.

**Acceptance Criteria**:
- [x] Call `_statusPollingDisposable?.Dispose()` in `Dispose()` method
- [x] Call `_soundDeviceStatus?.Dispose()` in `Dispose()` method
- [x] Ensure disposal order is correct (dispose subscriptions first, then Subjects)
- [x] Ensure no exceptions thrown (use `?.` and `try-catch`)

**文件**：
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**相关需求**：
- Memory Leak Prevention

---

### 任务 2.4：编写内存泄漏测试
**Priority**: Medium
**Effort**: 2 hours
**Dependencies**: Task 2.3

**Description**:
Write memory leak tests to verify polling subscriptions are properly released.

**Acceptance Criteria**:
- [ ] Create `AttendedWeighingViewModelMemoryLeakTests.cs` (if not exists)
- [ ] Test case: Create ViewModel, wait 80 seconds, dispose, check no subscription leaks
- [ ] Test case: Repeat create/dispose ViewModel 100 times, check memory growth < 50MB
- [ ] Use `dotMemory` or `Visual Studio Profiler` for verification
- [ ] Tests pass

**文件**：
- `MaterialClient.Common.Tests/ViewModels/AttendedWeighingViewModelMemoryLeakTests.cs`

**相关需求**：
- Memory Leak Prevention

---

## 阶段 3：UI 集成（第 3 天）

### 任务 3.1：修改 AttendedWeighingWindow.axaml
**Priority**: High
**Effort**: 1 hour
**Dependencies**: Task 2.1

**Description**:
Add sound column device status indicator to `AttendedWeighingWindow` status bar.

**Acceptance Criteria**:
- [x] Add sound column device status indicator after printer status indicator (`Grid.Column="3"`) at `Grid.Column="4"`
- [x] Use `StackPanel`, `Orientation="Horizontal"`, `Spacing="8"`
- [x] Add `Ellipse` (10x10 dot), `Fill` bound to `SoundDeviceStatusColor`
- [x] Add `TextBlock`, Text="音柱", FontSize=13, Foreground=#666
- [x] Add `TextBlock`, Text bound to `SoundDeviceStatusText`, FontSize=13, FontWeight=SemiBold
- [x] Entire StackPanel's `IsVisible` bound to `IsSoundDeviceEnabled`
- [x] Add `ToolTip.Tip="音柱设备状态"`
- [x] Style matches other device status indicators

**文件**：
- `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml`

**相关需求**：
- Status Bar UI Display

---

### 任务 3.2：集成测试
**Priority**: Medium
**Effort**: 2 hours
**Dependencies**: Task 3.1

**Description**:
Manual testing of end-to-end flow for sound column device status monitoring functionality.

**Acceptance Criteria**:
- [ ] Launch application, open attended weighing window, status bar displays sound column device status
- [ ] Wait 8 seconds, status bar automatically updates
- [ ] Disconnect sound column device network, wait 8 seconds, status bar shows "Offline" (gray)
- [ ] Restore network connection, wait 8 seconds, status bar shows "Online" (green)
- [ ] Close window, check memory release is normal
- [ ] Reopen window, status bar displays normally
- [ ] When device is disabled, status bar does not show sound column device status indicator
- [ ] Other device status indicators work normally

**文件**：
- Manual testing checklist

**相关需求**：
- Device Status Polling
- Status Bar UI Display

---

### 任务 3.3：回归测试
**Priority**: Medium
**Effort**: 1 hour
**Dependencies**: Task 3.2

**Description**:
Verify existing functionality is not affected.

**Acceptance Criteria**:
- [ ] Voice playback functionality works normally (`PlayTextAsync()`, `PlayTextV2Async()`)
- [ ] Camera status display works normally
- [ ] USB camera status display works normally
- [ ] Printer status display works normally
- [ ] Window loading performance shows no significant degradation
- [ ] All existing unit tests pass

**文件**：
- Regression testing checklist

**相关需求**：
- All Requirements

---

## 阶段 4：配置与文档（可选）

### 任务 4.1：添加配置项
**Priority**: Low
**Effort**: 30 minutes
**Dependencies**: Task 2.2

**Description**:
Add polling configuration items to `appsettings.json`.

**Acceptance Criteria**:
- [ ] Add `SoundDevice` configuration section to `appsettings.json`
- [ ] Add `StatusPollingIntervalSeconds` (default 8)
- [ ] Add `StatusQueryTimeoutSeconds` (default 5)
- [ ] Add `StatusRetryAttempts` (default 3)
- [ ] Read configuration items in code
- [ ] Use minimum value when configuration item is below minimum (e.g., polling interval < 5 seconds, use 5 seconds)

**文件**：
- `MaterialClient/appsettings.json`
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**相关需求**：
- Polling Configuration

---

### 任务 4.2：更新文档
**Priority**: Low
**Effort**: 1 hour
**Dependencies**: All tasks

**Description**:
Update project documentation to record sound column device status monitoring functionality.

**Acceptance Criteria**:
- [ ] Update `openspec/specs/sound-device-status/spec.md` (after archival)
- [ ] Update `docs/SDD.md` (if exists)
- [ ] Add code comments (XML documentation)
- [ ] Update README.md (if needed)

**文件**：
- `openspec/specs/sound-device-status/spec.md`
- `docs/SDD.md`
- Related code files

**相关需求**：
- All Requirements

---

## 依赖

### 外部依赖
- Sound column device remote API availability (`/api/devices/getDeviceBySN` endpoint)
- Sound column device network connection stability

### 内部依赖
- `ISettingsService` - Get sound column device configuration
- `IHttpClientFactory` - Create HTTP client
- `ISoundDeviceService` - Query device status
- `ReactiveUI` - Rx polling logic

### 技术依赖
- Refit 9.0.2 - HTTP client encapsulation
- System.Reactive 7.0.0-preview.1 - Rx polling
- System.Text.Json - JSON parsing

---

## 风险缓解

### Risk 1: API Format Does Not Match Expectations
**Risk Level**: Medium
**Mitigation**:
- Add test cases for various response formats in Task 1.4
- Use `try-catch` to catch JSON parsing exceptions
- Log actual response content for debugging

### Risk 2: Memory Leaks
**Risk Level**: High
**Mitigation**:
- Tasks 2.3 and 2.4 focus on testing resource release
- Use `dotMemory` or `Visual Studio Profiler` for verification
- Code review focuses on subscription lifecycle management

### Risk 3: UI Thread Update Errors
**Risk Level**: Medium
**Mitigation**:
- Use `ObserveOn(RxApp.MainThreadScheduler)` in Task 2.2
- Verify UI updates work normally in integration tests
- Log thread ID for debugging

### Risk 4: Polling Frequency Too High Affects Performance
**Risk Level**: Low
**Mitigation**:
- Polling interval >= 5 seconds, default 8 seconds
- Support configuration for easy adjustment
- HTTP timeout set to 5 seconds to avoid long blocking

---

## 时间线

**Total Effort**: 2-3 working days

### Day 1 - API and Data Layer
- Task 1.1 - 1.4: Create DTO, extend API, implement service, write unit tests

### Day 2 - ViewModel and State Management
- Task 2.1 - 2.4: Extend ViewModel, implement polling, resource disposal, memory leak tests

### Day 3 - UI Integration and Testing
- Task 3.1 - 3.3: Modify XAML, integration testing, regression testing

### Optional - Configuration and Documentation
- Task 4.1 - 4.2: Add configuration, update documentation

---

## 完成定义

**任务完成标准**：
- [ ] All code reviews passed
- [ ] All unit tests passed
- [ ] All integration tests passed
- [ ] Memory leak tests passed
- [ ] Regression tests passed
- [ ] Documentation updated
- [ ] Code merged to main branch

**功能完成标准**：
- [ ] Status bar displays sound column device status
- [ ] Status updates automatically every 8 seconds
- [ ] Colors and text correctly reflect device status
- [ ] Status indicator hidden when device is disabled
- [ ] No memory leaks when window is closed
- [ ] Network exceptions do not affect other functionality
