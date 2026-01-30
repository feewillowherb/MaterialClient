# Unify LPR Events with MessageBus - Proposal Summary

## Quick Overview

**Change ID**: `unify-lpr-events-with-messagebus`
**Status**: Draft (Pending Approval)
**Type**: Refactoring
**Created**: 2026-01-29

## Problem Statement

Currently, LPR (License Plate Recognition) hardware callback handlers in `MinimalWebHostService` directly call `IAttendedWeighingService.OnPlateNumberRecognized()`. This creates tight coupling between the hardware layer and business logic layer, making the system harder to test and extend.

**Current Flow**:
```
Hardware → MinimalWebHostService → weighingService.OnPlateNumberRecognized() → Processing
```

## Proposed Solution

Refactor to use ReactiveUI MessageBus for decoupled event delivery:

**New Flow**:
```
Hardware → MinimalWebHostService → MessageBus → AttendedWeighingService → Processing
```

## Key Changes

1. **Create Unified Message**: `LicensePlateRecognizedMessage` class with plate number, color, device type, device name, and timestamp

2. **Refactor Callback Handlers**: `MinimalWebHostService` publishes MessageBus messages instead of calling service directly

3. **Add Service Subscription**: `AttendedWeighingService` subscribes to `LicensePlateRecognizedMessage` in constructor

4. **Simplify Interface**: Remove `OnPlateNumberRecognized()` from `IAttendedWeighingService` (becomes private)

5. **Ensure Cleanup**: Properly dispose subscriptions to prevent memory leaks

## Benefits

- ✅ **Loose Coupling**: Hardware layer no longer depends on business service
- ✅ **Testability**: Can test callbacks and business logic independently
- ✅ **Extensibility**: Easy to add new subscribers (logging, monitoring, alerts)
- ✅ **Consistency**: Aligns with ADR-009 (MessageBus for cross-component communication)
- ✅ **Performance**: MessageBus adds <1ms latency (negligible for 10-20 LPR events/minute)

## Files Created

- `openspec/changes/unify-lpr-events-with-messagebus/proposal.md` - Why, what, impact
- `openspec/changes/unify-lpr-events-with-messagebus/tasks.md` - 15 implementation tasks
- `openspec/changes/unify-lpr-events-with-messagebus/design.md` - Technical architecture and design decisions
- `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md` - Modified requirements
- `openspec/changes/unify-lpr-events-with-messagebus/README.md` - This summary

## Validation

```bash
openspec validate unify-lpr-events-with-messagebus --strict
```

Result: ✅ **Valid**

## Next Steps

1. **Review Proposal**: Read `proposal.md` and `design.md` for full details
2. **Approve or Request Changes**: Provide feedback on the approach
3. **Implement**: Follow tasks in `tasks.md` sequentially
4. **Test**: Run unit, integration, and memory leak tests
5. **Deploy**: Monitor system after changes

## Estimated Effort

- **Total Tasks**: 15
- **Estimated Duration**: 3-4 days
- **Risk Level**: Medium (requires careful testing of all LPR device types)

## References

- ADR-009: MessageBus for cross-component communication
- `openspec/docs/timer-to-rx-pattern.md` - Reactive programming patterns
- Related: `hikvision-lpr-implementation`, `hikvision-lpr-integration`
