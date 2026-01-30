# Change: Unify LPR License Plate Recognition Events Using MessageBus

**Change ID**: `unify-lpr-events-with-messagebus`
**Status**: Draft
**Created**: 2026-01-29
**Type**: Refactoring

---

## Why

### Background

MaterialClient integrates with multiple License Plate Recognition (LPR) hardware devices:
- **Hikvision LPR devices** - Using HCNetSDK (implemented in `hikvision-lpr-implementation` change)
- **LprAllInOne devices** - Using HTTP callback endpoints
- **Huaxiazhixin devices** - Using HTTP callback endpoints

Currently, when these devices recognize license plates, the callback handlers in `MinimalWebHostService` directly invoke the `IAttendedWeighingService.OnPlateNumberRecognized()` method. This creates tight coupling between the hardware integration layer and the business logic layer.

### Problems

1. **Tight Coupling**: Hardware callback handlers (`MinimalWebHostService`) directly depend on business service (`IAttendedWeighingService`), violating dependency inversion principle

2. **Limited Extensibility**: Cannot support multiple subscribers for LPR events (e.g., logging, statistics, alerts) without modifying the calling code

3. **Testing Difficulty**: Hard to test hardware callback logic independently from business logic

4. **Inconsistent Architecture**: The rest of the application uses ReactiveUI MessageBus for cross-component communication (ADR-009), but LPR events use direct method calls

5. **Mixed Event Patterns**: `LicensePlateRecognizedEvent` (ABP event) is defined but not used, while `PlateNumberChangedMessage` (MessageBus) is used internally, creating confusion

---

## What Changes

### Overview

Refactor LPR license plate recognition event delivery to use ReactiveUI MessageBus consistently. Hardware callback handlers will publish `LicensePlateRecognizedMessage` to the bus, and `AttendedWeighingService` will subscribe to receive and process these events. This decouples hardware integration from business logic and aligns with ADR-009 architecture.

### Detailed Changes

#### 1. Create Unified MessageBus Message (ADDED)

Create `LicensePlateRecognizedMessage` class to carry complete LPR recognition data:

```csharp
namespace MaterialClient.Common.Events;

public class LicensePlateRecognizedMessage
{
    public string PlateNumber { get; set; }
    public LprAllInOneColorType? ColorType { get; set; }
    public LprDeviceType DeviceType { get; set; }
    public string DeviceName { get; set; }
    public DateTime Timestamp { get; set; }
}
```

This replaces the existing `LicensePlateRecognizedEvent` (ABP event) which is not actively used.

#### 2. Refactor MinimalWebHostService (MODIFIED)

Remove direct dependency on `IAttendedWeighingService`:

**Current Code**:
```csharp
// MaterialClient/Services/MinimalWebHostService.cs:188-200
var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();
weighingService.OnPlateNumberRecognized(license, colorType);
```

**New Code**:
```csharp
// Publish to MessageBus instead
var message = new LicensePlateRecognizedMessage
{
    PlateNumber = license,
    ColorType = colorType,
    DeviceType = LprDeviceType.LprAllInOne,
    DeviceName = deviceName,
    Timestamp = DateTime.Now
};
MessageBus.Current.SendMessage(message);
```

Apply same pattern to Hikvision and Huaxiazhixin callback handlers.

#### 3. Modify AttendedWeighingService (MODIFIED)

Add MessageBus subscription to receive LPR events:

```csharp
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService(/* existing deps */)
    {
        // Subscribe to LPR recognition events
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(msg => OnPlateNumberRecognized(
                msg.PlateNumber,
                msg.ColorType
            ));
    }

    public async ValueTask DisposeAsync()
    {
        _licensePlateSubscription?.Dispose();
        // ... existing disposal logic
    }
}
```

The existing `OnPlateNumberRecognized()` method implementation remains unchanged.

#### 4. Update Service Interface (MODIFIED)

Remove the following method from `IAttendedWeighingService` interface:
```csharp
void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);
```

This method becomes private/internal implementation detail, not part of public interface. It's invoked via MessageBus subscription internally.

#### 5. Documentation Updates (ADDED)

Create or update documentation explaining:
- MessageBus usage guidelines for hardware events
- Difference between MessageBus (real-time UI events) and LocalEventBus (async domain events)
- Memory leak prevention for MessageBus subscriptions

---

## Impact

### Expected Benefits

1. **Loose Coupling**: Hardware layer no longer depends on business service layer, improving testability and modularity

2. **Consistent Architecture**: Aligns with ADR-009 (MessageBus for cross-component communication) and reactive programming patterns documented in `timer-to-rx-pattern.md`

3. **Extensibility**: Easy to add new subscribers (logging, statistics, monitoring) without modifying existing code

4. **Simplified Testing**: Can test hardware callback and business processing independently by sending/receiving MessageBus messages

5. **Real-Time Performance**: MessageBus provides synchronous delivery (<1ms latency) suitable for high-frequency LPR events

6. **Clarity**: Eliminates confusion between `LicensePlateRecognizedEvent` (unused ABP event) and actual message mechanism

### Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Memory leaks from undisposed subscriptions | High | Add `DisposeWith()` pattern, include subscription disposal in `DisposeAsync()`, add memory leak tests |
| Breaking existing integrations | Medium | Keep `OnPlateNumberRecognized()` method signature unchanged, only change how it's invoked |
| Regression in LPR functionality | Medium | Comprehensive integration tests for all device types (Hikvision, LprAllInOne, Huaxiazhixin) |
| Performance overhead from MessageBus | Low | MessageBus is synchronous and lightweight; overhead is negligible |

---

## Success Criteria

- [x] `LicensePlateRecognizedMessage` class created with all required properties
- [ ] `MinimalWebHostService` callback handlers publish `LicensePlateRecognizedMessage` instead of calling `weighingService.OnPlateNumberRecognized()`
- [ ] `AttendedWeighingService` subscribes to `LicensePlateRecognizedMessage` in constructor
- [ ] `OnPlateNumberRecognized()` method removed from `IAttendedWeighingService` interface (becomes private)
- [ ] All LPR device types (Hikvision, LprAllInOne, Huaxiazhixin) work correctly after refactoring
- [ ] MessageBus subscriptions properly disposed in `DisposeAsync()`
- [ ] Unit tests pass for message publishing and subscription
- [ ] Integration tests pass for all LPR device types
- [ ] Memory leak tests show no subscription-related leaks
- [ ] Documentation updated with MessageBus usage guidelines

---

## Next Steps

1. **Review and Approve Proposal**: Review this proposal with the team, confirm architectural approach
2. **Create Design Document**: Create detailed `design.md` explaining the refactoring approach and migration strategy
3. **Create Spec Deltas**: Update `license-plate-recognition` specification with modified requirements
4. **Implement Message Class**: Create `LicensePlateRecognizedMessage` with proper XML documentation
5. **Refactor Callback Handlers**: Modify `MinimalWebHostService` to publish messages instead of direct calls
6. **Add Subscription in AttendedWeighingService**: Implement MessageBus subscription and disposal
7. **Update Service Interface**: Remove `OnPlateNumberRecognized()` from public interface
8. **Write Tests**: Create unit and integration tests for message-based LPR event flow
9. **Run Memory Leak Tests**: Verify no subscription-related memory leaks
10. **Update Documentation**: Document MessageBus usage patterns and guidelines
11. **Archive Old Event**: Remove or deprecate unused `LicensePlateRecognizedEvent` (ABP event)

---

## References

- **ADR-009**: `docs/SDD.md:1654-1693` - MessageBus for cross-component communication
- **Reactive Pattern**: `openspec/docs/timer-to-rx-pattern.md` - Reactive programming patterns in the system
- **Related Changes**:
  - `hikvision-lpr-implementation` - Hikvision LPR service implementation
  - `hikvision-lpr-integration` - Hikvision LPR configuration and UI
- **Existing Events**:
  - `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs` - Unused ABP event (to be deprecated)
  - `MaterialClient.Common/Events/PlateNumberChangedMessage.cs` - Internal UI notification message
- **Specification**: `openspec/specs/license-plate-recognition` - License plate recognition requirements
