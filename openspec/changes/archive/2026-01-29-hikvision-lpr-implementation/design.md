# Design: Hikvision LPR Service Implementation

**Change ID**: `hikvision-lpr-implementation`
**Status**: Draft
**Created**: 2025-01-29

---

## Architecture Overview

This document explains the architectural design, key decisions, and technical implementation details for the Hikvision LPR service (`HikvisionLprService`).

### System Context

The service integrates with Hikvision LPR devices through HCNetSDK, receiving license plate recognition results via a single listen port that can handle multiple devices. Recognition events are published through Rx observable streams for integration with weighing services.

---

## Key Design Decisions

### 1. Single Listen Port for Multiple Devices

**Decision**: Use `NET_DVR_StartListen_V30` to start listening on a single port that receives recognition results from multiple Hikvision devices.

**Rationale**:
- `NET_DVR_StartListen_V30` is designed to receive data from multiple devices on a single port
- Simplifies configuration management (only one listen address to configure)
- Reduces resource usage (single listen handle and callback)

**Impact**:
- Interface design adjusted to global listen service mode (`StartAsync`/`StopAsync`)
- Device identification required through `NET_DVR_ALARMER` structure in callback

### 2. GCHandle to Pin Callback Delegate

**Decision**: Use `GCHandle.Alloc()` to pin the `MSGCallBack` delegate, preventing garbage collection during listening.

**Rationale**:
- HCNetSDK is unmanaged code, storing only function pointers
- GC cannot detect that unmanaged code is still using the delegate
- If delegate is collected, SDK crashes when calling the function pointer

**Risks**:
- Memory leak if `GCHandle` is not released
- Must ensure `GCHandle.Free()` is called in `StopAsync`

### 3. Rx Subject for Event Stream

**Decision**: Use `Subject<LicensePlateRecognizedEvent>` as the core of the event stream, exposed through `IObservable<T>`.

**Rationale**:
- Aligns with project's ReactiveUI pattern
- Supports multiple subscribers
- Provides rich operators (filter, buffer, throttle, etc.)
- Easy to test

**Type Choice**:
- Using `Subject<T>` (does not replay historical events)
- License plate events are one-time, no need for replay

### 4. Separate Listen and Device Configuration

**Decision**: Clearly distinguish between listen configuration (local IP/port) and device configuration (device IP/port/credentials).

**Rationale**:
- Listen configuration is the client's listen endpoint, controlled by the application
- Device configuration is Hikvision device connection information, used for active capture and device identification
- Clear configuration separation aids understanding and usage

### 5. GBK Encoding for Chinese License Plates

**Decision**: Unify use of GBK encoding for processing Chinese characters in license plate text.

**Rationale**:
- Hikvision SDK returns Chinese text using GBK encoding
- License plates may contain Chinese characters (e.g., "京A12345")

**Risks**:
- Some systems may not support GBK encoding
- Fallback to UTF-8 provided with warning logging

### 6. Thread Safety in Callback

**Decision**: SDK callbacks execute in unmanaged threads; perform only data parsing and event publishing, no blocking operations.

**Rationale**:
- Callbacks run in unmanaged thread pool, should not block
- Blocking operations would block SDK's internal processing
- Use Rx scheduler to marshal events to main thread if needed

### 7. Error Handling Strategy

**Decision**: Check errors after every SDK call, call `NET_DVR_GetLastError()` to get error code, and map to readable description.

**Rationale**:
- HCNetSDK error information is critical for debugging
- Unified error handling aids troubleshooting

---

## Data Flow

### Passive Capture Flow (Device Push)

1. Application starts → `StartAsync("192.168.1.10", 7200)` → `NET_DVR_StartListen_V30` → Listen handle stored
2. Hikvision device recognizes plate → Pushes data to `192.168.1.10:7200`
3. SDK invokes callback → `MessageCallback(0x2800, pAlarmer, pAlarmInfo, dwBufLen, pUser)`
4. Parse `NET_DVR_ALARMER` → Extract device IP
5. Lookup device config by IP → Get device name and direction
6. Parse `NET_DVR_PLATE_RESULT` → Extract plate number (GBK encoding)
7. Create `LicensePlateRecognizedEvent` → Publish to Rx stream
8. Subscribers receive event → Update weighing records

### Active Capture Flow (App Triggered)

1. User triggers capture → `TriggerManualCaptureAsync(config)`
2. `NET_DVR_Login_V40` (login device) → `NET_DVR_ContinuousShoot` (trigger capture)
3. Result received via listen callback (same as passive capture flow)
4. Optional: `NET_DVR_Logout` (logout device)

---

## Error Handling

### Error Scenarios and Handling

| Error Scenario | Detection Method | Handling Strategy | Log Level |
|----------------|------------------|-------------------|-----------|
| SDK initialization failed | `NET_DVR_Init()` returns `false` | Throw `InvalidOperationException` | Critical |
| Listen port occupied | `NET_DVR_StartListen_V30()` returns `< 0` | Log error, return `false`, prompt user to check port | Error |
| Device offline | `NET_DVR_Login_V40()` returns `< 0` | `IsOnline()` returns `false`, log error code | Warning |
| Callback exception | `try-catch` wraps callback | Log exception, don't affect other callbacks | Error |
| Memory leak | Memory leak tests | Fix resource leaks | Critical |
| Encoding unavailable | `NotSupportedException` | Fallback to UTF-8, log warning | Warning |

---

## Testing Strategy

### Unit Tests

- **Framework**: xUnit
- **Mock**: Wrap HCNetSDK calls as `IHikvisionSdk` interface, use Moq
- **Test Cases**: Add/Update device, IsOnline, StartAsync, StopAsync, callback handling

### Memory Leak Tests

- **Method**: Repeatedly start/stop listening (1000 times), long-running (1 hour), many events (10,000)
- **Verification**: Use `GC.GetTotalMemory()` or dotMemory
- **Resources to Check**: GCHandle, listen handle, Rx subscriptions

### Integration Tests

- **Environment**: Real Hikvision devices or simulated environment
- **Scenarios**: Connect device, receive recognition results, multiple devices
- **CI/CD**: Mark as manual tests (requires hardware)

---

## Performance Considerations

### Performance Goals

- **Callback Processing Time**: < 1ms (don't block SDK)
- **Event Publishing Latency**: < 10ms (from receive to publish)
- **Memory Usage**: Stable, no continuous growth
- **Multi-Device Support**: At least 10 devices pushing simultaneously

### Optimization Strategies

1. **Callback Optimization**: Only parse data, no blocking I/O
2. **Event Stream Optimization**: Use `RefCount()` for subscription management
3. **Memory Optimization**: Release unmanaged memory promptly, avoid large allocations in callback

---

## Security Considerations

### Security Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Plaintext device credentials | Use encrypted storage (Windows Credential Manager) or config file encryption |
| Unauthorized listen port access | Use firewall, listen only on localhost (127.0.0.1) |
| Callback injection attacks | Validate all input data, no dynamic code execution in callback |
| DLL hijacking | Use strong name signing, place DLLs in application directory |

---

## Deployment Considerations

### Dependencies

- **HCNetSDK DLLs**: HCNetSDK.dll, HCNetSDKCom.dll, etc.
- **Location**: Application directory (same as exe)
- **Version**: CH-HCNetSDKV6.1.9.48 or compatible

### Configuration Requirements

- **Listen Port**: Must be dedicated, not conflict with other services, suggested range 7200-7299
- **Device Configuration**: Each device needs IP, port, username, password
- **Device-Side Configuration**: Devices must be configured to push results to client listen address

### Troubleshooting

1. **Listen startup fails**: Check port occupation, firewall settings, error code
2. **Device offline**: Use `IsOnline()` to check, verify network, check credentials
3. **Callback not triggered**: Check device push configuration, network connectivity, use Wireshark

---

## Future Enhancements

1. **Active Capture Functionality**: Implement `TriggerManualCaptureAsync()`
2. **Image Storage**: Automatically save license plate images, upload to cloud (OSS)
3. **Advanced Event Filtering**: Filter by device, direction, time, blacklist/whitelist
4. **Performance Monitoring**: Monitor callback processing time, recognition success rate
5. **Configuration Hot Update**: Add/remove devices at runtime without restart

---

## References

- **SDK Documentation**: HCNetSDK.h (CH-HCNetSDKV6.1.9.48)
- **Proposal**: `HikLpr_OpenSpec_Proposal.md`
- **Interface**: `IHikvisionLprService`
- **Reference Implementation**: `HikvisionService` (security camera service)
- **Project Conventions**: `openspec/project.md`
