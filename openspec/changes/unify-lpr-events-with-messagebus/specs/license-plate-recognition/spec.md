# license-plate-recognition Specification Delta

**Capability**: `license-plate-recognition`
**Change ID**: `unify-lpr-events-with-messagebus`
**Type**: MODIFIED
**Date**: 2026-01-29

This document specifies the requirements modified by the `unify-lpr-events-with-messagebus` change. Only the requirements that have changed are included here. Refer to the base specification for the complete set of requirements.

---

## MODIFIED Requirements

### Requirement: LPR Service Integration with MessageBus

The system SHALL use ReactiveUI MessageBus to deliver license plate recognition events from hardware callback handlers to business services, decoupling the hardware integration layer from the business logic layer.

#### Scenario: Hikvision device recognizes license plate and publishes MessageBus message
- **GIVEN** the system is configured with a Hikvision LPR device
- **AND** the Hikvision SDK callback `MSGCallBack` receives a `COMM_UPLOAD_PLATE_RESULT` (0x2800) message
- **AND** the callback handler in `MinimalWebHostService` is invoked
- **WHEN** the callback parses the license plate result
- **THEN** the system SHALL:
  - Create a `LicensePlateRecognizedMessage` with:
    - `PlateNumber` set to the recognized plate text (using GBK encoding)
    - `ColorType` set to the parsed color type if available
    - `DeviceType` set to `LprDeviceType.Hikvision`
    - `DeviceName` set to the device name from callback configuration
    - `Timestamp` set to the current UTC time
  - Publish the message via `MessageBus.Current.SendMessage(message)`
  - Log the recognition event with device name and plate number
  - NOT directly call `IAttendedWeighingService.OnPlateNumberRecognized()`

#### Scenario: LprAllInOne device recognizes license plate and publishes MessageBus message
- **GIVEN** the system is configured with an LprAllInOne device
- **AND** the HTTP callback endpoint receives a POST request with `type=online`
- **AND** the form data contains `plate_num` parameter
- **WHEN** the callback handler in `MinimalWebHostService` processes the request
- **THEN** the system SHALL:
  - Create a `LicensePlateRecognizedMessage` with:
    - `PlateNumber` set to the `plate_num` value
    - `ColorType` set to null or parsed from `plate_color` if available
    - `DeviceType` set to `LprDeviceType.LprAllInOne`
    - `DeviceName` set to the configured device name
    - `Timestamp` set to the current UTC time
  - Publish the message via `MessageBus.Current.SendMessage(message)`
  - Log the recognition event
  - Return HTTP 200 response to the hardware device

#### Scenario: Huaxiazhixin device recognizes license plate and publishes MessageBus message
- **GIVEN** the system is configured with a Huaxiazhixin device
- **AND** the HTTP callback endpoint receives a POST request with recognized plate data
- **WHEN** the callback handler in `MinimalWebHostService` processes the request
- **THEN** the system SHALL:
  - Create a `LicensePlateRecognizedMessage` with:
    - `PlateNumber` set to the recognized plate number
    - `ColorType` set to null or parsed if available
    - `DeviceType` set to `LprDeviceType.Huaxiazhixin`
    - `DeviceName` set to the configured device name
    - `Timestamp` set to the current UTC time
  - Publish the message via `MessageBus.Current.SendMessage(message)`
  - Log the recognition event
  - Return HTTP 200 response to the hardware device

---

### Requirement: AttendedWeighingService Subscribes to LPR Messages

The system SHALL configure `AttendedWeighingService` to subscribe to `LicensePlateRecognizedMessage` via ReactiveUI MessageBus, processing recognition events through the existing license plate caching and recommendation logic.

#### Scenario: AttendedWeighingService subscribes to LPR messages on initialization
- **GIVEN** the `AttendedWeighingService` is being instantiated as a singleton dependency
- **WHEN** the service constructor is executed
- **THEN** the system SHALL:
  - Create a MessageBus subscription using `MessageBus.Current.Listen<LicensePlateRecognizedMessage>()`
  - Store the subscription `IDisposable` in a private field (`_licensePlateSubscription`)
  - Configure the subscription handler to:
    - Log the received message (plate number, device name, timestamp)
    - Invoke the private `OnPlateNumberRecognized()` method with message data
  - Ensure the subscription persists for the service lifetime

#### Scenario: AttendedWeighingService processes LPR message from MessageBus
- **GIVEN** the `AttendedWeighingService` has an active MessageBus subscription
- **AND** a `LicensePlateRecognizedMessage` is published to the bus
- **WHEN** the subscription handler receives the message
- **THEN** the system SHALL:
  - Log the recognition event with device information
  - Invoke `OnPlateNumberRecognized(message.PlateNumber, message.ColorType)`
  - Execute existing license plate caching logic (frequency counting, color filtering)
  - Publish `PlateNumberChangedMessage` via MessageBus for UI updates
  - Process low-priority plate colors according to existing rules

#### Scenario: AttendedWeighingService disposes LPR message subscription on cleanup
- **GIVEN** the `AttendedWeighingService` is being disposed
- **AND** the MessageBus subscription is active
- **WHEN** the `DisposeAsync()` method is called
- **THEN** the system SHALL:
  - Call `Dispose()` on the `_licensePlateSubscription` field
  - Set the field to null to release reference
  - Continue with existing disposal logic
  - Prevent memory leaks from undelivered messages

---

### Requirement: LicensePlateRecognizedMessage Definition

The system SHALL provide a unified message class for transmitting license plate recognition data via ReactiveUI MessageBus, supporting all LPR device types with consistent structure.

#### Scenario: LicensePlateRecognizedMessage carries complete recognition data
- **GIVEN** a license plate recognition event occurs from any LPR device type
- **WHEN** a `LicensePlateRecognizedMessage` is created
- **THEN** the message SHALL contain:
  - `PlateNumber` (string): The recognized license plate text (e.g., "京A12345")
  - `ColorType` (LprAllInOneColorType?): Optional plate color (蓝色, 黄色, 绿色, etc.)
  - `DeviceType` (LprDeviceType): Enum value indicating device type (Hikvision, LprAllInOne, Huaxiazhixin)
  - `DeviceName` (string): Human-readable device name from configuration
  - `Timestamp` (DateTime): UTC timestamp when recognition occurred

#### Scenario: LicensePlateRecognizedMessage is published via MessageBus
- **GIVEN** a `LicensePlateRecognizedMessage` is created with complete data
- **WHEN** the message is published using `MessageBus.Current.SendMessage(message)`
- **THEN** the system SHALL:
  - Deliver the message to all active subscribers of `LicensePlateRecognizedMessage`
  - Deliver synchronously without queuing delays (<1ms latency)
  - Preserve all message properties without data loss
  - Allow multiple subscribers to receive the same message instance

---

### Requirement: Service Interface Simplification

The system SHALL remove the `OnPlateNumberRecognized` method from the `IAttendedWeighingService` public interface, making it a private implementation detail invoked via MessageBus subscription.

#### Scenario: IAttendedWeighingService interface excludes OnPlateNumberRecognized method
- **GIVEN** the `IAttendedWeighingService` interface is defined
- **WHEN** the refactoring is complete
- **THEN** the interface SHALL NOT contain:
  - `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)`
- **AND** the interface SHALL maintain all other existing members

#### Scenario: AttendedWeighingService implements OnPlateNumberRecognized as private method
- **GIVEN** the `AttendedWeighingService` class implements `IAttendedWeighingService`
- **AND** the MessageBus subscription is active
- **WHEN** the subscription handler receives a `LicensePlateRecognizedMessage`
- **THEN** the class SHALL invoke a private `OnPlateNumberRecognized()` method
- **AND** the method SHALL NOT be accessible from external code
- **AND** the method SHALL retain its existing implementation logic (caching, filtering, etc.)

---

### Requirement: Memory Leak Prevention for MessageBus Subscriptions

The system SHALL ensure all MessageBus subscriptions are properly disposed to prevent memory leaks in long-running scenarios.

#### Scenario: Subscription is stored and disposed correctly
- **GIVEN** a service creates a MessageBus subscription
- **WHEN** the subscription is created
- **THEN** the system SHALL:
  - Store the `IDisposable` returned by `Subscribe()` in a private field
  - Add XML documentation comment indicating disposal responsibility
  - Include the field in the disposal sequence

#### Scenario: Subscription is disposed when service is cleaned up
- **GIVEN** a service with an active MessageBus subscription is being disposed
- **WHEN** the `DisposeAsync()` or `Dispose()` method is called
- **THEN** the system SHALL:
  - Call `Dispose()` on all stored subscription `IDisposable` fields
  - Handle null subscriptions gracefully
  - Log disposal completion if logging is enabled
  - Release all references to the subscription

#### Scenario: Long-running system does not leak memory from repeated LPR events
- **GIVEN** the system is running for extended period (24+ hours)
- **AND** LPR devices recognize 1000+ license plates
- **WHEN** memory usage is monitored
- **THEN** the system SHALL:
  - Show no continuous memory growth from MessageBus subscriptions
  - Properly dispose subscriptions when services are restarted
  - Pass memory leak tests with <1MB growth after 1000 events
  - Not accumulate undelivered messages in MessageBus queue

---

## Cross-References

**Modified Requirements**:
- LPR Service Integration with MessageBus (MODIFIED from direct service calls)
- AttendedWeighingService Subscribes to LPR Messages (NEW subscription pattern)
- LicensePlateRecognizedMessage Definition (NEW message class)
- Service Interface Simplification (MODIFIED interface)
- Memory Leak Prevention for MessageBus Subscriptions (NEW requirement)

**Related Capabilities**:
- `attended-weighing` - Depends on LPR events for automated plate capture

**Base Specification**:
- `openspec/specs/license-plate-recognition/spec.md` - Full specification including unmodified requirements

**Related Changes**:
- `hikvision-lpr-implementation` - Implemented Hikvision LPR service with event streams
- `hikvision-lpr-integration` - Added Hikvision configuration and UI
