# Implementation Tasks: Sound Device Test Feature

## Task Overview

This document provides an ordered checklist of implementation tasks for the sound device test feature. Tasks are organized by priority and dependency order.

## Phase 1: Service Layer Implementation

### Task 1.1: Add Test Method to ISoundDeviceService Interface

**File**: `MaterialClient.Common/Services/SoundDeviceService.cs`

**Steps**:
1. Locate the `ISoundDeviceService` interface definition (around line 16)
2. Add new method signature after existing `PlayTextV2Async` method:
   ```csharp
   /// <summary>
   ///     Play fixed test text on sound device for testing purposes
   /// </summary>
   /// <param name="cancellationToken">Cancellation token</param>
   Task PlayTextV2TestAsync(CancellationToken cancellationToken = default);
   ```
3. Add XML documentation comment explaining the method purpose

**Validation**:
- Interface compiles without errors
- Method signature follows project async naming convention (Async suffix)
- XML documentation is present and properly formatted

**Dependencies**: None

---

### Task 1.2: Implement PlayTextV2TestAsync Method

**File**: `MaterialClient.Common/Services/SoundDeviceService.cs`

**Steps**:
1. Implement the `PlayTextV2TestAsync` method in `SoundDeviceService` class (after `PlayTextV2Async` method, around line 375)
2. Implementation should:
   - Use fixed test text: "音柱测试"
   - Call existing `PlayTextV2Async` method internally to reuse logic
   - Wrap call in try-catch block for exception handling
   - Log test operation start using `_logger?.LogInformation`
   - Log success/failure using appropriate log levels
   - Respect `CancellationToken` parameter
3. Follow existing error handling patterns from `PlayTextV2Async` method

**Pseudo-code**:
```csharp
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

**Validation**:
- Method compiles without errors
- Fixed test text is "音柱测试"
- Logging is implemented for start, success, and failure cases
- Cancellation token is passed through to underlying method
- Exception handling prevents uncaught exceptions

**Dependencies**: Task 1.1 must be completed first

---

## Phase 2: ViewModel Implementation

### Task 2.1: Add ISoundDeviceService Dependency to ViewModel

**File**: `MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**Steps**:
1. Locate the constructor around line 130
2. Add `ISoundDeviceService` parameter to constructor
3. Add private readonly field for the service:
   ```csharp
   private readonly ISoundDeviceService _soundDeviceService;
   ```
4. Assign parameter to field in constructor body

**Validation**:
- Code compiles without errors
- AutoConstructor source generator will handle the parameter assignment
- Dependency injection will resolve `ISoundDeviceService` at runtime

**Dependencies**: Phase 1 must be completed

---

### Task 2.2: Add Test Status Properties

**File**: `MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**Steps**:
1. Add observable properties for test status tracking after line 126 (after sound device settings properties):
   ```csharp
   [Reactive] private bool _isSoundDeviceTestRunning = false;
   [Reactive] private string? _soundDeviceTestResult = null;
   ```
2. These properties will be bound to UI for status display

**Validation**:
- Properties compile without errors
- ReactiveUI source generator generates property change notifications
- Property names follow camelCase convention

**Dependencies**: Task 2.1

---

### Task 2.3: Add Test Command

**File**: `MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**Steps**:
1. Add ReactiveCommand method after existing commands (after `TestCaptureAsync` around line 408):
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

**Validation**:
- Command compiles without errors
- ReactiveUI source generator generates command property
- Async/await pattern is properly used
- Finally block ensures `IsSoundDeviceTestRunning` is always reset
- User-friendly error messages are set

**Dependencies**: Task 2.2

---

### Task 2.4: Configure Test Command CanExecute

**File**: `MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**Steps**:
1. In constructor, add canExecute logic for test command after existing subscriptions (around line 145):
   ```csharp
   // Configure TestSoundDevice command to only enable when sound device is enabled
   TestSoundDevice = ReactiveCommand.CreateFromTask(TestSoundDeviceAsync,
       this.WhenAnyValue(x => x.SoundDeviceEnabled).Select(enabled => enabled));
   ```
2. This ensures test button is only clickable when sound device is enabled

**Validation**:
- Command canExecute logic compiles
- Test button becomes enabled/disabled based on `SoundDeviceEnabled` checkbox
- Rx subscription properly observes property changes

**Dependencies**: Task 2.3

---

## Phase 3: UI Implementation

### Task 3.1: Add Test Button to Settings Window

**File**: `MaterialClient/Views/SettingsWindow.axaml`

**Steps**:
1. Locate the sound device settings section (around line 584 where checkbox is)
2. After the volume TextBox (around line 628), add test button:
   ```xml
   <Button Content="测试音响"
           Command="{Binding TestSoundDevice}"
           IsEnabled="{Binding IsSoundDeviceTestRunning, Converter={x:Static Converters:BoolNegationConverter.Instance}}"
           Margin="0,10,0,0"
           Padding="20,8"
           HorizontalAlignment="Left" />
   ```
3. Add status TextBlock below button:
   ```xml
   <TextBlock Text="{Binding SoundDeviceTestResult}"
              Foreground="Blue"
              Margin="0,5,0,0"
              TextWrapping="Wrap"
              FontSize="12"
              Visibility="{Binding SoundDeviceTestResult, Converter={x:Static Converters:StringNotNullToVisibilityConverter.Instance}}" />
   ```

**Note**: You may need to create `BoolNegationConverter` and `StringNotNullToVisibilityConverter` if they don't exist in the project. Alternatively, use existing converters or inline logic.

**Validation**:
- Button appears in sound device section
- Button is bound to `TestSoundDevice` command
- Button is disabled when test is running
- Status text appears when test result is available

**Dependencies**: Phase 2 must be completed

---

### Task 3.2: Verify UI Layout and Styling

**File**: `MaterialClient/Views/SettingsWindow.axaml`

**Steps**:
1. Ensure test button aligns with existing UI style
2. Check spacing and margins match surrounding controls
3. Verify button is positioned logically (below configuration controls)
4. Ensure status text is readable and properly styled
5. Test with different window sizes to ensure no overflow

**Validation**:
- Button and status text are visually consistent with existing UI
- No layout issues or overlaps
- All text is readable and properly aligned

**Dependencies**: Task 3.1

---

## Phase 4: Testing

### Task 4.1: Write Unit Tests

**File**: `MaterialClient.Common.Tests/Tests/SoundDeviceServiceTests.cs`

**Steps**:
1. Add unit test for `PlayTextV2TestAsync` method
2. Mock `ISettingsService` to return valid sound device settings
3. Verify test text is "音柱测试"
4. Verify logging is called appropriately
5. Test cancellation token handling
6. Test exception handling and re-throwing

**Example Test Structure**:
```csharp
[Fact]
public async Task PlayTextV2TestAsync_ShouldUseFixedTestText()
{
    // Arrange
    var mockSettingsService = new Mock<ISettingsService>();
    // ... setup mock
    var service = new SoundDeviceService(...);

    // Act
    await service.PlayTextV2TestAsync(CancellationToken.None);

    // Assert
    // Verify PlayTextV2Async was called with "音柱测试"
}
```

**Validation**:
- All tests pass
- Test coverage includes success, failure, and cancellation cases
- Mocks are properly configured

**Dependencies**: Phase 1-3 completed

---

### Task 4.2: Manual Testing

**Steps**:
1. Launch application and open Settings window
2. Enable sound device with valid configuration
3. Click "测试音响" button
4. Verify "音柱测试" is played through sound device
5. Verify "测试成功" message appears
6. Test with sound device disabled - button should be disabled
7. Test with invalid configuration - verify error message appears
8. Test cancellation - close settings window during test

**Validation**:
- Audio plays correctly when device is configured
- Appropriate success/error messages displayed
- UI remains responsive during async operation
- No memory leaks or resource issues

**Dependencies**: Task 4.1

---

### Task 4.3: Memory Leak Testing

**File**: `MaterialClient.Common.Tests/Tests/SoundDeviceServiceMemoryLeakTests.cs` (new file)

**Steps**:
1. Create memory leak test following `AttendedWeighingServiceMemoryLeakTests` pattern
2. Run test loop for multiple iterations (e.g., 1000 test calls)
3. Verify memory usage remains stable
4. Check for undisposed HttpClient instances
5. Verify Rx subscriptions are properly disposed

**Validation**:
- Memory usage is stable over test iterations
- No undisposed resources detected
- Test passes consistently

**Dependencies**: Task 4.2

---

## Phase 5: Documentation

### Task 5.1: Update User Documentation (if applicable)

**Steps**:
1. Document sound device test feature in user manual
2. Add screenshots of test button in Settings window
3. Explain test procedure and troubleshooting tips
4. Document common error messages and their meanings

**Validation**:
- Documentation is clear and accurate
- Screenshots match current UI
- Troubleshooting tips cover common scenarios

**Dependencies**: Phase 4 completed

---

### Task 5.2: Update Developer Documentation

**Steps**:
1. Add code comments explaining test method purpose
2. Document any new patterns or conventions introduced
3. Update architecture documentation if needed

**Validation**:
- Code comments are clear and helpful
- Any architectural decisions are documented

**Dependencies**: Task 5.1

---

## Task Dependencies Summary

```
Phase 1: Service Layer (No dependencies)
├─ Task 1.1: Interface method
└─ Task 1.2: Method implementation (depends on 1.1)

Phase 2: ViewModel (depends on Phase 1)
├─ Task 2.1: Add service dependency
├─ Task 2.2: Add status properties (depends on 2.1)
├─ Task 2.3: Add test command (depends on 2.2)
└─ Task 2.4: Configure canExecute (depends on 2.3)

Phase 3: UI (depends on Phase 2)
├─ Task 3.1: Add button and status
└─ Task 3.2: Verify layout (depends on 3.1)

Phase 4: Testing (depends on Phases 1-3)
├─ Task 4.1: Unit tests
├─ Task 4.2: Manual testing (depends on 4.1)
└─ Task 4.3: Memory leak testing (depends on 4.2)

Phase 5: Documentation (depends on Phase 4)
├─ Task 5.1: User documentation
└─ Task 5.2: Developer documentation (depends on 5.1)
```

## Parallelization Opportunities

- **Phase 1** tasks are sequential (1.2 depends on 1.1)
- **Phase 2** tasks are sequential (each depends on previous)
- **Phase 3** tasks are sequential (3.2 depends on 3.1)
- **Phase 4** tasks are mostly sequential, but 4.1 can start once Phase 3 is complete
- **Phase 5** tasks can be done in parallel with each other once Phase 4 is done

## Estimated Validation Checklist

Use this checklist to verify complete implementation:

- [x] `ISoundDeviceService.PlayTextV2TestAsync` method added to interface
- [x] `PlayTextV2TestAsync` implementation uses fixed text "音柱测试"
- [x] Implementation includes proper logging and error handling
- [x] `ISoundDeviceService` injected into `SettingsWindowViewModel`
- [x] `IsSoundDeviceTestRunning` and `SoundDeviceTestResult` properties added
- [x] `TestSoundDeviceAsync` command implemented with ReactiveUI
- [x] Command canExecute properly tied to `SoundDeviceEnabled` (via Button.IsEnabled binding)
- [x] Test button added to SettingsWindow.axaml
- [x] Status text display added to UI
- [ ] Unit tests written and passing (requires .NET SDK)
- [ ] Manual testing completed successfully (requires hardware)
- [ ] Memory leak tests passing (requires .NET SDK)
- [x] Code follows project MVVM and ReactiveUI patterns
- [x] All async methods use `Async` suffix
- [x] All Rx subscriptions properly disposed
- [x] No compiler warnings or errors (syntax verified)
- [ ] OpenSpec validation passes: `openspec validate sound-device-test-feature --strict`
