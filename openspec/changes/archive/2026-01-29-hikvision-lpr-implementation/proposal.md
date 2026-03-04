# 变更：海康威视 LPR 服务实现

**变更 ID**：`hikvision-lpr-implementation`
**状态**：执行完成
**创建日期**：2025-01-29
**完成日期**：2025-01-29
**类型**：功能

---

## 背景与动机

### 背景

MaterialClient 是用于工业场景物料称重管理的 Windows 桌面应用。当前系统已集成车牌识别功能。海康威视 LPR 服务接口 `IHikvisionLprService` 已定义但尚未实现。该接口用于支持系统使用海康威视设备进行车牌识别，包括主动抓拍与被动抓拍两种模式。

参考文档 `HikLpr_OpenSpec_Proposal.md` 提供了详细的技术迁移与实现指引，涵盖 HCNetSDK 集成、生命周期管理、回调处理等关键实现细节。

### 问题

The following issues need to be resolved:

1. **Interface Not Implemented**: The `IHikvisionLprService` interface is defined but lacks concrete implementation code. The system cannot use Hikvision LPR functionality.

2. **Non-standard Method Naming**: The method naming in the interface does not follow project conventions and needs to be corrected to comply with coding standards.

3. **Missing HCNetSDK Integration**: There are no P/Invoke declarations and Hikvision SDK type definitions, making it impossible to call native SDK functionality.

4. **Missing Lifecycle Management**: Proper SDK initialization, listen start/stop, callback delegate pinning (GCHandle), and other critical logic are not implemented.

5. **Unclear Configuration**: The distinction between listen address (local IP/port) and device address (device IP/port) configuration is unclear.

---

## 变更内容

### 概览

Implement the `IHikvisionLprService` interface to provide complete Hikvision LPR service functionality. The service will communicate with Hikvision devices through HCNetSDK, supporting both passive capture (device pushes license plate recognition results) and active capture (application triggers photo capture).

### 详细变更

#### 1. Interface Optimization (MODIFIED)

Based on the actual working mechanism of `NET_DVR_StartListen_V30` (single listen port receives data from multiple devices), optimize the `IHikvisionLprService` interface design:

- Remove original single-device connection methods (`ConnectAsync`/`DisconnectAsync`)
- Change to global listen service mode (`StartAsync`/`StopAsync`)
- Add multi-device management methods (`AddOrUpdateDevice`, `IsOnline`)
- Keep event stream interface (`PlateRecognized`)

#### 2. HCNetSDK P/Invoke Integration (ADDED)

Create a `HikvisionSdk.cs` module to centrally manage Hikvision SDK P/Invoke declarations:

- Core APIs: `NET_DVR_Init`, `NET_DVR_Cleanup`, `NET_DVR_Login_V40`, `NET_DVR_Logout`, `NET_DVR_StartListen_V30`, `NET_DVR_StopListen_V30`, `NET_DVR_ContinuousShoot`, `NET_DVR_GetLastError`
- Callback delegate: `MSGCallBack`
- Structures: `NET_DVR_USER_LOGIN_INFO`, `NET_DVR_DEVICEINFO_V40`, `NET_DVR_SNAPCFG`, `NET_DVR_ALARMER`, `NET_DVR_PLATE_RESULT`, `NET_ITS_PLATE_RESULT`, `NET_DVR_JPEGPARA`
- Ensure structure layout matches HCNetSDK.h header file

#### 3. HikvisionLprService Implementation (ADDED)

Implement the `IHikvisionLprService` interface, providing the following functionality:

**SDK Lifecycle Management**:
- Initialize HCNetSDK (singleton pattern, initialize only once globally)
- Proper resource cleanup (`NET_DVR_Cleanup`)

**Listen Service Management**:
- Start listen service (`StartAsync`): Call `NET_DVR_StartListen_V30`, pin callback delegate (GCHandle), store listen handle
- Stop listen service (`StopAsync`): Call `NET_DVR_StopListen_V30`, release callback delegate (GCHandle.Free)
- Dedicated listen port, must not conflict with other application ports

**Device Management**:
- Add or update device configuration (`AddOrUpdateDevice`): Use `ConcurrentDictionary` to store device configurations (IP → Config)
- Check device online status (`IsOnline`): Attempt to login to device to verify connection

**Event Stream Handling**:
- Implement `PlateRecognized` property: Use `Subject<LicensePlateRecognizedEvent>` or `BehaviorSubject` to manage event stream
- Handle license plate recognition results in callback delegate:
  - Parse `COMM_UPLOAD_PLATE_RESULT` (0x2800) → `NET_DVR_PLATE_RESULT`
  - Parse `COMM_ITS_PLATE_RESULT` (0x3050) → `NET_ITS_PLATE_RESULT`
- Identify device IP through `NET_DVR_ALARMER` structure, lookup device configuration, extract device name and direction
- Use GBK encoding for plate text (supports Chinese)
- Publish events to Rx stream

**Active Capture Functionality (Optional Extension)**:
- Provide `TriggerManualCaptureAsync` method: Call `NET_DVR_Login_V40` to login to device, call `NET_DVR_ContinuousShoot` to trigger capture, receive results via listen callback

#### 4. Callback Delegate Lifecycle Management (ADDED)

Implement critical resource management logic:

- Use `GCHandle.Alloc()` to pin the `MSGCallBack` delegate, preventing garbage collection during listening
- Save `GCHandle` instance in `StartAsync`
- Release delegate with `GCHandle.Free()` in `StopAsync`
- Add critical comment: *"CRITICAL: Use GCHandle to prevent delegate from being garbage collected. The unmanaged SDK only stores a function pointer; the GC cannot know it is still in use."*
- Prevent duplicate listen startup (check if listen handle already exists)

#### 5. Configuration Separation (ADDED)

Clearly distinguish between listen configuration and device configuration:

- **Listen Configuration** (in `StartAsync` parameters):
  - `listenLocalIp`: Local listen IP address
  - `listenLocalPort`: Local listen port (dedicated port, must not be shared with web application)

- **Device Configuration** (passed via `LicensePlateRecognitionConfig`):
  - `Ip`: Device IP address
  - `UserName`: Device authentication username
  - `Password`: Device authentication password
  - `Port`: Device service port
  - `Channel`: Channel number (fixed to "1")
  - `Name`: Device name (used for event identification)
  - `Direction`: Entry/Exit direction (In/Out)

#### 6. Error Handling and Logging (ADDED)

Implement comprehensive error handling mechanism:

- Check return value after each SDK call, call `NET_DVR_GetLastError` to get error code on failure
- Map error codes to readable error descriptions (reference `HikvisionService.GetErrorDescription`)
- Log all key operations and errors using Serilog (initialization, listen start/stop, device login, callback handling)
- Avoid empty catch blocks, all exceptions must be logged or propagated

#### 7. Encoding Handling (ADDED)

Create encoding helper utility class:

- Unify use of GBK encoding for plate text (`Encoding.GetEncoding("GBK")`)
- Centralize encoding logic in `HikvisionEncodingHelper` class
- Handle Chinese characters and special characters in plate numbers

#### 8. Test Support (ADDED)

Provide test-friendly design:

- Create `MockHikvisionLprService` for unit testing
- Implement `IHikvisionLprService` interface to support dependency injection
- Provide observable event streams for test verification

---

## 影响

### 预期收益

1. **Complete Hikvision LPR Functionality**: The system will have both passive and active capture capabilities, meeting license plate recognition requirements in industrial weighing scenarios

2. **Multi-Device Support**: Single listen port can receive license plate recognition results from multiple Hikvision devices, simplifying configuration and management

3. **Improved Reliability**: Proper callback delegate lifecycle management prevents crashes caused by garbage collection

4. **Easy Integration**: Seamlessly integrate with existing weighing processes (`AttendedWeighingService`) through Rx event streams

5. **Testability**: Interface-based design and Mock implementation support unit and integration testing

### 风险与缓解

| Risk | Impact | Mitigation |
|------|--------|------------|
| HCNetSDK DLL missing or incompatible | High | Document required DLL files and versions in docs, provide installation guide |
| Listen port conflict | Medium | Emphasize dedicated port requirement in docs, provide port configuration recommendations |
| Callback thread issues | Medium | Only collect data in callback, use Rx.Scheduler to marshal events to main thread for processing |
| Memory leaks | Medium | Strict lifecycle management, ensure all resources (GCHandle, handles, Rx subscriptions) are properly released, add memory leak tests |
| Device offline causes initialization failure | Low | Implement `IsOnline` method, check device status before starting listen, provide friendly error messages |
| GBK encoding unavailable on some systems | Low | Detect encoding support, provide fallback or clear error messages |

---

## 成功标准

- [x] `HikvisionLprService` class implements `IHikvisionLprService` interface
- [x] `HikvisionSdk.cs` module contains all required P/Invoke declarations and structure definitions
- [x] `StartAsync` method successfully starts listen service, callback delegate is pinned (GCHandle)
- [x] `StopAsync` method correctly stops listen service, releases all resources (GCHandle, handles)
- [x] `AddOrUpdateDevice` method supports dynamically adding and updating device configurations
- [x] `IsOnline` method can correctly check device connection status
- [x] `PlateRecognized` event stream can receive and publish license plate recognition events
- [x] Callback delegate can correctly handle `COMM_UPLOAD_PLATE_RESULT` and `COMM_ITS_PLATE_RESULT` messages
- [x] Identify device IP through `NET_DVR_ALARMER` structure and lookup corresponding device configuration
- [x] Events contain correct device name, plate number, direction, and timestamp
- [x] All SDK calls have error handling and logging
- [x] Pass memory leak tests (no memory growth during long runs)
- [x] Pass unit tests (Mock scenarios)
- [ ] Pass integration tests (real device or simulated environment) - Optional, requires hardware

---

## 后续步骤

1. **Review and Approve Proposal**: Review this proposal with the team, confirm technical approach and scope
2. **Create Design Document**: If needed, create detailed design document (`design.md`) explaining architectural decisions and implementation details
3. **Implement HCNetSDK Integration**: Create `HikvisionSdk.cs` module, define P/Invoke declarations and structures
4. **Implement Core Service**: Implement `HikvisionLprService` class, provide basic functionality
5. **Write Tests**: Create unit and integration tests to verify functionality correctness
6. **Integration**: Integrate service into existing weighing processes, test end-to-end functionality
7. **Documentation Update**: Update user and development documentation, explain configuration and usage

---

## 参考

- **OpenSpec Proposal**: `HikLpr_OpenSpec_Proposal.md` - Hikvision capture device migration evaluation document
- **Specification**: `license-plate-recognition` - License plate recognition functionality specification
- **Related Interface**: `IHikvisionLprService` - Hikvision LPR service interface
- **Related Service**: `IHikvisionService` - Hikvision security camera service (reference implementation)
- **Related Service**: `ILprAllInOneService` - LPRAllInOne device service (works in parallel)
- **SDK Documentation**: HCNetSDK.h (CH-HCNetSDKV6.1.9.48) - Hikvision SDK header file
