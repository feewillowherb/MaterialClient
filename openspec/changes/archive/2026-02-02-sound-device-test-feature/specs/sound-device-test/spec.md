# sound-device-test Specification

## Purpose

Provide sound device testing capability to allow users to quickly verify sound device configuration and functionality without requiring a complete weighing workflow.

## ADDED Requirements

### Requirement: Sound Device Test Button in Settings

The system SHALL provide a test button in the Settings window sound device configuration section to trigger audio test playback.

#### Scenario: Test button visibility and enablement
- **GIVEN** User opens Settings window
- **AND** Navigates to sound device configuration section
- **THEN** System SHALL display a "测试音响" (Test Sound) button
- **AND** Button SHALL be positioned after the volume configuration field
- **AND** Button SHALL be enabled only when sound device is enabled (`SoundDeviceEnabled = true`)
- **AND** Button SHALL be disabled when test is running

#### Scenario: Test button disabled when sound device is not enabled
- **GIVEN** User opens Settings window
- **AND** Sound device is not enabled (checkbox is unchecked)
- **THEN** Test button SHALL be disabled
- **AND** When user enables sound device (checks checkbox)
- **THEN** Test button SHALL become enabled

---

### Requirement: Sound Device Test Execution

The system SHALL play a fixed test phrase through the configured sound device when the test button is clicked.

#### Scenario: Successful test playback
- **GIVEN** Sound device is enabled and properly configured (valid IP, SN, LocalIP)
- **AND** Device is online and accessible
- **WHEN** User clicks the "测试音响" button
- **THEN** System SHALL call `ISoundDeviceService.PlayTextV2TestAsync()`
- **AND** Service SHALL play the fixed test text "音柱测试"
- **AND** System SHALL log information message: "Starting sound device test with text: 音柱测试"
- **AND** System SHALL display "测试成功" (Test Successful) message in UI
- **AND** Test button SHALL remain disabled during test execution
- **AND** Test button SHALL become enabled after test completes

#### Scenario: Test with invalid configuration
- **GIVEN** Sound device is enabled but configuration is invalid (missing or incorrect IP, SN, or LocalIP)
- **WHEN** User clicks the "测试音响" button
- **THEN** System SHALL attempt to call `PlayTextV2TestAsync()`
- **AND** Service SHALL detect invalid configuration
- **AND** Service SHALL log warning message with configuration details
- **AND** Service SHALL return without playing audio
- **AND** System SHALL display error message: "测试失败: [error details]"
- **AND** Test button SHALL become enabled after error

#### Scenario: Test with network error
- **GIVEN** Sound device is enabled and configuration is valid
- **AND** Device is offline or network is unreachable
- **WHEN** User clicks the "测试音响" button
- **THEN** System SHALL call `PlayTextV2TestAsync()`
- **AND** Service SHALL attempt HTTP request to device
- **AND** HTTP request SHALL fail with `HttpRequestException` or timeout
- **AND** Service SHALL log error with exception details
- **AND** Service SHALL rethrow exception to caller
- **AND** ViewModel SHALL catch exception
- **AND** System SHALL display user-friendly error message: "测试失败: 网络错误，请检查音响设备IP地址" or "测试失败: 请求超时，请检查音响设备是否在线"
- **AND** Test button SHALL become enabled after error

#### Scenario: Test cancellation and timeout
- **GIVEN** Sound device test is in progress
- **AND** Device is not responding
- **WHEN** 30 seconds elapse without successful response
- **THEN** HTTP request SHALL timeout
- **AND** Service SHALL log timeout error
- **AND** Service SHALL rethrow exception
- **AND** ViewModel SHALL display timeout error message
- **AND** Test button SHALL become enabled

#### Scenario: Fixed test text is always used
- **GIVEN** Sound device test is triggered
- **WHEN** `PlayTextV2TestAsync()` is called
- **THEN** Service SHALL use the constant test text "音柱测试"
- **AND** Test text SHALL NOT be configurable or user-defined
- **AND** Test text SHALL match the feature name for clarity

---

### Requirement: Test Status Feedback

The system SHALL provide visual feedback during and after the test execution to inform the user of test status.

#### Scenario: Display test in-progress status
- **GIVEN** User clicks the "测试音响" button
- **WHEN** Test execution begins
- **THEN** System SHALL set `IsSoundDeviceTestRunning = true`
- **AND** Test button SHALL become disabled (via binding to `IsSoundDeviceTestRunning`)
- **AND** Status message SHALL be cleared (`SoundDeviceTestResult = null`)

#### Scenario: Display test success result
- **GIVEN** Test execution completes successfully
- **WHEN** Audio playback finishes without errors
- **THEN** System SHALL set `SoundDeviceTestResult = "测试成功"`
- **AND** Status text block SHALL display the success message
- **AND** Status text SHALL be visible (via visibility converter)
- **AND** `IsSoundDeviceTestRunning` SHALL be set to `false`
- **AND** Test button SHALL become enabled

#### Scenario: Display test error result
- **GIVEN** Test execution fails with exception
- **WHEN** Exception is caught in ViewModel
- **THEN** System SHALL set `SoundDeviceTestResult` to error message containing exception details
- **AND** Status text block SHALL display the error message
- **AND** Error message SHALL be user-friendly (e.g., "测试失败: 网络错误，请检查音响设备IP地址")
- **AND** Error message SHALL be logged via Serilog
- **AND** `IsSoundDeviceTestRunning` SHALL be set to `false`
- **AND** Test button SHALL become enabled

#### Scenario: Status message persistence
- **GIVEN** Test has completed (success or failure)
- **AND** Status message is displayed
- **WHEN** User performs other actions in Settings window
- **THEN** Status message SHALL remain visible until next test is triggered
- **AND** Status message SHALL remain visible across property changes
- **AND** Status message SHALL only be cleared when new test starts

---

### Requirement: Service Layer Test Method

The system SHALL provide a dedicated test method in the sound device service to encapsulate test-specific logic.

#### Scenario: Dedicated test method exists in interface
- **GIVEN** `ISoundDeviceService` interface is defined
- **THEN** Interface SHALL contain method signature: `Task PlayTextV2TestAsync(CancellationToken cancellationToken = default)`
- **AND** Method SHALL have XML documentation comment explaining purpose
- **AND** Method SHALL accept cancellation token for async cancellation support

#### Scenario: Test method implementation uses fixed text
- **GIVEN** `SoundDeviceService` implements `ISoundDeviceService`
- **WHEN** `PlayTextV2TestAsync()` is called
- **THEN** Implementation SHALL define constant test text "音柱测试"
- **AND** Implementation SHALL log test start with test text
- **AND** Implementation SHALL call existing `PlayTextV2Async("音柱测试", cancellationToken)` method
- **AND** Implementation SHALL log successful completion
- **AND** Implementation SHALL catch, log, and rethrow exceptions

#### Scenario: Cancellation token is properly handled
- **GIVEN** `PlayTextV2TestAsync()` is called with cancellation token
- **WHEN** Cancellation is requested during test execution
- **THEN** Implementation SHALL pass cancellation token to underlying `PlayTextV2Async()` call
- **AND** Operation SHALL be cancelled gracefully
- **AND** System SHALL log cancellation if it occurs
- **AND** No resource leaks SHALL occur (HttpClient properly disposed)

---

### Requirement: MVVM Integration

The system SHALL follow MVVM architecture patterns with proper separation of concerns between View, ViewModel, and Service layers.

#### Scenario: ViewModel command for test functionality
- **GIVEN** `SettingsWindowViewModel` is instantiated
- **THEN** ViewModel SHALL have `TestSoundDevice` command property
- **AND** Command SHALL be implemented as `ReactiveCommand`
- **AND** Command SHALL execute async delegate `TestSoundDeviceAsync()`
- **AND** Command SHALL be decorated with `[ReactiveCommand]` attribute for source generation

#### Scenario: ViewModel has test status properties
- **GIVEN** `SettingsWindowViewModel` is instantiated
- **THEN** ViewModel SHALL have `IsSoundDeviceTestRunning` boolean property
- **AND** Property SHALL be decorated with `[Reactive]` attribute for change notification
- **AND** ViewModel SHALL have `SoundDeviceTestResult` string? property
- **AND** Property SHALL be decorated with `[Reactive]` attribute for change notification

#### Scenario: Service dependency injection
- **GIVEN** `SettingsWindowViewModel` is instantiated via dependency injection
- **THEN** Constructor SHALL accept `ISoundDeviceService` parameter
- **AND** Parameter SHALL be assigned to readonly field `_soundDeviceService`
- **AND** Field SHALL be used in `TestSoundDeviceAsync()` to call test method

#### Scenario: Command canExecute logic
- **GIVEN** `SettingsWindowViewModel` is initialized
- **THEN** `TestSoundDevice` command SHALL be created with canExecute observable
- **AND** Command SHALL only be executable when `SoundDeviceEnabled` is true
- **AND** Command SHALL use `this.WhenAnyValue(x => x.SoundDeviceEnabled)` observable
- **AND** When `SoundDeviceEnabled` changes, command executability SHALL update

#### Scenario: View binding to ViewModel
- **GIVEN** Settings window XAML is defined
- **THEN** Test button Command SHALL be bound to `{Binding TestSoundDevice}`
- **AND** Test button IsEnabled SHALL be bound to `{Binding IsSoundDeviceTestRunning, Converter=...}` (negated)
- **AND** Status TextBlock Text SHALL be bound to `{Binding SoundDeviceTestResult}`
- **AND** Status TextBlock Visibility SHALL be bound with converter to show when result is not null

---

### Requirement: Error Handling and Logging

The system SHALL handle all exceptions gracefully and provide comprehensive logging for debugging and monitoring.

#### Scenario: Service layer exception handling
- **GIVEN** `PlayTextV2TestAsync()` method is executing
- **WHEN** Exception occurs (network error, timeout, device error)
- **THEN** Method SHALL catch exception
- **AND** Method SHALL log error with full exception details via Serilog
- **AND** Method SHALL rethrow exception to caller
- **AND** Log message SHALL include context: "Sound device test failed"

#### Scenario: ViewModel layer exception handling
- **GIVEN** `TestSoundDeviceAsync()` command is executing
- **WHEN** `PlayTextV2TestAsync()` throws exception
- **THEN** ViewModel SHALL catch exception in catch block
- **AND** ViewModel SHALL log error with exception details
- **AND** ViewModel SHALL set `SoundDeviceTestResult` to user-friendly error message
- **AND** ViewModel SHALL ensure `IsSoundDeviceTestRunning` is set to false in finally block
- **AND** User SHALL see error message in UI

#### Scenario: Network error specific handling
- **GIVEN** Test execution encounters network connectivity issue
- **WHEN** `HttpRequestException` is thrown
- **THEN** ViewModel SHALL catch specific exception type
- **AND** ViewModel SHALL log error with exception details
- **AND** ViewModel SHALL display specific message: "测试失败: 网络错误，请检查音响设备IP地址"

#### Scenario: Timeout specific handling
- **GIVEN** Test execution times out (30 seconds elapsed)
- **WHEN** `TaskCanceledException` is thrown
- **THEN** ViewModel SHALL catch specific exception type
- **AND** ViewModel SHALL log timeout error
- **AND** ViewModel SHALL display specific message: "测试失败: 请求超时，请检查音响设备是否在线"

#### Scenario: Logging at all stages
- **GIVEN** Test operation is triggered
- **WHEN** Test starts
- **THEN** System SHALL log: "Starting sound device test with text: 音柱测试"
- **WHEN** Test succeeds
- **THEN** System SHALL log: "Sound device test succeeded"
- **WHEN** Test fails
- **THEN** System SHALL log error with exception details and context
- **AND** All logs SHALL use appropriate log levels (Information, Warning, Error)

---

### Requirement: Memory Management and Resource Disposal

The system SHALL properly manage resources and avoid memory leaks during test operations.

#### Scenario: HttpClient disposal in service layer
- **GIVEN** `PlayTextV2Async()` is called by test method
- **WHEN** HttpClient is created for HTTP request
- **THEN** HttpClient SHALL be wrapped in try-finally block
- **AND** HttpClient SHALL be disposed in finally block
- **AND** No socket leaks SHALL occur

#### Scenario: Rx subscription management
- **GIVEN** `TestSoundDevice` command is created with canExecute observable
- **WHEN** ViewModel is disposed or window is closed
- **THEN** Command subscriptions SHALL be properly released
- **AND** No memory leaks SHALL occur from Rx subscriptions
- **AND** Memory usage SHALL remain stable over repeated test executions

#### Scenario: ReactiveUI command lifecycle
- **GIVEN** `TestSoundDevice` command is created via `ReactiveCommand.CreateFromTask()`
- **WHEN** Command is executed multiple times
- **THEN** Each execution SHALL complete cleanly
- **AND** Command SHALL not accumulate state
- **AND** Memory usage SHALL remain constant

---

### Requirement: Test Operation Timeout

The system SHALL enforce a reasonable timeout for test operations to prevent indefinite blocking.

#### Scenario: HTTP request timeout
- **GIVEN** Test execution is in progress
- **AND** Sound device is not responding
- **WHEN** HTTP request does not complete within 30 seconds
- **THEN** HttpClient SHALL throw `TaskCanceledException` due to timeout
- **AND** Exception SHALL be caught and logged
- **AND** User SHALL see timeout error message
- **AND** Test button SHALL become enabled

#### Scenario: Overall operation timeout
- **GIVEN** Test includes multiple retry attempts (as per existing `PlayTextV2Async` logic)
- **AND** Each attempt has 30-second timeout
- **WHEN** All 8 retry attempts timeout
- **THEN** Total test duration SHALL not exceed approximately 240 seconds (8 × 30s)
- **AND** Operation SHALL complete with error
- **AND** UI SHALL remain responsive during execution

---

### Requirement: UI Thread Non-Blocking

The system SHALL maintain UI responsiveness during async test operations.

#### Scenario: Async/await pattern throughout call chain
- **GIVEN** User clicks test button
- **WHEN** `TestSoundDeviceAsync()` command executes
- **THEN** Method SHALL be marked as `async Task`
- **AND** `await` SHALL be used when calling `PlayTextV2TestAsync()`
- **AND** UI thread SHALL not be blocked during HTTP request
- **AND** Window SHALL remain draggable and responsive

#### Scenario: Status updates on UI thread
- **GIVEN** Test execution is in progress on background thread
- **WHEN** `IsSoundDeviceTestRunning` or `SoundDeviceTestResult` properties are set
- **THEN** ReactiveUI SHALL automatically marshal property changes to UI thread
- **AND** UI SHALL update immediately without `Dispatcher.Invoke`
- **AND** Button enablement and status text SHALL update synchronously

---

## MODIFIED Requirements

*None - This is a new feature with no modifications to existing requirements.*

---

## REMOVED Requirements

*None - This is a new feature with no removed requirements.*
