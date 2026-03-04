# sound-device-status 能力规范

## 目的

提供音柱设备在线状态监控能力，在有人值守称重窗口的状态栏中展示音柱设备的实时工作状态（在线/离线/任务中/断电），提升系统可观测性与运维效率。

## 新增需求

### 需求：设备状态轮询

The system SHALL periodically poll sound column device online status and update status bar display.

#### 场景：正常轮询状态更新
- **给定** Sound column device is enabled and configuration is valid
- **且** System has started and entered attended weighing window
- **则**系统应： start periodic timer with interval of 8 seconds (configurable)
- **且** Every 8 seconds, call `ISoundDeviceService.IsOnlineAsync()` to query device status
- **且** Update status bar display based on response status code

#### Scenario: Do not start polling when device is disabled
- **给定** Sound column device is not enabled (`SoundDeviceSettings.Enabled = false`)
- **当** System starts attended weighing window
- **则**系统应： NOT start timer
- **且** Status bar does not show sound column device status indicator

#### Scenario: Return offline status when configuration is invalid
- **给定** Sound column device is enabled but configuration is invalid (missing `SoundSN`, `SoundIP`, or `LocalIP`)
- **当** Timer calls `IsOnlineAsync()`
- **则**系统应： return `false` (offline)
- **且** Log warning
- **且** Status bar displays "Offline" status

#### Scenario: Retry and show offline when network exception occurs
- **给定** Sound column device network connection is interrupted
- **当** Timer calls `IsOnlineAsync()`
- **则**系统应： catch `HttpRequestException` or `TaskCanceledException`
- **且** Return `false` (offline)
- **且** Log error
- **且** Use Rx `Retry()` to retry up to 3 times
- **且** Status bar displays "Offline" status

#### Scenario: Stop polling when window is closed
- **给定** Timer is running
- **当** User closes attended weighing window
- **则**系统应： release polling subscription (`Dispose()`)
- **且** Stop all timers
- **且** Release `BehaviorSubject` resources
- **且** No memory leaks occur

### 需求：设备状态 API 集成

The system SHALL query sound column device status through remote API and map response to device online status.

#### Scenario: Call remote API to query device status
- **给定** Sound column device serial number is `"020021EA63AC"`
- **且** Device IP address is `"192.168.1.100"`
- **当** `SoundDeviceService.IsOnlineAsync()` is called
- **则**系统应： build device serial number format as `"ls20://020021EA63AC"`
- **且** Create HTTP client, BaseURL is `"http://192.168.1.100:8888"`
- **且** Call `GET /api/devices/getDeviceBySN?type=req&app=ls20&sn=ls20://020021EA63AC`
- **且** Set timeout to 5 seconds

#### Scenario: Parse online status response
- **给定** Remote API returns response: `{ "status": 1, "tasks": [] }`
- **当** `IsOnlineAsync()` receives response
- **则**系统应： parse JSON to `SoundDeviceStatusDto`
- **且** Determine `status == 1 || status == 2` as online
- **且** Return `true`

#### Scenario: Parse offline status response
- **给定** Remote API returns response: `{ "status": 0, "tasks": [] }`
- **当** `IsOnlineAsync()` receives response
- **则**系统应： parse JSON to `SoundDeviceStatusDto`
- **且** Determine `status != 1 && status != 2` as offline
- **且** Return `false`

#### Scenario: Parse in-task status response
- **给定** Remote API returns response: `{ "status": 2, "tasks": [...] }`
- **当** `IsOnlineAsync()` receives response
- **则**系统应： parse JSON to `SoundDeviceStatusDto`
- **且** Determine `status == 2` as online (in-task still considered online)
- **且** Return `true`
- **且** Log debug: "Device is busy with tasks"

#### Scenario: Parse power-off status response
- **给定** Remote API returns response: `{ "status": 3, "tasks": [] }`
- **当** `IsOnlineAsync()` receives response
- **则**系统应： parse JSON to `SoundDeviceStatusDto`
- **且** Determine `status == 3` as offline (power-off considered offline)
- **且** Return `false`
- **且** Log warning: "Device is powered off"

### 需求：状态栏 UI 显示

The system SHALL display sound column device status indicator in the attended weighing window status bar, using colors and text to identify device status.

#### Scenario: Display online status
- **给定** Sound column device is online (status code 1)
- **当** Status bar renders device status indicator
- **则**系统应： display green dot (`#10B981`)
- **且** Display text "Sound"
- **且** Display status text "Online" (green font)

#### Scenario: Display offline status
- **给定** Sound column device is offline (status code 0)
- **当** Status bar renders device status indicator
- **则**系统应： display gray dot (`#9CA3AF`)
- **且** Display text "Sound"
- **且** Display status text "Offline" (gray font)

#### Scenario: Display in-task status
- **给定** Sound column device is in-task (status code 2)
- **当** Status bar renders device status indicator
- **则**系统应： display yellow dot (`#F59E0B`)
- **且** Display text "Sound"
- **且** Display status text "In Task" (yellow font)

#### Scenario: Display power-off status
- **给定** Sound column device is powered off (status code 3)
- **当** Status bar renders device status indicator
- **则**系统应： display red dot (`#EF4444`)
- **且** Display text "Sound"
- **且** Display status text "Power Off" (red font)

#### Scenario: Hide status indicator when device is disabled
- **给定** Sound column device is not enabled
- **当** Status bar renders
- **则**系统应： NOT display sound column device status indicator
- **且** Other device status indicators display normally

#### Scenario: Automatically refresh UI when status updates
- **给定** Status bar currently displays sound column device "Offline" status
- **当** Timer receives new device status (online)
- **则**系统应： update UI on main thread
- **且** Change dot color from gray to green
- **且** Change status text from "Offline" to "Online"
- **且** Trigger `RaisePropertyChanged` notification

### 需求：内存泄漏预防

The system SHALL properly manage Rx subscription lifecycle to prevent memory leaks.

#### Scenario: Polling subscription properly released
- **给定** `AttendedWeighingViewModel` created and polling subscription started
- **当** `ViewModel.Dispose()` is called
- **则**系统应： call `_statusPollingDisposable.Dispose()`
- **且** Call `_soundDeviceStatus.Dispose()`
- **且** All timers stop running
- **且** No event handler leaks

#### Scenario: No memory leaks after multiple open/close cycles
- **给定** User opens attended weighing window
- **且** Wait 80 seconds (simulate 10 polling cycles)
- **当** User closes window
- **则**系统应： release all resources
- **且** Repeat above operation 100 times
- **且** Memory usage shows no significant growth (< 50MB)
- **且** Verified using `dotMemory` or `Visual Studio Profiler`

#### Scenario: Subscription still releasable when exception occurs
- **给定** Timer is running and uncaught exception occurs
- **当** Exception is caught by Rx `Catch` operator
- **则**系统应： log error
- **且** Subscription remains active (not terminated by single exception)
- **且** `Dispose()` method can properly release subscription

### 需求：轮询配置

The system SHALL support adjusting polling parameters through configuration file.

#### Scenario: Use default polling interval
- **给定** `appsettings.json` does not configure polling interval
- **当** System starts timer
- **则**系统应： use default value 8 seconds

#### Scenario: Use custom polling interval
- **给定** `appsettings.json` configures `"SoundDevice:StatusPollingIntervalSeconds": 10`
- **当** System starts timer
- **则**系统应： use configured value 10 seconds

#### Scenario: Use minimum value when polling interval is less than minimum
- **给定** `appsettings.json` configures `"SoundDevice:StatusPollingIntervalSeconds": 2`
- **且** Minimum allowed value is 5 seconds
- **当** System starts timer
- **则**系统应： use minimum value 5 seconds
- **且** Log warning: "Polling interval too low, using minimum value"

## 修改的需求

*This change does not involve modifying existing requirements, only adding sound column device status monitoring functionality.*

## 已移除需求

*This change does not involve deleting existing requirements.*
