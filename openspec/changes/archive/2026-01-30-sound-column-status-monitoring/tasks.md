# Implementation Tasks

## Overview

This document lists all tasks for implementing the "Add Sound Column Device Status Monitoring to Status Bar" feature, ordered by priority and dependencies.

---

## Phase 1: API and Data Layer (Day 1)

### Task 1.1: Create DTO Class
**Priority**: High
**Effort**: 30 minutes
**Dependencies**: None

**Description**:
Create sound column device status response DTO.

**Acceptance Criteria**:
- [x] Create `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs`
- [x] Define `Status` property (`int`, JsonPropertyName "status")
- [x] Define `Tasks` property (`IList<DeviceTaskInfo>`, JsonPropertyName "tasks")
- [x] Create `DeviceTaskInfo` record (reserved field)

**Files**:
- `MaterialClient.Common/Api/Dtos/SoundDeviceStatusDto.cs`

**Related Requirements**:
- Device Status API Integration

---

### Task 1.2: Extend ISoundDeviceApi Interface
**Priority**: High
**Effort**: 30 minutes
**Dependencies**: Task 1.1

**Description**:
Add device status query interface to `ISoundDeviceApi`.

**Acceptance Criteria**:
- [x] Add `GetDeviceStatusAsync()` method to `ISoundDeviceApi`
- [x] Use Refit `[Get("/api/devices/getDeviceBySN")]` attribute
- [x] Define query parameters: `type`, `app`, `sn`
- [x] Return type is `Task<SoundDeviceStatusDto>`
- [x] Add XML comment documentation

**Files**:
- `MaterialClient.Common/Api/ISoundDeviceApi.cs`

**Related Requirements**:
- Device Status API Integration

---

### Task 1.3: Implement SoundDeviceService.IsOnlineAsync()
**Priority**: High
**Effort**: 2 hours
**Dependencies**: Task 1.1, Task 1.2

**Description**:
Implement device online status detection method in `SoundDeviceService`.

**Acceptance Criteria**:
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

**Files**:
- `MaterialClient.Common/Services/SoundDeviceService.cs`

**Related Requirements**:
- Device Status Polling
- Device Status API Integration

---

### Task 1.4: Write Unit Tests
**Priority**: Medium
**Effort**: 2 hours
**Dependencies**: Task 1.3

**Description**:
Write unit tests for `SoundDeviceService.IsOnlineAsync()`.

**Acceptance Criteria**:
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

**Files**:
- `MaterialClient.Common.Tests/Services/SoundDeviceServiceTests.cs`

**Related Requirements**:
- Device Status Polling
- Device Status API Integration

---

## Phase 2: ViewModel and State Management (Day 2)

### Task 2.1: Extend AttendedWeighingViewModel Fields
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

**Files**:
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**Related Requirements**:
- Status Bar UI Display

---

### Task 2.2: Implement Polling Logic
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

**Files**:
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**Related Requirements**:
- Device Status Polling
- Status Bar UI Display

---

### Task 2.3: Implement Resource Disposal
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

**Files**:
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**Related Requirements**:
- Memory Leak Prevention

---

### Task 2.4: Write Memory Leak Tests
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

**Files**:
- `MaterialClient.Common.Tests/ViewModels/AttendedWeighingViewModelMemoryLeakTests.cs`

**Related Requirements**:
- Memory Leak Prevention

---

## Phase 3: UI Integration (Day 3)

### Task 3.1: Modify AttendedWeighingWindow.axaml
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

**Files**:
- `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml`

**Related Requirements**:
- Status Bar UI Display

---

### Task 3.2: Integration Testing
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

**Files**:
- Manual testing checklist

**Related Requirements**:
- Device Status Polling
- Status Bar UI Display

---

### Task 3.3: Regression Testing
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

**Files**:
- Regression testing checklist

**Related Requirements**:
- All Requirements

---

## Phase 4: Configuration and Documentation (Optional)

### Task 4.1: Add Configuration Items
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

**Files**:
- `MaterialClient/appsettings.json`
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**Related Requirements**:
- Polling Configuration

---

### Task 4.2: Update Documentation
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

**Files**:
- `openspec/specs/sound-device-status/spec.md`
- `docs/SDD.md`
- Related code files

**Related Requirements**:
- All Requirements

---

## Dependencies

### External Dependencies
- Sound column device remote API availability (`/api/devices/getDeviceBySN` endpoint)
- Sound column device network connection stability

### Internal Dependencies
- `ISettingsService` - Get sound column device configuration
- `IHttpClientFactory` - Create HTTP client
- `ISoundDeviceService` - Query device status
- `ReactiveUI` - Rx polling logic

### Technical Dependencies
- Refit 9.0.2 - HTTP client encapsulation
- System.Reactive 7.0.0-preview.1 - Rx polling
- System.Text.Json - JSON parsing

---

## Risk Mitigation

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

## Timeline

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

## Definition of Done

**Task Completion Criteria**:
- [ ] All code reviews passed
- [ ] All unit tests passed
- [ ] All integration tests passed
- [ ] Memory leak tests passed
- [ ] Regression tests passed
- [ ] Documentation updated
- [ ] Code merged to main branch

**Feature Completion Criteria**:
- [ ] Status bar displays sound column device status
- [ ] Status updates automatically every 8 seconds
- [ ] Colors and text correctly reflect device status
- [ ] Status indicator hidden when device is disabled
- [ ] No memory leaks when window is closed
- [ ] Network exceptions do not affect other functionality
