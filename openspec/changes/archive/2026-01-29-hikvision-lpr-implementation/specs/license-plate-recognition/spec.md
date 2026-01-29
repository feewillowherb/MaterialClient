# Spec Delta: license-plate-recognition

**Change ID**: `hikvision-lpr-implementation`
**Affected Spec**: `license-plate-recognition`
**Type**: Implementation

---

## ADDED Requirements

### Requirement: Hikvision LPR Service Implementation

The system SHALL provide a complete implementation of the `IHikvisionLprService` interface, enabling communication with Hikvision LPR devices for both passive (device-pushed) and manual (app-triggered) license plate capture.

#### Scenario: Service initializes HCNetSDK and starts listening

- **GIVEN** the system has Hikvision LPR devices configured
- **AND** the application starts
- **WHEN** the `HikvisionLprService.StartAsync(string listenLocalIp, int listenLocalPort)` is called
- **THEN** the system SHALL:
  - Call `NET_DVR_Init()` to initialize HCNetSDK (only once globally)
  - Create a `MSGCallBack` delegate instance for the callback function
  - **CRITICAL**: Pin the callback delegate using `GCHandle.Alloc()` to prevent garbage collection
  - Call `NET_DVR_StartListen_V30(listenLocalIp, listenLocalPort, callback, IntPtr.Zero)`
  - Store the returned listen handle for later cleanup
  - Return `true` if listen started successfully, `false` otherwise
  - Log the initialization and listen start operations

#### Scenario: Service stops listening and cleans up resources

- **GIVEN** the `HikvisionLprService` is currently listening for LPR events
- **WHEN** the `HikvisionLprService.StopAsync()` is called
- **THEN** the system SHALL:
  - Check if a listen handle exists (≥ 0)
  - Call `NET_DVR_StopListen_V30(listenHandle)` with the stored handle
  - **CRITICAL**: Release the pinned callback delegate by calling `GCHandle.Free()`
  - Reset the listen handle to -1 and callback handle to null
  - Optionally call `NET_DVR_Cleanup()` if this is the final cleanup
  - Log the stop and cleanup operations

#### Scenario: Service adds or updates device configuration

- **GIVEN** the operator wants to add or update a Hikvision LPR device
- **WHEN** `HikvisionLprService.AddOrUpdateDevice(LicensePlateRecognitionConfig config)` is called
- **AND** the config is valid (not null and `IsValid()` returns true)
- **THEN** the system SHALL:
  - Use the device IP address as the key
  - Add the config to the internal device dictionary if the key doesn't exist
  - Update the config if the key already exists
  - Store the config for later device identification in callbacks
  - Log the add or update operation

#### Scenario: Service checks device online status

- **GIVEN** a Hikvision LPR device configuration
- **WHEN** `HikvisionLprService.IsOnline(LicensePlateRecognitionConfig config)` is called
- **AND** the config is valid
- **THEN** the system SHALL:
  - Call `NET_DVR_Init()` if not already initialized
  - Create a `NET_DVR_USER_LOGIN_INFO` structure with the device credentials
  - Call `NET_DVR_Login_V40()` to attempt login
  - Return `true` if login succeeds (userId ≥ 0)
  - Return `false` if login fails (userId < 0)
  - On failure, call `NET_DVR_GetLastError()` and log the error code and description
  - Logout after the check (optional, for session management)

#### Scenario: Service receives plate recognition result via callback

- **GIVEN** the `HikvisionLprService` is listening
- **AND** a Hikvision device recognizes a license plate
- **WHEN** the SDK invokes the `MSGCallBack` with `lCommand = 0x2800` (COMM_UPLOAD_PLATE_RESULT)
- **THEN** the system SHALL:
  - **CRITICAL**: Wrap the entire callback in a try-catch to prevent process crashes
  - Parse the `NET_DVR_ALARMER` structure from `pAlarmer` to identify the device
  - Extract the device IP from `NET_DVR_ALARMER`
  - Look up the device configuration by IP address
  - Parse the `NET_DVR_PLATE_RESULT` structure from `pAlarmInfo`
  - Extract the license plate number using GBK encoding (for Chinese characters)
  - Create a `LicensePlateRecognizedEvent` with:
    - `PlateNumber`: The extracted license plate text
    - `DeviceName`: From the device config, or "Unknown" if not found
    - `Direction`: From the device config, or default to `In`
    - `Timestamp`: Current date and time
  - Publish the event to the `PlateRecognized` observable stream
  - Log the recognition event
  - **NOT**: Perform any blocking I/O or long-running operations in the callback

#### Scenario: Multiple devices send recognition results simultaneously

- **GIVEN** multiple Hikvision LPR devices are configured
- **AND** all devices are configured to push results to the same client listen port
- **WHEN** two or more devices recognize plates simultaneously
- **THEN** the system SHALL:
  - Receive all callbacks through the same `MSGCallBack` function
  - Correctly identify each device from `NET_DVR_ALARMER` structure
  - Look up the correct device configuration for each callback
  - Create events with the correct `DeviceName` and `Direction` for each device
  - Publish all events to the `PlateRecognized` observable stream
  - Handle concurrent callbacks safely (thread-safe dictionary operations)

---

### Requirement: HCNetSDK P/Invoke Integration

The system SHALL provide P/Invoke declarations for HCNetSDK functions and structures, enabling managed code to call the native Hikvision SDK.

#### Scenario: HikvisionSdk module defines all required P/Invoke declarations

- **GIVEN** the system needs to call HCNetSDK functions
- **WHEN** the development team implements the service
- **THEN** the system SHALL:
  - Create a `HikvisionSdk.cs` module in `MaterialClient.Common/Services/Hikvision/`
  - Define the `MSGCallBack` delegate type with signature:
    - `void MSGCallBack(int lCommand, IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser)`
  - Declare core SDK functions using `DllImport`
  - Define structures with `StructLayout(LayoutKind.Sequential)`
  - Define constants for message types
  - Ensure structure layout matches HCNetSDK.h header file

---

### Requirement: Encoding Support for Chinese License Plates

The system SHALL support GBK encoding for processing Chinese characters in license plate numbers.

#### Scenario: License plate text contains Chinese characters

- **GIVEN** a Hikvision device recognizes a Chinese license plate (e.g., "京A12345")
- **WHEN** the service processes the `NET_DVR_PLATE_RESULT` structure
- **THEN** the system SHALL:
  - Use `HikvisionEncodingHelper.GetStringFromPtr()` to extract the license plate text
  - Use GBK encoding (`Encoding.GetEncoding("GBK")`) to decode the byte array
  - Correctly decode Chinese characters (e.g., "京" instead of garbled text)
  - Store the decoded text in `LicensePlateRecognizedEvent.PlateNumber`
  - Log the recognized plate number with correct characters

#### Scenario: GBK encoding is not available on the system

- **GIVEN** the system runs on an environment that doesn't support GBK encoding
- **WHEN** `Encoding.GetEncoding("GBK")` throws `NotSupportedException`
- **THEN** the system SHALL:
  - Catch the `NotSupportedException` in the encoding helper
  - Fall back to UTF-8 encoding
  - Log a warning indicating GBK is unavailable and UTF-8 is used
  - Continue processing without crashing
  - **ACKNOWLEDGE**: Chinese characters may not display correctly with UTF-8 fallback

---

### Requirement: Error Handling and Logging

The system SHALL provide comprehensive error handling and logging for all HCNetSDK operations.

#### Scenario: SDK function call fails

- **GIVEN** a HCNetSDK function is called (e.g., `NET_DVR_Login_V40`)
- **WHEN** the function returns a failure indicator (e.g., userId < 0)
- **THEN** the system SHALL:
  - Immediately call `NET_DVR_GetLastError()` to retrieve the error code
  - Map the error code to a human-readable description
  - Log the error with details: function name, error code, error description, device info
  - Propagate the error to the caller (return `false` or throw exception as appropriate)
  - **NOT**: Ignore errors or have empty catch blocks

---

### Requirement: Memory Management and Resource Cleanup

The system SHALL properly manage all resources to prevent memory leaks and ensure stable long-term operation.

#### Scenario: Service starts and stops listening repeatedly

- **GIVEN** the service is started and stopped multiple times (e.g., 1000 iterations)
- **WHEN** a memory leak test is run
- **THEN** the system SHALL:
  - Allocate a new `GCHandle` on each start
  - Release the `GCHandle` on each stop
  - Call `NET_DVR_StopListen_V30()` with the correct handle on each stop
  - **NOT** leak GCHandle instances (verified via memory profiling)
  - **NOT** leak unmanaged memory (verified via memory profiling)
  - Show stable memory usage over time (no continuous growth)

---

### Requirement: Thread Safety in Callback Processing

The system SHALL ensure thread-safe processing of SDK callbacks, which may be invoked on unmanaged threads concurrently.

#### Scenario: Multiple callbacks are invoked concurrently

- **GIVEN** multiple Hikvision devices send recognition results simultaneously
- **WHEN** the SDK invokes `MSGCallBack` on multiple threads concurrently
- **THEN** the system SHALL:
  - Use `ConcurrentDictionary` for device configuration storage
  - Ensure thread-safe access to shared state
  - Use atomic operations for flags and counters
  - **NOT**: Use locks that could cause deadlocks
  - **NOT**: Allow race conditions that could corrupt data

---

## MODIFIED Requirements

### Requirement: Hikvision LPR Service Interface Definition (Modified)

The `IHikvisionLprService` interface SHALL be modified to support multi-device management and global listen service mode, replacing the previous single-device design.

#### Scenario: Interface reflects multi-device architecture

- **GIVEN** the system needs to support multiple Hikvision LPR devices
- **WHEN** the `IHikvisionLprService` interface is reviewed
- **THEN** the system SHALL:
  - **REMOVED**: `Task<bool> ConnectAsync(LicensePlateRecognitionConfig config)` - Single-device connection method
  - **REMOVED**: `Task DisconnectAsync()` - Single-device disconnection method
  - **REMOVED**: `Task StartListeningAsync()` - Single-device listen start (no parameters)
  - **REMOVED**: `Task StopListeningAsync()` - Single-device listen stop (no parameters)
  - **REMOVED**: `bool IsConnected { get; }` - Single-device connection state
  - **ADDED**: `void AddOrUpdateDevice(LicensePlateRecognitionConfig config)` - Multi-device configuration management
  - **ADDED**: `bool IsOnline(LicensePlateRecognitionConfig config)` - Device online check
  - **ADDED**: `Task<bool> StartAsync(string listenLocalIp, int listenLocalPort)` - Global listen service start
  - **ADDED**: `Task StopAsync()` - Global listen service stop
  - **UNCHANGED**: `IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }` - Event stream
  - Provide XML documentation comments for all members explaining the multi-device architecture

---

## Implementation Notes

### Critical Implementation Points

1. **GCHandle Management**:
   - Always pin the callback delegate with `GCHandle.Alloc()` before calling `NET_DVR_StartListen_V30()`
   - Always release the GCHandle with `GCHandle.Free()` in `StopAsync()`
   - Store the GCHandle instance as a field for later cleanup

2. **Callback Exception Handling**:
   - Wrap the entire callback body in try-catch
   - Log all exceptions but never let them propagate to unmanaged code
   - Unhandled exceptions in callbacks can crash the application process

3. **Thread Safety**:
   - Callbacks run on unmanaged threads - only perform quick operations
   - Use `ConcurrentDictionary` for device configurations
   - Use thread-safe Rx subjects (`Subject<T>` is thread-safe for OnNext)

4. **Encoding**:
   - Always use GBK encoding for Chinese license plates
   - Provide fallback to UTF-8 if GBK is unavailable
   - Log warnings when fallback encoding is used

5. **Error Handling**:
   - Check return values of all SDK function calls
   - Call `NET_DVR_GetLastError()` immediately after a failure
   - Map error codes to readable descriptions

6. **Memory Management**:
   - Release all unmanaged resources (handles, GCHandles, subscriptions)
   - Implement `IDisposable` if needed for cleanup
   - Write memory leak tests for long-running scenarios

### Dependencies

- **HCNetSDK DLLs**: Must be deployed with the application
- **.NET 10.0**: For P/Invoke and modern C# features
- **System.Reactive**: For Rx observable streams
- **Serilog**: For structured logging

### Platform Constraints

- **Windows Only**: HCNetSDK is a Windows-only native library
- **x64 Architecture**: SDK is built for x64, application must target x64
- **Admin Rights**: May require administrator privileges for certain SDK operations

---

## References

- **Original Proposal**: `HikLpr_OpenSpec_Proposal.md`
- **Design Document**: `openspec/changes/hikvision-lpr-implementation/design.md`
- **Tasks**: `openspec/changes/hikvision-lpr-implementation/tasks.md`
- **Affected Spec**: `license-plate-recognition`
