# sound-device-status Capability Specification

## Purpose

Provide sound column device online status monitoring functionality, displaying real-time working status of sound column devices (online/offline/in-task/power-off) in the status bar of the attended weighing window, improving system observability and operational efficiency.

## ADDED Requirements

### Requirement: Device Status Polling

The system SHALL periodically poll sound column device online status and update status bar display.

#### Scenario: Normal polling status update
- **GIVEN** Sound column device is enabled and configuration is valid
- **AND** System has started and entered attended weighing window
- **THEN** System SHALL start periodic timer with interval of 8 seconds (configurable)
- **AND** Every 8 seconds, call `ISoundDeviceService.IsOnlineAsync()` to query device status
- **AND** Update status bar display based on response status code

#### Scenario: Do not start polling when device is disabled
- **GIVEN** Sound column device is not enabled (`SoundDeviceSettings.Enabled = false`)
- **WHEN** System starts attended weighing window
- **THEN** System SHALL NOT start timer
- **AND** Status bar does not show sound column device status indicator

#### Scenario: Return offline status when configuration is invalid
- **GIVEN** Sound column device is enabled but configuration is invalid (missing `SoundSN`, `SoundIP`, or `LocalIP`)
- **WHEN** Timer calls `IsOnlineAsync()`
- **THEN** System SHALL return `false` (offline)
- **AND** Log warning
- **AND** Status bar displays "Offline" status

#### Scenario: Retry and show offline when network exception occurs
- **GIVEN** Sound column device network connection is interrupted
- **WHEN** Timer calls `IsOnlineAsync()`
- **THEN** System SHALL catch `HttpRequestException` or `TaskCanceledException`
- **AND** Return `false` (offline)
- **AND** Log error
- **AND** Use Rx `Retry()` to retry up to 3 times
- **AND** Status bar displays "Offline" status

#### Scenario: Stop polling when window is closed
- **GIVEN** Timer is running
- **WHEN** User closes attended weighing window
- **THEN** System SHALL release polling subscription (`Dispose()`)
- **AND** Stop all timers
- **AND** Release `BehaviorSubject` resources
- **AND** No memory leaks occur

### Requirement: Device Status API Integration

The system SHALL query sound column device status through remote API and map response to device online status.

#### Scenario: Call remote API to query device status
- **GIVEN** Sound column device serial number is `"020021EA63AC"`
- **AND** Device IP address is `"192.168.1.100"`
- **WHEN** `SoundDeviceService.IsOnlineAsync()` is called
- **THEN** System SHALL build device serial number format as `"ls20://020021EA63AC"`
- **AND** Create HTTP client, BaseURL is `"http://192.168.1.100:8888"`
- **AND** Call `GET /api/devices/getDeviceBySN?type=req&app=ls20&sn=ls20://020021EA63AC`
- **AND** Set timeout to 5 seconds

#### Scenario: Parse online status response
- **GIVEN** Remote API returns response: `{ "status": 1, "tasks": [] }`
- **WHEN** `IsOnlineAsync()` receives response
- **THEN** System SHALL parse JSON to `SoundDeviceStatusDto`
- **AND** Determine `status == 1 || status == 2` as online
- **AND** Return `true`

#### Scenario: Parse offline status response
- **GIVEN** Remote API returns response: `{ "status": 0, "tasks": [] }`
- **WHEN** `IsOnlineAsync()` receives response
- **THEN** System SHALL parse JSON to `SoundDeviceStatusDto`
- **AND** Determine `status != 1 && status != 2` as offline
- **AND** Return `false`

#### Scenario: Parse in-task status response
- **GIVEN** Remote API returns response: `{ "status": 2, "tasks": [...] }`
- **WHEN** `IsOnlineAsync()` receives response
- **THEN** System SHALL parse JSON to `SoundDeviceStatusDto`
- **AND** Determine `status == 2` as online (in-task still considered online)
- **AND** Return `true`
- **AND** Log debug: "Device is busy with tasks"

#### Scenario: Parse power-off status response
- **GIVEN** Remote API returns response: `{ "status": 3, "tasks": [] }`
- **WHEN** `IsOnlineAsync()` receives response
- **THEN** System SHALL parse JSON to `SoundDeviceStatusDto`
- **AND** Determine `status == 3` as offline (power-off considered offline)
- **AND** Return `false`
- **AND** Log warning: "Device is powered off"

### Requirement: Status Bar UI Display

The system SHALL display sound column device status indicator in the attended weighing window status bar, using colors and text to identify device status.

#### Scenario: Display online status
- **GIVEN** Sound column device is online (status code 1)
- **WHEN** Status bar renders device status indicator
- **THEN** System SHALL display green dot (`#10B981`)
- **AND** Display text "Sound"
- **AND** Display status text "Online" (green font)

#### Scenario: Display offline status
- **GIVEN** Sound column device is offline (status code 0)
- **WHEN** Status bar renders device status indicator
- **THEN** System SHALL display gray dot (`#9CA3AF`)
- **AND** Display text "Sound"
- **AND** Display status text "Offline" (gray font)

#### Scenario: Display in-task status
- **GIVEN** Sound column device is in-task (status code 2)
- **WHEN** Status bar renders device status indicator
- **THEN** System SHALL display yellow dot (`#F59E0B`)
- **AND** Display text "Sound"
- **AND** Display status text "In Task" (yellow font)

#### Scenario: Display power-off status
- **GIVEN** Sound column device is powered off (status code 3)
- **WHEN** Status bar renders device status indicator
- **THEN** System SHALL display red dot (`#EF4444`)
- **AND** Display text "Sound"
- **AND** Display status text "Power Off" (red font)

#### Scenario: Hide status indicator when device is disabled
- **GIVEN** Sound column device is not enabled
- **WHEN** Status bar renders
- **THEN** System SHALL NOT display sound column device status indicator
- **AND** Other device status indicators display normally

#### Scenario: Automatically refresh UI when status updates
- **GIVEN** Status bar currently displays sound column device "Offline" status
- **WHEN** Timer receives new device status (online)
- **THEN** System SHALL update UI on main thread
- **AND** Change dot color from gray to green
- **AND** Change status text from "Offline" to "Online"
- **AND** Trigger `RaisePropertyChanged` notification

### Requirement: Memory Leak Prevention

The system SHALL properly manage Rx subscription lifecycle to prevent memory leaks.

#### Scenario: Polling subscription properly released
- **GIVEN** `AttendedWeighingViewModel` created and polling subscription started
- **WHEN** `ViewModel.Dispose()` is called
- **THEN** System SHALL call `_statusPollingDisposable.Dispose()`
- **AND** Call `_soundDeviceStatus.Dispose()`
- **AND** All timers stop running
- **AND** No event handler leaks

#### Scenario: No memory leaks after multiple open/close cycles
- **GIVEN** User opens attended weighing window
- **AND** Wait 80 seconds (simulate 10 polling cycles)
- **WHEN** User closes window
- **THEN** System SHALL release all resources
- **AND** Repeat above operation 100 times
- **AND** Memory usage shows no significant growth (< 50MB)
- **AND** Verified using `dotMemory` or `Visual Studio Profiler`

#### Scenario: Subscription still releasable when exception occurs
- **GIVEN** Timer is running and uncaught exception occurs
- **WHEN** Exception is caught by Rx `Catch` operator
- **THEN** System SHALL log error
- **AND** Subscription remains active (not terminated by single exception)
- **AND** `Dispose()` method can properly release subscription

### Requirement: Polling Configuration

The system SHALL support adjusting polling parameters through configuration file.

#### Scenario: Use default polling interval
- **GIVEN** `appsettings.json` does not configure polling interval
- **WHEN** System starts timer
- **THEN** System SHALL use default value 8 seconds

#### Scenario: Use custom polling interval
- **GIVEN** `appsettings.json` configures `"SoundDevice:StatusPollingIntervalSeconds": 10`
- **WHEN** System starts timer
- **THEN** System SHALL use configured value 10 seconds

#### Scenario: Use minimum value when polling interval is less than minimum
- **GIVEN** `appsettings.json` configures `"SoundDevice:StatusPollingIntervalSeconds": 2`
- **AND** Minimum allowed value is 5 seconds
- **WHEN** System starts timer
- **THEN** System SHALL use minimum value 5 seconds
- **AND** Log warning: "Polling interval too low, using minimum value"

## MODIFIED Requirements

*This change does not involve modifying existing requirements, only adding sound column device status monitoring functionality.*

## REMOVED Requirements

*This change does not involve deleting existing requirements.*
