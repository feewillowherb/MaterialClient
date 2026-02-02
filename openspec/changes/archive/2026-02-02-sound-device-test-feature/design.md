# Design Document: Sound Device Test Feature

## Overview

This document describes the technical design and architectural decisions for implementing the sound device test feature in MaterialClient.

## Architecture Context

### Existing Sound Device Architecture

The current sound device implementation follows this architecture:

```
┌─────────────────────────────────────────────────────────────┐
│                    SettingsWindow (View)                     │
│  - Sound device configuration UI (checkbox, IP, SN, volume) │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Data Binding
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SettingsWindowViewModel (ViewModel)             │
│  - Sound device settings properties                         │
│  - Save/Cancel commands                                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Service Calls
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ISoundDeviceService (Service Interface)         │
│  - PlayTextAsync(text, cancellationToken)                   │
│  - PlayTextV2Async(text, cancellationToken)                 │
│  - IsOnlineAsync()                                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Implementation
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SoundDeviceService (Service)                    │
│  - HTTP calls to sound device API                           │
│  - TTS URI construction                                     │
│  - Retry logic (8 attempts)                                 │
│  - Logging via Serilog                                      │
└─────────────────────────────────────────────────────────────┘
```

### New Components

This proposal adds the following components (highlighted in bold):

```
┌─────────────────────────────────────────────────────────────┐
│                    SettingsWindow (View)                     │
│  - Sound device configuration UI                            │
│  + Test button                                              │
│  + Status display                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SettingsWindowViewModel (ViewModel)             │
│  - Sound device settings properties                         │
│  - Save/Cancel commands                                     │
│  + IsSoundDeviceTestRunning property                        │
│  + SoundDeviceTestResult property                           │
│  + TestSoundDevice command                                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ISoundDeviceService (Service Interface)         │
│  - PlayTextAsync(text, cancellationToken)                   │
│  - PlayTextV2Async(text, cancellationToken)                 │
│  - IsOnlineAsync()                                          │
│  + PlayTextV2TestAsync(cancellationToken)                   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SoundDeviceService (Service)                    │
│  - HTTP calls to sound device API                           │
│  - TTS URI construction                                     │
│  - Retry logic (8 attempts)                                 │
│  - Logging via Serilog                                      │
│  + PlayTextV2TestAsync implementation                       │
└─────────────────────────────────────────────────────────────┘
```

## Design Decisions

### Decision 1: Dedicated Test Method vs. Reusing PlayTextV2Async

**Problem**: Should we create a dedicated `PlayTextV2TestAsync` method or call `PlayTextV2Async("音柱测试")` directly from the ViewModel?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) Create dedicated test method | • Clear semantic separation<br>• Easier to add test-specific logic later<br>• Better testability<br>• Consistent with service-oriented architecture | • Slightly more code |
| B) Call PlayTextV2Async from ViewModel | • Less code<br>• No interface changes | • Tight coupling<br>• Harder to extend<br>• Mixes concerns |

**Decision**: **Option A** - Create dedicated `PlayTextV2TestAsync` method

**Rationale**:
1. **Separation of Concerns**: ViewModel should not know about test-specific implementation details
2. **Future Extensibility**: May need test-specific behavior (different timeout, logging, diagnostics)
3. **Testability**: Easier to mock and test in isolation
4. **Semantic Clarity**: "Test" has different meaning than "Play" in business logic
5. **Consistency**: Follows existing service-oriented pattern

**Implementation**:
```csharp
// In SoundDeviceService
public async Task PlayTextV2TestAsync(CancellationToken cancellationToken = default)
{
    const string testText = "音柱测试";
    _logger?.LogInformation("Starting sound device test with text: {TestText}", testText);

    try
    {
        await PlayTextV2Async(testText, cancellationToken);
        _logger?.LogInformation("Sound device test completed successfully");
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Sound device test failed");
        throw;
    }
}
```

---

### Decision 2: Test Text Content

**Problem**: What text should be used for the sound device test?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) Fixed text: "音柱测试" | • Simple and clear<br>• Self-documenting<br>• Short (reduces test time) | • Less informative than full sentence |
| B) Fixed text: "音响设备测试中" | • More descriptive | • Longer (increases test time) |
| C) Configurable test text | • Maximum flexibility | • Adds complexity (UI, settings)<br>• Overkill for this use case |
| D) Random test phrase | • Tests variety | • Confusing for users<br>• Hard to document |

**Decision**: **Option A** - Fixed text: "音柱测试"

**Rationale**:
1. **Simplicity**: No additional UI or configuration needed
2. **Clarity**: Users immediately understand what's being tested
3. **Efficiency**: Short text minimizes test duration
4. **Consistency**: Same test every time makes troubleshooting easier
5. **Self-Documenting**: Text name matches feature name

---

### Decision 3: Test Status Feedback Mechanism

**Problem**: How should test status be communicated to the user?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) TextBlock with status message | • Simple to implement<br>• Clear feedback | • Uses additional screen space |
| B) Button text change during test | • No extra space needed | • Less informative<br>• Harder to show error details |
| C) Progress dialog | • Very explicit | • Overkill for quick test<br>• Blocks UI |
| D) Toast notification | • Non-blocking<br>• Modern UX | • May be missed<br>• Requires toast infrastructure |

**Decision**: **Option A** - TextBlock with status message

**Rationale**:
1. **Simplicity**: Fits existing Avalonia UI patterns
2. **Information Density**: Can show detailed error messages
3. **Visibility**: Always visible during and after test
4. **Consistency**: Similar patterns exist elsewhere in SettingsWindow
5. **No New Infrastructure**: Leverages existing binding mechanisms

**Implementation**:
```csharp
// ViewModel properties
[Reactive] private bool _isSoundDeviceTestRunning = false;
[Reactive] private string? _soundDeviceTestResult = null;

// Command
[ReactiveCommand]
private async Task TestSoundDeviceAsync()
{
    try
    {
        IsSoundDeviceTestRunning = true;
        SoundDeviceTestResult = null;

        await _soundDeviceService.PlayTextV2TestAsync(CancellationToken.None);

        SoundDeviceTestResult = "测试成功";
    }
    catch (Exception ex)
    {
        SoundDeviceTestResult = $"测试失败: {ex.Message}";
    }
    finally
    {
        IsSoundDeviceTestRunning = false;
    }
}
```

```xml
<!-- View -->
<Button Content="测试音响"
        Command="{Binding TestSoundDevice}"
        IsEnabled="{Binding IsSoundDeviceTestRunning, Converter={x:Static Converters:BoolNegationConverter.Instance}}" />

<TextBlock Text="{Binding SoundDeviceTestResult}"
           Visibility="{Binding SoundDeviceTestResult, Converter={x:Static Converters:StringNotNullToVisibilityConverter.Instance}}" />
```

---

### Decision 4: Command Enablement Logic

**Problem**: When should the test button be enabled?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) Enable only when SoundDeviceEnabled is true | • Clear logic<br>• Prevents invalid calls | • May try to test with incomplete config |
| B) Enable when SoundDeviceEnabled AND config is valid | • More defensive | • Requires reactive validation<br>• More complex |
| C) Always enable, show error on test | • Simplest | • Poor UX<br>• Wastes user time |

**Decision**: **Option A** - Enable only when `SoundDeviceEnabled` is true

**Rationale**:
1. **Simplicity**: Straightforward reactive binding
2. **Consistency**: Matches existing patterns (e.g., other features use enable/disable toggles)
3. **Good Enough**: Existing `PlayTextV2Async` already validates config and returns gracefully
4. **Performance**: Avoids reactive validation overhead

**Implementation**:
```csharp
// In ViewModel constructor
TestSoundDevice = ReactiveCommand.CreateFromTask(
    TestSoundDeviceAsync,
    this.WhenAnyValue(x => x.SoundDeviceEnabled).Select(enabled => enabled)
);
```

**Note**: If validation issues arise, we can enhance to Option B later without breaking changes.

---

### Decision 5: Cancellation Token Handling

**Problem**: Should the test command support cancellation? If so, how?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) Use CancellationToken.None | • Simple<br>- Predictable 30s timeout | • Can't cancel long-running test |
| B) Use CancellationToken from command | • Allows cancellation | • More complex<br>• Need cancellation token source |
| C) Add Cancel button pair | • Very explicit | • Clutters UI<br>• Overkill for 30s operation |

**Decision**: **Option A** - Use `CancellationToken.None` initially, document for future enhancement

**Rationale**:
1. **Simplicity**: 30-second timeout is reasonable for test operation
2. **No UI Clutter**: No need for cancel button
3. **Sufficient**: Users can close Settings window to "cancel" if needed
4. **Future Extensibility**: Can add cancellation token support later without breaking changes

**Future Enhancement Path**:
If cancellation becomes important:
1. Add `CancellationTokenSource` field to ViewModel
2. Create cancel command that calls `cts.Cancel()`
3. Pass `cts.Token` to `PlayTextV2TestAsync`
4. Ensure proper disposal in `Dispose()` pattern

---

### Decision 6: Error Handling Strategy

**Problem**: How should test errors be handled and communicated?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) Catch all exceptions, show in UI | • User-friendly<br>• No crashes | • May hide programming errors |
| B) Let exceptions propagate | • Fail-fast<br>• Easier debugging | • Poor UX<br>• May crash app |
| C) Log and rethrow | • Best of both worlds | • Requires global error handler |

**Decision**: **Option A** - Catch all exceptions, show in UI + log

**Rationale**:
1. **User Experience**: Test operation is non-critical, should never crash
2. **Debugging**: Serilog captures full exception details
3. **Consistency**: Matches existing `PlayTextV2Async` error handling pattern
4. **Safety**: Network errors, timeouts, device offline are expected scenarios

**Implementation**:
```csharp
[ReactiveCommand]
private async Task TestSoundDeviceAsync()
{
    try
    {
        IsSoundDeviceTestRunning = true;
        SoundDeviceTestResult = null;

        await _soundDeviceService.PlayTextV2TestAsync(CancellationToken.None);

        SoundDeviceTestResult = "测试成功";
        _logger.LogInformation("Sound device test succeeded");
    }
    catch (HttpRequestException ex)
    {
        SoundDeviceTestResult = "测试失败: 网络错误，请检查音响设备IP地址";
        _logger.LogError(ex, "Sound device test failed: Network error");
    }
    catch (TaskCanceledException ex)
    {
        SoundDeviceTestResult = "测试失败: 请求超时，请检查音响设备是否在线";
        _logger.LogError(ex, "Sound device test failed: Timeout");
    }
    catch (Exception ex)
    {
        SoundDeviceTestResult = $"测试失败: {ex.Message}";
        _logger.LogError(ex, "Sound device test failed");
    }
    finally
    {
        IsSoundDeviceTestRunning = false;
    }
}
```

---

### Decision 7: UI Button Placement

**Problem**: Where should the test button be placed in the Settings window?

**Options**:

| Option | Pros | Cons |
|--------|------|------|
| A) After volume TextBox, before next section | • Logical grouping<br>• Easy to find | • May clutter section |
| B) In a separate "Test" section | • Very organized | • Overkill for single button |
| C) In a toolbar at bottom of window | • Global test area | • Less context<br>• Harder to find |

**Decision**: **Option A** - After volume TextBox, before next section

**Rationale**:
1. **Logical Grouping**: Test is part of sound device configuration
2. **Discoverability**: Users enabling device will immediately see test option
3. **Consistency**: Similar to how "Test Capture" is placed in camera section
4. **Minimal Disruption**: Doesn't require major UI restructuring

**UI Layout**:
```
┌─────────────────────────────────────────────┐
│ 音响设备设置                                 │
├─────────────────────────────────────────────┤
│ ☑ 启用音响设备                              │
│                                             │
│ 本机IP:     [192.168.1.100        ]         │
│ 音响设备IP: [192.168.1.200        ]         │
│ 音响序列号: [1234567890ABC      ]         │
│ 音量:       [0          ]                   │
│                                             │
│ [ 测试音响 ]                                │
│ 测试成功                                    │
└─────────────────────────────────────────────┘
```

---

## Data Flow

### Normal Test Flow (Success)

```
User clicks "测试音响" button
    │
    ▼
TestSoundDevice command executes
    │
    ▼
IsSoundDeviceTestRunning = true
    │
    ▼
Button becomes disabled (via binding)
    │
    ▼
await _soundDeviceService.PlayTextV2TestAsync()
    │
    ▼
SoundDeviceService.PlayTextV2TestAsync()
    │
    ├─ Log: "Starting sound device test..."
    │
    ├─ Call PlayTextV2Async("音柱测试")
    │   │
    │   ├─ Get settings from ISettingsService
    │   │
    │   ├─ Validate device enabled and config valid
    │   │
    │   ├─ Build TTS URI
    │   │
    │   ├─ Create HttpClient
    │   │
    │   ├─ Post play request (8 retry attempts)
    │   │   │
    │   │   └─ Success response received
    │   │
    │   └─ Return completed task
    │
    ├─ Log: "Sound device test completed successfully"
    │
    └─ Return completed task
    │
    ▼
SoundDeviceTestResult = "测试成功"
    │
    ▼
IsSoundDeviceTestRunning = false
    │
    ▼
Button becomes enabled (via binding)
    │
    ▼
Status text displays "测试成功"
```

### Error Flow

```
User clicks "测试音响" button
    │
    ▼
TestSoundDevice command executes
    │
    ▼
await _soundDeviceService.PlayTextV2TestAsync()
    │
    ▼
SoundDeviceService.PlayTextV2TestAsync()
    │
    ├─ Call PlayTextV2Async("音柱测试")
    │   │
    │   ├─ Get settings
    │   │
    │   ├─ Validate config → VALID
    │   │
    │   ├─ Create HttpClient
    │   │
    │   ├─ Post play request (8 attempts)
    │   │   │
    │   │   ├─ Attempt 1: Exception (network error)
    │   │   ├─ Attempt 2: Exception (timeout)
    │   │   ├─ ...
    │   │   └─ Attempt 8: Exception (no response)
    │   │
    │   └─ Log error and throw exception
    │
    └─ Catch exception, log, rethrow
        │
        ▼
Exception propagates to ViewModel
    │
    ▼
catch (Exception ex) block in TestSoundDeviceAsync
    │
    ├─ Log error details
    │
    ├─ SoundDeviceTestResult = "测试失败: [error message]"
    │
    └─ finally block executes
        │
        ▼
IsSoundDeviceTestRunning = false
    │
    ▼
Status text displays error message
```

## Memory Management

### Rx Subscription Disposal

**Concern**: ReactiveUI command subscriptions can cause memory leaks if not properly disposed.

**Mitigation**:
1. `ReactiveCommand` uses `CreateFromTask` factory which manages subscriptions properly
2. ViewModel follows transient dependency lifecycle (`ITransientDependency`)
3. SettingsWindow is short-lived (opened/closed frequently)
4. No long-lived subscriptions in test command

**Verification Plan**:
- Create memory leak test following `AttendedWeighingServiceMemoryLeakTests` pattern
- Run 1000 iterations of test command
- Verify memory usage remains stable

### HttpClient Disposal

**Concern**: `PlayTextV2Async` creates HttpClient instances directly (not using factory), must be properly disposed.

**Current Implementation Analysis**:
```csharp
// In PlayTextV2Async
var httpClient = new HttpClient { ... };
try
{
    // ... use httpClient
}
finally
{
    httpClient.Dispose(); // ✅ Already properly disposed
}
```

**Status**: ✅ Already handled correctly in existing code.

## Security Considerations

### Input Validation

- Test text is fixed constant ("音柱测试") - no user input
- No SQL injection risk (SQLite with parameterized queries)
- No XSS risk (desktop app, not web)

### Network Security

- Uses existing HTTP client infrastructure
- No new network endpoints exposed
- TLS/SSL follows existing sound device API configuration

### Logging Security

- Test text is logged ("音柱测试") - no sensitive data
- Error messages may contain device IP/SN - already logged in existing code
- No user credentials or secrets involved

## Performance Considerations

### Async Operation

- Test operation is fully async (non-blocking UI)
- 30-second timeout prevents indefinite blocking
- HttpClient timeout configured appropriately

### Memory Footprint

- Minimal additional memory (two string properties, one command)
- No large buffers or collections
- No background timers or scheduled tasks

### CPU Usage

- Test operation is I/O bound (HTTP request)
- Minimal CPU usage during async wait
- No tight loops or computational overhead

## Testing Strategy

### Unit Testing

**What to Test**:
1. `PlayTextV2TestAsync` uses fixed test text
2. Logging is called appropriately
3. Exceptions are caught and rethrown
4. Cancellation token is passed through

**How to Test**:
- Mock `ISettingsService` to return valid configuration
- Use in-memory test doubles for HTTP calls
- Verify method calls and logging

### Integration Testing

**What to Test**:
1. End-to-end flow with real sound device
2. UI button enables/disables correctly
3. Status updates propagate to UI
4. Error handling with device offline

**How to Test**:
- Manual testing with physical hardware
- Automated UI tests (if UI testing framework exists)

### Memory Leak Testing

**What to Test**:
1. No memory leaks after repeated test executions
2. Rx subscriptions are properly disposed
3. HttpClient instances are properly released

**How to Test**:
- Create memory leak test following existing patterns
- Run 1000+ iterations
- Monitor memory usage with dotMemory or Visual Studio Profiler

## Future Enhancements

### Potential Improvements

1. **Advanced Diagnostics**:
   - Network connectivity test (ping device IP)
   - Device status query before playing audio
   - Volume calibration test

2. **Customizable Test Text**:
   - Allow users to configure test phrase
   - Support multiple test phrases (random selection)

3. **Test History**:
   - Log test results with timestamps
   - Show success/failure statistics
   - Export test history for debugging

4. **Batch Testing**:
   - Test all configured sound devices in sequence
   - Support for multi-device setups

5. **Enhanced Cancellation**:
   - Add cancel button for long-running tests
   - Use `CancellationTokenSource` for proper cancellation

### Extensibility Points

The current design supports these enhancements without breaking changes:

- **Test Text**: Can be added as parameter or configuration setting
- **Cancellation**: Can add `CancellationTokenSource` to ViewModel
- **Diagnostics**: Can add additional methods to `ISoundDeviceService`
- **History**: Can add new entity and repository for test results

## Conclusion

This design document outlines a simple, maintainable implementation of sound device testing that:

- Follows existing MVVM and ReactiveUI patterns
- Maintains separation of concerns
- Provides clear user feedback
- Handles errors gracefully
- Avoids memory leaks
- Supports future enhancements

The implementation is straightforward, low-risk, and delivers immediate value to users by enabling quick validation of sound device configuration.
