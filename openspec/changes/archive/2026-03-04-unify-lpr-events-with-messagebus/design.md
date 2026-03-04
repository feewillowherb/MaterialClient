# Design: Unify LPR Events with MessageBus

**Change ID**: `unify-lpr-events-with-messagebus`
**Author**: Claude (AI Assistant)
**Date**: 2026-01-29
**Status**: Draft

---

## Overview

This document describes the architectural design for refactoring License Plate Recognition (LPR) event delivery from direct method calls to ReactiveUI MessageBus. The refactoring improves decoupling, testability, and consistency with existing architecture patterns.

---

## Current Architecture

### Event Flow (Before)

```
┌─────────────────────────┐
│  Hardware Device        │
│  (Hikvision/LprAllInOne)│
└───────────┬─────────────┘
            │ 1. HTTP Callback / SDK Event
            ▼
┌─────────────────────────────────────────────────────────┐
│  MinimalWebHostService                                  │
│  - HandleHikvisionLprCallback()                        │
│  - HandleLprAllInOneCallback()                         │
└───────────┬─────────────────────────────────────────────┘
            │ 2. Direct Service Call
            │    weighingService.OnPlateNumberRecognized()
            ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - OnPlateNumberRecognized()                           │
│  - License plate caching logic                         │
│  - Publishes PlateNumberChangedMessage via MessageBus  │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus Message
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - Update display       │
└─────────────────────────┘
```

### Problems

1. **Tight Coupling**: `MinimalWebHostService` (hardware layer) directly depends on `IAttendedWeighingService` (business layer)
2. **Hard to Test**: Cannot test hardware callback logic independently from business logic
3. **Limited Extensibility**: Adding new subscribers (logging, monitoring) requires modifying callback handlers
4. **Architectural Inconsistency**: Rest of system uses MessageBus for cross-component communication (ADR-009)

---

## Proposed Architecture

### Event Flow (After)

```
┌─────────────────────────┐
│  Hardware Device        │
│  (Hikvision/LprAllInOne)│
└───────────┬─────────────┘
            │ 1. HTTP Callback / SDK Event
            ▼
┌─────────────────────────────────────────────────────────┐
│  MinimalWebHostService                                  │
│  - HandleHikvisionLprCallback()                        │
│  - HandleLprAllInOneCallback()                         │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ MessageBus.Current.SendMessage(                  │  │
│  │   new LicensePlateRecognizedMessage {           │  │
│  │     PlateNumber, ColorType, DeviceType, ... })   │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────┘
                             │ 2. MessageBus Message
                             │    (Decoupled communication)
                             ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - Subscribes to LicensePlateRecognizedMessage        │
│  - OnPlateNumberRecognized() [private method]         │
│  - License plate caching logic                         │
│  - Publishes PlateNumberChangedMessage via MessageBus  │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus Message
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - Update display       │
└─────────────────────────┘
```

### Benefits

1. **Loose Coupling**: Hardware layer only knows about MessageBus messages, not business services
2. **Easy Testing**: Can test callback handlers by verifying MessageBus messages
3. **Extensible**: Add new subscribers without modifying publishers
4. **Consistent**: Aligns with ADR-009 and reactive programming patterns

---

## Component Design

### 1. LicensePlateRecognizedMessage

**Purpose**: Unified message class for all LPR device types

**Location**: `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

**Design**:
```csharp
namespace MaterialClient.Common.Events;

/// <summary>
///     Message published when a license plate is recognized by any LPR device.
///     Sent via ReactiveUI MessageBus for decoupled event delivery.
/// </summary>
public class LicensePlateRecognizedMessage
{
    /// <summary>
    ///     The recognized license plate number (e.g., "京A12345")
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     Optional plate color type (e.g., 蓝色, 黄色, 绿色)
    /// </summary>
    public LprAllInOneColorType? ColorType { get; set; }

    /// <summary>
    ///     Device type that recognized the plate
    /// </summary>
    public LprDeviceType DeviceType { get; set; }

    /// <summary>
    ///     Human-readable device name (from configuration)
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp when recognition occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

**Rationale**:
- Includes all relevant data from hardware callback
- Device type and name for logging and diagnostics
- Timestamp for accurate event ordering
- Optional color type (not all devices support it)

---

### 2. Callback Handler Refactoring

**Before** (Hikvision example):
```csharp
// MaterialClient/Services/MinimalWebHostService.cs:188-200
private IActionResult HandleHikvisionLprCallback(LprCallbackData callback)
{
    var weighingService = _sharedServiceProvider
        .GetRequiredService<IAttendedWeighingService>();

    var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
    var colorType = callback?.AlarmInfoPlate?.Result?.PlateResult?.ColorType;

    if (!string.IsNullOrWhiteSpace(license))
    {
        // ❌ Direct dependency on business service
        weighingService.OnPlateNumberRecognized(license, colorType);
        return Results.Ok(new { result = 1 });
    }
}
```

**After**:
```csharp
private IActionResult HandleHikvisionLprCallback(LprCallbackData callback)
{
    // ❌ Remove: var weighingService = ...

    var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
    var colorType = callback?.AlarmInfoPlate?.Result?.PlateResult?.ColorType;

    if (!string.IsNullOrWhiteSpace(license))
    {
        // ✅ Publish decoupled message
        var message = new LicensePlateRecognizedMessage
        {
            PlateNumber = license,
            ColorType = colorType.HasValue
                ? (LprAllInOneColorType?)colorType.Value
                : null,
            DeviceType = LprDeviceType.Hikvision,
            DeviceName = callback?.AlarmInfoPlate?.DeviceName ?? "Unknown",
            Timestamp = DateTime.Now
        };

        MessageBus.Current.SendMessage(message);
        _logger.LogInformation("Hikvision LPR: {Plate}", license);

        return Results.Ok(new { result = 1 });
    }
}
```

**Changes**:
- Remove `IAttendedWeighingService` dependency
- Create and populate `LicensePlateRecognizedMessage`
- Publish via `MessageBus.Current.SendMessage()`
- Keep logging for diagnostics

---

### 3. Service Subscription Pattern

**Design**:
```csharp
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService(/* existing dependencies */)
    {
        // Subscribe to LPR recognition events from MessageBus
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(msg =>
            {
                _logger?.LogInformation(
                    "Received LPR event: {Plate} from {Device}",
                    msg.PlateNumber, msg.DeviceName);

                // Invoke existing processing logic
                OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
            });
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose subscription to prevent memory leaks
        _licensePlateSubscription?.Dispose();

        // ... existing disposal logic
    }

    // Changed from public to private (no longer part of interface)
    private void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)
    {
        // Existing implementation unchanged
        // License plate caching, recommendation logic, etc.
    }
}
```

**Key Points**:
- Subscription created in constructor (singleton service)
- Existing `OnPlateNumberRecognized()` logic reused
- Method visibility changed from `public` to `private`
- Subscription disposed in `DisposeAsync()` to prevent memory leaks
- Logging added for diagnostics

---

## Memory Management

### Subscription Disposal

**Critical**: All MessageBus subscriptions must be disposed to prevent memory leaks.

**Pattern**:
```csharp
public class AttendedWeighingService : IAsyncDisposable
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService()
    {
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(/* handler */);
    }

    public async ValueTask DisposeAsync()
    {
        // ✅ Always dispose subscriptions
        _licensePlateSubscription?.Dispose();
    }
}
```

**Memory Leak Prevention**:
1. Store subscription reference in field
2. Dispose in `DisposeAsync()` or `Dispose()`
3. Use `DisposeWith()` pattern for ViewModel subscriptions
4. Test with long-running scenarios (1000+ cycles)

---

## Error Handling

### Callback Handler Errors

**Strategy**: Log and continue, don't let one bad message break the system

```csharp
try
{
    var message = new LicensePlateRecognizedMessage { /* ... */ };
    MessageBus.Current.SendMessage(message);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process LPR callback from {Device}", deviceName);
    // Return success to hardware device (don't retry)
    return Results.Ok(new { result = 0, error = "Processing failed" });
}
```

### Subscription Errors

**Strategy**: Log but don't throw in subscription handler

```csharp
MessageBus.Current
    .Listen<LicensePlateRecognizedMessage>()
    .Subscribe(msg =>
    {
        try
        {
            OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process LPR message: {Plate}",
                msg.PlateNumber);
        }
    });
```

---

## Testing Strategy

### Unit Tests

**1. Message Publishing**:
```csharp
[Fact]
public void HandleHikvisionLprCallback_ShouldPublishCorrectMessage()
{
    // Arrange
    var messages = new List<LicensePlateRecognizedMessage>();
    using var subscription = MessageBus.Current
        .Listen<LicensePlateRecognizedMessage>()
        .Subscribe(messages.Add);

    var callback = new LprCallbackData
    {
        AlarmInfoPlate = new AlarmInfoPlate
        {
            Result = new PlateResult { License = "京A12345" },
            DeviceName = "Hikvision-LPR-1"
        }
    };

    // Act
    var result = _service.HandleHikvisionLprCallback(callback);

    // Assert
    Assert.Single(messages);
    Assert.Equal("京A12345", messages[0].PlateNumber);
    Assert.Equal(LprDeviceType.Hikvision, messages[0].DeviceType);
}
```

**2. Message Subscription**:
```csharp
[Fact]
public void AttendedWeighingService_ShouldSubscribeToLprMessages()
{
    // Arrange
    var service = new AttendedWeighingService(/* mocks */);
    var plateNumbers = new List<string?>();

    // Act
    MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
    {
        PlateNumber = "京A12345"
    });

    var mostFrequent = service.GetMostFrequentPlateNumber();

    // Assert
    Assert.Equal("京A12345", mostFrequent);
}
```

### Integration Tests

**End-to-End Flow**:
```csharp
[Fact]
public async Task LprEventFlow_ShouldUpdateUiCorrectly()
{
    // Arrange: Setup complete system with mocked hardware
    var (service, viewModel) = CreateSystem();

    // Act: Simulate hardware callback
    SimulateHikvisionCallback("京A12345");

    // Assert: Verify service state
    Assert.Equal("京A12345", service.GetMostFrequentPlateNumber());

    // Assert: Verify UI updated
    Assert.Equal("京A12345", viewModel.MostFrequentPlateNumber);
}
```

### Memory Leak Tests

**Long-Running Scenario**:
```csharp
[Fact]
public void RepeatedLprMessages_ShouldNotLeakMemory()
{
    // Arrange
    var service = new AttendedWeighingService(/* mocks */);
    var initialMemory = GC.GetTotalMemory(true);

    // Act: Send 1000 messages
    for (int i = 0; i < 1000; i++)
    {
        MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
        {
            PlateNumber = $"京A{i:00000}"
        });
    }

    // Cleanup
    service.Dispose();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // Assert: Memory growth should be minimal
    var finalMemory = GC.GetTotalMemory(true);
    var growth = finalMemory - initialMemory;
    Assert.True(growth < 1024 * 1024, $"Memory grew {growth} bytes");
}
```

---

## Migration Strategy

### Phase 1: Prepare (No Breaking Changes)
1. Create `LicensePlateRecognizedMessage` class
2. Add MessageBus subscription in `AttendedWeighingService` alongside existing method
3. Keep `OnPlateNumberRecognized()` in interface (dual mode)

### Phase 2: Migrate Publishers
4. Refactor `MinimalWebHostService` callback handlers to use MessageBus
5. Add feature flag to switch between direct calls and MessageBus (optional)
6. Test both modes in parallel

### Phase 3: Remove Old Path
7. Remove direct service calls from callback handlers
8. Remove `OnPlateNumberRecognized()` from public interface
9. Make method private in implementation
10. Update tests and documentation

### Rollback Plan

If issues arise:
1. Revert callback handlers to direct calls
2. Keep MessageBus subscription as additional listener (no harm)
3. Investigate and fix issues
4. Retry migration

---

## Trade-offs and Alternatives

### Alternative 1: Use ABP LocalEventBus

**Rejected Because**:
- Adds 5-10ms latency vs <1ms for MessageBus
- Overkill for simple UI notifications
- Requires additional `IEventHandler` classes
- Less suitable for high-frequency events (10-20 LPR/minute)

**Use Case**: Better for async domain events like `TryMatchEvent` (database operations)

### Alternative 2: Keep Direct Calls

**Rejected Because**:
- Tight coupling between layers
- Hard to test in isolation
- Inconsistent with rest of architecture
- Cannot support multiple subscribers

### Alternative 3: Use Rx Observable in Services

**Rejected Because**:
- `IHikvisionLprService` already has `IObservable<LicensePlateRecognizedEvent>`
- But `MinimalWebHostService` sits between SDK and service
- Would require `MinimalWebHostService` to implement observable pattern
- MessageBus is simpler for this use case

---

## Impact Analysis

### Breaking Changes

**Public Interface**:
- `IAttendedWeighingService.OnPlateNumberRecognized()` removed
- Impact: Low (likely no external callers)

**Internal Implementation**:
- Callback handler signatures unchanged
- `OnPlateNumberRecognized()` logic unchanged
- Impact: None

### Performance

- **Before**: Direct method call (~0.1ms)
- **After**: MessageBus publish + subscribe (~0.5ms)
- **Impact**: Negligible for LPR frequency (10-20 events/minute)

### Compatibility

- Backward compatible if `OnPlateNumberRecognized()` kept temporarily
- Forward compatible with new subscriber patterns
- No database or API changes required

---

## Open Questions

1. **Should we deprecate `LicensePlateRecognizedEvent` (ABP event)?**
   - It's currently unused
   - Recommendation: Mark `[Obsolete]` and remove in future cleanup

2. **Should we support both MessageBus and direct calls during transition?**
   - Depends on deployment risk tolerance
   - Recommendation: Do full switch with thorough testing (simpler)

3. **Should we add device-specific message classes?**
   - Current design: Single `LicensePlateRecognizedMessage` with `DeviceType` property
   - Alternative: `HikvisionLprMessage`, `LprAllInOneMessage`, etc.
   - Recommendation: Single message is sufficient, reduces duplication

---

## References

- **ADR-009**: MessageBus for cross-component communication
- **Reactive Pattern**: `openspec/docs/timer-to-rx-pattern.md`
- **Related Changes**: `hikvision-lpr-implementation`, `hikvision-lpr-integration`
- **Specification**: `openspec/specs/license-plate-recognition`
