# Proposal: Sound Device Test Feature

## Metadata

- **Change ID**: `sound-device-test-feature`
- **Title**: Sound Device Test Feature
- **Status**: ExecutionCompleted
- **Created**: 2025-02-02
- **Author**: AI Assistant

## Overview

Add a quick test capability for sound devices in the MaterialClient application. Currently, users can enable/disable sound devices in SettingsWindow but have no way to verify if the device is working correctly without going through a complete weighing workflow. This proposal adds a test button in the settings UI that triggers a fixed audio test phrase "音柱测试" to validate sound device functionality.

## Problem Statement

### Current Limitations

1. **Configuration Validation Difficulty**: After enabling sound device in SettingsWindow, users cannot immediately verify if the device configuration is correct (IP, SN, volume settings)

2. **Inefficient Troubleshooting**: Sound device failures can only be discovered during actual weighing operations, requiring a complete business flow to reproduce issues

3. **Poor User Experience**: Lack of immediate feedback mechanism creates uncertainty about device status after configuration changes

### Impact

- System administrators waste time completing full weighing workflows just to test sound device changes
- Configuration errors (wrong IP, serial number, network issues) are discovered late
- No way to verify device functionality during initial setup or after hardware replacement

## Proposed Solution

### 1. UI Layer Changes (SettingsWindow)

Add a **Test Button** in the sound device configuration section of SettingsWindow:

- **Location**: Adjacent to the sound device enable/disable toggle
- **Functionality**: Clicking triggers the sound device test
- **Status Feedback**: Display test in-progress and final result (success/failure/timeout)
- **Availability**: Only enabled when sound device is enabled and properly configured

### 2. Service Layer Implementation (ISoundDeviceService)

Add a new test method to the `ISoundDeviceService` interface:

```csharp
Task PlayTextV2TestAsync(CancellationToken cancellationToken);
```

**Implementation Requirements**:

- **Fixed Test Text**: Always use "音柱测试" (Sound Column Test)
- **Cancellation Support**: Support operation cancellation via `CancellationToken`
- **Exception Handling**: Catch and log device exceptions, provide user-friendly error messages
- **Implementation Location**: `MaterialClient.Common/Services/SoundDeviceService.cs`

### 3. MVVM Integration

Follow project MVVM architecture patterns:

- **ViewModel**: Add test command in `SettingsWindowViewModel.cs`
- **ReactiveUI**: Use `ReactiveCommand` for test button command binding
- **State Management**: Observable properties to reflect test status
- **Dependency Injection**: Inject `ISoundDeviceService` through constructor

### 4. Error Handling & Logging

- **Serilog**: Log test operation start, success, failure events
- **User Feedback**: Display test result in UI (success/failure/timeout message)
- **Timeout Mechanism**: Configure reasonable timeout to avoid long blocking (default: 30 seconds)

## Scope

### In Scope

- Add test button UI in SettingsWindow sound device section
- Implement `PlayTextV2TestAsync` method in `SoundDeviceService`
- Add test command in `SettingsWindowViewModel` with ReactiveUI
- Implement test status feedback in UI (in-progress, success, failure)
- Add logging for test operations
- Handle cancellation tokens properly

### Out of Scope

- Sound device discovery or auto-configuration features
- Advanced diagnostics (network connectivity tests, volume calibration)
- Multiple test phrases or custom test text
- Test history or statistics
- Sound device configuration validation beyond existing `IsValid()` check

## Impact Analysis

### Functional Impact

- **New Feature**: Quick sound device testing capability
- **Modified Files**:
  - `Views/SettingsWindow.axaml` - Add test button UI
  - `ViewModels/SettingsWindowViewModel.cs` - Add test command and status properties
  - `Services/ISoundDeviceService.cs` - Add test method to interface
  - `Services/SoundDeviceService.cs` - Implement test method

### Technical Impact

- **API Surface**: New method added to `ISoundDeviceService` interface (backward compatible)
- **Dependencies**: No new external dependencies required
- **Memory Management**: Must ensure proper disposal of HttpClient instances in test method
- **Threading**: Async/await pattern with proper cancellation token handling

### Performance Impact

- Minimal - test operation is short-lived (~30 seconds max)
- No impact on existing weighing workflows
- Negligible memory footprint

### Security Impact

- No security concerns - uses existing sound device API
- Test text is fixed and non-user-controlled
- Existing authentication/authorization applies

### User Experience Impact

- **Positive**: Immediate feedback on sound device configuration
- **Reduced friction**: Faster troubleshooting and validation
- **No disruption**: Test button is additive, doesn't change existing workflow

## Technical Constraints

### Architecture Constraints

- **MVVM Pattern**: Must follow existing MVVM architecture with View-ViewModel separation
- **ReactiveUI**: Use `ReactiveCommand` and `ReactiveObject` for command bindings
- **Dependency Injection**: Service already registered as singleton (`ISingletonDependency`)

### Platform Constraints

- **Target**: Windows x64 only (existing constraint due to HCNetSDK)
- **Runtime**: .NET 10.0
- **UI Framework**: Avalonia UI 11.3.9

### Code Style Constraints

- **Async Methods**: Must use `Async` suffix per project conventions
- **Nullable Reference Types**: Enabled throughout codebase
- **Source Generators**: Use `AutoConstructor` and `ReactiveUI.SourceGenerators`
- **Naming**: Private fields use `_camelCase`, async methods end with `Async`

### Memory Management Constraints

- **Rx Subscriptions**: Proper disposal is critical - use `DisposeWith()` or `using` blocks
- **HttpClient**: Must be properly disposed after use in test method
- **Cancellation Tokens**: Ensure proper registration to avoid leaks

## Dependencies

### Internal Dependencies

- `ISoundDeviceService` - Existing service for sound device operations
- `ISettingsService` - Access to sound device configuration
- `IHttpClientFactory` - For creating HTTP clients (or direct HttpClient instantiation per existing pattern)

### External Dependencies

- System.Reactive (Rx.NET) - ReactiveUI infrastructure
- Serilog - Logging framework
- System.Text.Json - JSON serialization

## Alternatives Considered

### Alternative 1: Reuse `PlayTextV2Async` Method

**Approach**: Call existing `PlayTextV2Async("音柱测试")` from ViewModel

**Pros**:
- No interface changes needed
- Simpler implementation

**Cons**:
- Tight coupling - ViewModel directly calls business logic method
- No dedicated semantic for "test" vs "production" usage
- Harder to add test-specific behavior later (e.g., different logging, timeout)

**Decision**: Rejected - Better to have explicit test method for clarity and future extensibility

### Alternative 2: Add Test Button in Main Window

**Approach**: Put sound device test button in main application window instead of Settings

**Pros**:
- More accessible during normal operation

**Cons**:
- Clutters main UI
- Test is primarily a configuration validation task, not operational task
- Settings is the logical place for device configuration tools

**Decision**: Rejected - SettingsWindow is the appropriate location for configuration-related testing

### Alternative 3: Use Refit for Test Method

**Approach**: Create new Refit interface method for test endpoint

**Pros**:
- Consistent with existing `ISoundDeviceApi` pattern

**Cons**:
- Sound device doesn't have a dedicated "test" endpoint
- Test uses same play API as production (`PlayTextV2Async`)
- Would require mock endpoint or unnecessary abstraction

**Decision**: Rejected - Test should reuse existing play API with fixed text

## Migration Plan

### Backward Compatibility

- **Fully Backward Compatible**: New method addition doesn't break existing code
- **No Database Changes**: No schema migrations required
- **No Configuration Changes**: No new settings required

### Deployment Considerations

- **Zero Downtime**: Feature can be deployed without service interruption
- **No Migration Steps**: Drop-in replacement/upgrade
- **User Training**: Intuitive - test button is self-explanatory

## Testing Strategy

### Unit Tests

- Test `PlayTextV2TestAsync` method with mocked `ISettingsService`
- Test cancellation token handling
- Test exception handling and logging
- Verify fixed test text is always used

### Integration Tests

- Test end-to-end flow with real sound device hardware
- Verify UI command binding and status updates
- Test timeout behavior
- Test with invalid configuration (device disabled, invalid IP)

### Manual Testing

- Enable sound device, click test button, verify audio plays
- Test with sound device disabled - button should be disabled
- Test with invalid configuration - should show error message
- Test cancellation - close window during test should not leak resources

## Success Criteria

1. **Functional**:
   - Test button appears in SettingsWindow sound device section
   - Clicking test button plays "音柱测试" through configured sound device
   - UI shows appropriate feedback (testing, success, error)
   - Test only works when sound device is enabled and configured

2. **Technical**:
   - No memory leaks (verified with long-running test)
   - Proper cancellation token handling
   - All exceptions caught and logged
   - Code follows project MVVM and ReactiveUI patterns

3. **User Experience**:
   - Test completes within 30 seconds
   - Clear feedback on test result
   - No UI freezes during async operation

## Risks & Mitigations

### Risk 1: Sound Device Hardware Unavailability

**Risk**: Test may fail if no physical sound device is available during development/testing

**Mitigation**:
- Create mock implementation of `ISoundDeviceService` for unit tests
- Use integration test environment with real hardware for validation
- Log clear error messages when device is unreachable

### Risk 2: Memory Leaks in Rx Subscriptions

**Risk**: Improper disposal of ReactiveUI command subscriptions could cause memory leaks

**Mitigation**:
- Follow existing patterns in `SettingsWindowViewModel` for command disposal
- Use `DisposeWith()` pattern for subscription management
- Add memory leak test following project's `AttendedWeighingServiceMemoryLeakTests` pattern

### Risk 3: UI Thread Blocking

**Risk**: Long-running test operation could freeze UI if not properly awaited

**Mitigation**:
- Use async/await pattern throughout the call chain
- Ensure `ReactiveCommand` is properly configured for async operations
- Configure reasonable timeout (30 seconds)

## Open Questions

None at this time.

## References

- `openspec/project.md` - Project architecture and conventions
- `MaterialClient.Common/Services/SoundDeviceService.cs` - Existing sound device implementation
- `MaterialClient/ViewModels/SettingsWindowViewModel.cs` - Settings window ViewModel
- `MaterialClient/Views/SettingsWindow.axaml` - Settings window UI
