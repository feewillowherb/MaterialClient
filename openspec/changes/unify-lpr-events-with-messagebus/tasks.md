# Tasks: Unify LPR License Plate Recognition Events Using MessageBus

**Change ID**: `unify-lpr-events-with-messagebus`
**Total Tasks**: 15
**Estimated Duration**: 3-4 days

---

## Task Overview

Refactor LPR event delivery from direct method calls to ReactiveUI MessageBus. This involves creating a unified message class, modifying callback handlers to publish messages, adding subscriptions in business services, and ensuring proper resource disposal. The work is organized into phases: message creation, callback refactoring, service subscription, testing, and documentation.

---

## Phase 1: Foundation - Message and Interface

### Task 1.1: Create LicensePlateRecognizedMessage Class

**Status**: Pending
**Priority**: High
**Estimated**: 1 hour

**Description**:
Create a new MessageBus message class to carry LPR recognition data from hardware callbacks to business services.

**Steps**:
1. Create file `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`
2. Define properties: `PlateNumber`, `ColorType`, `DeviceType`, `DeviceName`, `Timestamp`
3. Add XML documentation comments for all members
4. Mark class as `public` with parameterless constructor or primary constructor

**Validation**:
- [ ] File created at correct path
- [ ] All properties defined with correct types
- [ ] XML documentation complete
- [ ] Code compiles without errors

**Output**: `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

---

### Task 1.2: Remove OnPlateNumberRecognized from Service Interface

**Status**: Pending
**Priority**: High
**Estimated**: 0.5 hours

**Description**:
Remove `OnPlateNumberRecognized` method from `IAttendedWeighingService` interface as it becomes an internal implementation detail.

**Steps**:
1. Open `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. Remove `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);` from interface definition
3. Keep method implementation in concrete class (will be invoked via MessageBus subscription)
4. Update interface XML documentation if it references this method

**Validation**:
- [ ] Method removed from interface only
- [ ] Method implementation still exists in concrete class
- [ ] Code compiles without errors
- [ ] No build warnings about unused methods

**Output**: Modified `IAttendedWeighingService` interface

---

## Phase 2: Hardware Callback Refactoring

### Task 2.1: Refactor Hikvision LPR Callback Handler

**Status**: Pending
**Priority**: High
**Estimated**: 1 hour

**Description**:
Modify Hikvision LPR callback in `MinimalWebHostService` to publish MessageBus message instead of directly calling service method.

**Steps**:
1. Open `MaterialClient/Services/MinimalWebHostService.cs`
2. Locate Hikvision callback handler (around line 188-200)
3. Remove `IAttendedWeighingService` dependency retrieval
4. Replace direct call with `MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage { ... })`
5. Set `DeviceType = LprDeviceType.Hikvision`
6. Extract device name from callback data

**Validation**:
- [ ] Direct service call removed
- [ ] MessageBus message published with correct properties
- [ ] DeviceType set to Hikvision
- [ ] Code compiles without errors

**Output**: Modified Hikvision callback handler

---

### Task 2.2: Refactor LprAllInOne HTTP Callback Handler

**Status**: Pending
**Priority**: High
**Estimated**: 1 hour

**Description**:
Modify LprAllInOne HTTP callback handler to publish MessageBus message.

**Steps**:
1. Locate LprAllInOne callback handler in `MinimalWebHostService` (around line 347-361)
2. Remove `IAttendedWeighingService` dependency retrieval
3. Replace direct call with `MessageBus.Current.SendMessage()`
4. Set `DeviceType = LprDeviceType.LprAllInOne`
5. Extract device name from request data or configuration

**Validation**:
- [ ] Direct service call removed
- [ ] MessageBus message published
- [ ] DeviceType set correctly
- [ ] Existing logging preserved

**Output**: Modified LprAllInOne callback handler

---

### Task 2.3: Refactor Huaxiazhixin HTTP Callback Handler

**Status**: Pending
**Priority**: Medium
**Estimated**: 1 hour

**Description**:
Modify Huaxiazhixin HTTP callback handler to publish MessageBus message.

**Steps**:
1. Locate Huaxiazhixin callback handler in `MinimalWebHostService`
2. Remove `IAttendedWeighingService` dependency retrieval
3. Replace direct call with `MessageBus.Current.SendMessage()`
4. Set `DeviceType = LprDeviceType.Huaxiazhixin`
5. Extract device name from request data or configuration

**Validation**:
- [ ] Direct service call removed
- [ ] MessageBus message published
- [ ] DeviceType set correctly
- [ ] Existing logging preserved

**Output**: Modified Huaxiazhixin callback handler

---

## Phase 3: Business Service Integration

### Task 3.1: Add MessageBus Subscription to AttendedWeighingService

**Status**: Pending
**Priority**: High
**Estimated**: 2 hours

**Description**:
Add MessageBus subscription in `AttendedWeighingService` constructor to receive LPR recognition messages.

**Steps**:
1. Open `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. Add private field `IDisposable _licensePlateSubscription`
3. In constructor, create subscription:
   ```csharp
   _licensePlateSubscription = MessageBus.Current
       .Listen<LicensePlateRecognizedMessage>()
       .Subscribe(msg => OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType));
   ```
4. Ensure subscription is disposed in `DisposeAsync()` method

**Validation**:
- [ ] Subscription field added
- [ ] Subscription created in constructor
- [ ] Existing `OnPlateNumberRecognized()` method reused
- [ ] Subscription disposed in `DisposeAsync()`
- [ ] Code compiles without errors

**Output**: Modified `AttendedWeighingService` with MessageBus subscription

---

### Task 3.2: Make OnPlateNumberRecognized Private/Internal

**Status**: Pending
**Priority**: Low
**Estimated**: 0.5 hours

**Description**:
Change `OnPlateNumberRecognized` visibility from public to private or internal since it's no longer part of public interface.

**Steps**:
1. Locate `OnPlateNumberRecognized` method in `AttendedWeighingService`
2. Change visibility from `public` to `private`
3. Update XML documentation to indicate it's invoked via MessageBus

**Validation**:
- [ ] Visibility changed to private
- [ ] Code compiles without errors
- [ ] No external code references this method directly

**Output**: `OnPlateNumberRecognized` method with private visibility

---

## Phase 4: Testing

### Task 4.1: Create Unit Tests for Message Publishing

**Status**: Pending
**Priority**: High
**Estimated**: 2 hours

**Description**:
Create unit tests to verify callback handlers publish correct messages.

**Steps**:
1. Create test file `MaterialClient.Common.Tests/Tests/LicensePlateRecognizedMessageTests.cs`
2. Test Hikvision callback publishes message with correct properties
3. Test LprAllInOne callback publishes message with correct properties
4. Test Huaxiazhixin callback publishes message with correct properties
5. Mock `MessageBus.Current` using test isolation techniques
6. Verify device-specific properties (DeviceType, DeviceName)

**Validation**:
- [ ] Test file created
- [ ] All three device types tested
- [ ] Message properties verified
- [ ] Tests pass consistently

**Output**: Unit test suite for message publishing

---

### Task 4.2: Create Unit Tests for Message Subscription

**Status**: Pending
**Priority**: High
**Estimated**: 2 hours

**Description**:
Create unit tests to verify `AttendedWeighingService` correctly subscribes and processes LPR messages.

**Steps**:
1. Create or extend test file for `AttendedWeighingService`
2. Test that subscription is created in constructor
3. Test that receiving message invokes `OnPlateNumberRecognized`
4. Test subscription disposal in `DisposeAsync()`
5. Test multiple message handling (license plate caching logic)
6. Use mock services to isolate subscription behavior

**Validation**:
- [ ] Subscription creation verified
- [ ] Message processing verified
- [ ] Disposal logic tested
- [ ] No memory leaks in tests
- [ ] Tests pass consistently

**Output**: Unit test suite for message subscription

---

### Task 4.3: Create Integration Tests for End-to-End Flow

**Status**: Pending
**Priority**: High
**Estimated**: 3 hours

**Description**:
Create integration tests simulating real hardware callbacks through MessageBus to business logic.

**Steps**:
1. Create integration test file `MaterialClient.Common.Tests/Integration/LprEventFlowTests.cs`
2. Test complete flow: Hardware callback → MessageBus → Service processing → UI notification
3. Test with mock hardware simulators for each device type
4. Verify license plate caching logic works correctly
5. Verify `PlateNumberChangedMessage` still sent to UI
6. Test error handling (invalid plate numbers, null values)

**Validation**:
- [ ] Integration test file created
- [ ] All device types tested end-to-end
- [ ] License plate caching verified
- [ ] UI notifications verified
- [ ] Error cases handled
- [ ] Tests pass consistently

**Output**: Integration test suite for LPR event flow

---

### Task 4.4: Run Memory Leak Tests

**Status**: Pending
**Priority**: High
**Estimated**: 2 hours

**Description**:
Run memory leak tests to ensure MessageBus subscriptions don't cause memory leaks.

**Steps**:
1. Extend existing `AttendedWeighingServiceMemoryLeakTests`
2. Add test for repeated message subscription and disposal
3. Monitor memory growth over 1000+ message cycles
4. Verify subscriptions are properly disposed
5. Check for lingering references to message handlers
6. Use dotMemory or similar profiler if available

**Validation**:
- [ ] Memory leak test created
- [ ] No memory growth after 1000+ cycles
- [ ] Subscriptions properly disposed
- [ ] Test passes consistently

**Output**: Memory leak test results

---

## Phase 5: Documentation and Cleanup

### Task 5.1: Update Software Design Document

**Status**: Pending
**Priority**: Medium
**Estimated**: 1 hour

**Description**:
Update `docs/SDD.md` to document MessageBus usage for LPR events and clarify event system architecture.

**Steps**:
1. Update ADR-009 section to include LPR event examples
2. Add guidance on when to use MessageBus vs LocalEventBus
3. Document `LicensePlateRecognizedMessage` usage
4. Add memory leak prevention guidelines for subscriptions
5. Cross-reference related ADRs and patterns

**Validation**:
- [ ] ADR-009 updated with LPR examples
- [ ] MessageBus vs LocalEventBus guidance added
- [ ] Memory leak guidelines documented
- [ ] Document builds without errors

**Output**: Updated `docs/SDD.md`

---

### Task 5.2: Create LPR Integration Documentation

**Status**: Pending
**Priority**: Low
**Estimated**: 1.5 hours

**Description**:
Create or update documentation explaining how LPR devices integrate with the system via MessageBus.

**Steps**:
1. Create file `openspec/docs/lpr-event-architecture.md`
2. Document the event flow: Hardware → Callback → MessageBus → Service
3. Include code examples for adding new LPR device types
4. Explain subscription patterns and disposal requirements
5. Add troubleshooting guide for common issues

**Validation**:
- [ ] Documentation file created
- [ ] Event flow clearly explained
- [ ] Code examples provided
- [ ] Troubleshooting guide included
- [ ] Document reviewed for clarity

**Output**: `openspec/docs/lpr-event-architecture.md`

---

### Task 5.3: Deprecate Unused LicensePlateRecognizedEvent

**Status**: Pending
**Priority**: Low
**Estimated**: 0.5 hours

**Description**:
Mark the unused ABP event `LicensePlateRecognizedEvent` as obsolete or remove it entirely.

**Steps**:
1. Open `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs`
2. Add `[Obsolete]` attribute with deprecation message
3. Search codebase for any usages of this event
4. If unused, consider deleting the file entirely
5. Update any related documentation

**Validation**:
- [ ] Event marked as obsolete or removed
- [ ] No active usages in codebase
- [ ] Documentation updated
- [ ] Code compiles without errors

**Output**: Deprecated or removed `LicensePlateRecognizedEvent`

---

### Task 5.4: Update OpenSpec Specification

**Status**: Pending
**Priority**: Medium
**Estimated**: 1 hour

**Description**:
Update `license-plate-recognition` specification to reflect MessageBus-based architecture.

**Steps**:
1. Open `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md`
2. Add MODIFIED requirements for LPR event handling
3. Update scenarios to reference MessageBus instead of direct calls
4. Add scenarios for MessageBus subscription and disposal
5. Run `openspec validate unify-lpr-events-with-messagebus --strict`

**Validation**:
- [ ] Spec delta created with MODIFIED requirements
- [ ] Scenarios updated for MessageBus pattern
- [ ] Validation passes without errors
- [ ] Specification is clear and complete

**Output**: Updated specification in `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md`

---

## Progress Tracking

**Phase 1 Progress**: 0/2 tasks completed (0%)
**Phase 2 Progress**: 0/3 tasks completed (0%)
**Phase 3 Progress**: 0/2 tasks completed (0%)
**Phase 4 Progress**: 0/4 tasks completed (0%)
**Phase 5 Progress**: 0/4 tasks completed (0%)
**Overall Progress**: 0/15 tasks (0%)

---

## Dependencies and Parallelization

**Can be done in parallel**:
- Phase 1 tasks (independent)
- Phase 2 tasks (each device handler is independent)
- Phase 4.1 and 4.2 (unit tests can be written in parallel)
- Phase 5.1, 5.2, 5.3 (documentation tasks are independent)

**Must be sequential**:
- Phase 1 must complete before Phase 3
- Phase 2 and Phase 3 should complete before Phase 4 (integration tests need both publisher and subscriber)
- Phase 4 must complete before finalizing documentation in Phase 5
