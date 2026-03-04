# 任务：海康威视 LPR 服务实现

**变更 ID**：`hikvision-lpr-implementation`
**总任务数**：12
**预估工期**：5–7 天

---

## 任务概览

本实现将分阶段完成海康威视 LPR 服务的开发。 First establish the SDK integration foundation, then implement core service functionality, and finally perform testing and integration. During implementation, pay special attention to callback delegate lifecycle management to avoid memory leaks and crashes.

---

## 阶段 1：SDK 集成基础

### 任务 1.1：创建 HikvisionSdk.cs 模块

**状态**：已完成
**优先级**：高
**预估**：4 小时

**描述**：
创建集中的 HCNetSDK P/Invoke 声明模块，定义所有需要的原生 SDK 调用与数据结构。

**步骤**：
1. Create `HikvisionSdk.cs` file in `MaterialClient.Common/Services/Hikvision/` directory
2. Add `System.Runtime.InteropServices` namespace reference
3. Define `MSGCallBack` delegate type:
   ```csharp
   internal delegate void MSGCallBack(int lCommand, IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser);
   ```
4. Declare core APIs using `DllImport`:
   - `NET_DVR_Init` / `NET_DVR_Cleanup`
   - `NET_DVR_Login_V40` / `NET_DVR_Logout`
   - `NET_DVR_StartListen_V30` / `NET_DVR_StopListen_V30`
   - `NET_DVR_ContinuousShoot`
   - `NET_DVR_GetLastError`
5. Define structures (using `StructLayout(LayoutKind.Sequential)`):
   - `NET_DVR_USER_LOGIN_INFO`
   - `NET_DVR_DEVICEINFO_V40`
   - `NET_DVR_SNAPCFG`
   - `NET_DVR_ALARMER`
   - `NET_DVR_PLATE_RESULT`
   - `NET_ITS_PLATE_RESULT`
   - `NET_DVR_JPEGPARA`
6. Define constants:
   - `COMM_UPLOAD_PLATE_RESULT = 0x2800`
   - `COMM_ITS_PLATE_RESULT = 0x3050`
7. Reference `NET_DVR` static class in `HikvisionService` to ensure correct structure layout

**验收**：
- [x] All P/Invoke declarations compile successfully
- [x] Structure fields match HCNetSDK.h header file
- [x] Delegate and function signatures are correct

**产出**： `MaterialClient.Common/Services/Hikvision/HikvisionSdk.cs`

---

### 任务 1.2：创建编码辅助工具类

**状态**：已完成
**优先级**：中
**预估**：1 小时

**描述**：
创建统一的编码处理工具类，用于处理车牌文本中的中文字符。

**步骤**：
1. Create `HikvisionEncodingHelper.cs` file in `MaterialClient.Common/Utils/` directory
2. Implement static methods:
   - `string GetStringFromPtr(IntPtr ptr, int maxLength)` - Read GBK-encoded string from unmanaged pointer
   - `byte[] GetBytes(string text)` - Convert string to GBK-encoded byte array
3. Add encoding detection and fallback logic:
   - Try to get GBK encoding (`Encoding.GetEncoding("GBK")`)
   - If failed, use UTF-8 encoding and log warning

**验收**：
- [x] Can correctly handle license plates containing Chinese
- [x] Graceful fallback when encoding is unavailable

**产出**： `MaterialClient.Common/Utils/HikvisionEncodingHelper.cs`

---

## 阶段 2：核心服务实现

### 任务 2.1：实现 HikvisionLprService 基础结构

**状态**：已完成
**优先级**：高
**预估**：2 小时

**描述**：
创建 `HikvisionLprService` 类，实现基础类结构、依赖注入与状态管理。

**步骤**：
1. Create `HikvisionLprService.cs` file in `MaterialClient.Common/Services/Hikvision/` directory
2. Use AutoConstructor attribute to generate constructor injection:
   - `ILogger<HikvisionLprService>? logger`
   - `ISettingsService? settingsService` (optional)
3. Implement `IHikvisionLprService` interface
4. Add private fields:
   - `ConcurrentDictionary<string, LicensePlateRecognitionConfig> _deviceConfigs`
   - `int _listenHandle = -1`
   - `GCHandle? _callbackHandle`
   - `Subject<LicensePlateRecognizedEvent> _plateRecognizedSubject`
   - `bool _isInitialized`
5. Implement `PlateRecognized` property: return `_plateRecognizedSubject.AsObservable()`
6. Add `Dispose` pattern (optional, or use `IDisposable`)

**验收**：
- [x] Class compiles successfully
- [x] Dependency injection works properly
- [x] Event stream can be subscribed to

**产出**： `MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs` (basic structure)

---

### Task 2.2: Implement SDK Lifecycle Management

**Status**: Completed
**Priority**: High
**Estimated**: 2 hours

**Description**:
Implement HCNetSDK initialization and cleanup logic.

**Steps**:
1. Create private method `EnsureInitialized()`:
   - Check `_isInitialized` flag
   - If not initialized, call `HikvisionSdk.NET_DVR_Init()`
   - If failed, throw `InvalidOperationException`
   - Register `AppDomain.CurrentDomain.ProcessExit` handler to call `Cleanup()` on process exit
   - Set `_isInitialized = true`
2. Create private method `Cleanup()`:
   - If initialized, call `HikvisionSdk.NET_DVR_Cleanup()`
   - Set `_isInitialized = false`
3. Call `EnsureInitialized()` at the beginning of `StartAsync`
4. Call `Cleanup()` at the end of `StopAsync`
5. Add logging for all critical operations

**验收**：
- [x] SDK can be initialized successfully
- [x] Cleanup is called correctly on process exit
- [x] Clear error messages on initialization failure

**产出**： Update `HikvisionLprService.cs`

---

### Task 2.3: Implement Device Management Functionality

**Status**: Completed
**Priority**: High
**Estimated**: 2 hours

**Description**:
Implement add, update, and online check functionality for devices.

**Steps**:
1. Implement `AddOrUpdateDevice(LicensePlateRecognitionConfig config)` method:
   - Validate config is not null (`ArgumentNullException.ThrowIfNull`)
   - Validate config is valid (`config.IsValid()`)
   - Use device IP as key, add or update in `_deviceConfigs` dictionary
   - Log operation
2. Implement `IsOnline(LicensePlateRecognitionConfig config)` method:
   - Validate config is not null
   - Call `EnsureInitialized()`
   - Call private method `TryLogin(config, out int userId)`
   - Return `true` if `userId >= 0`, otherwise return `false`
3. Implement private method `TryLogin(config, out userId)`:
   - Build `NET_DVR_USER_LOGIN_INFO` structure
   - Call `HikvisionSdk.NET_DVR_Login_V40()`
   - Return `userId` if successful, otherwise return `-1`
   - On failure, log error code and error description

**验收**：
- [x] Can successfully add device configuration
- [x] Can update existing device configuration
- [x] `IsOnline` can correctly detect device status
- [x] Error handling and logging work properly

**产出**： Update `HikvisionLprService.cs`

---

### Task 2.4: Implement Listen Service Start and Stop

**Status**: Completed
**Priority**: High
**Estimated**: 4 hours

**Description**:
Implement listen service start and stop logic, including callback delegate lifecycle management (CRITICAL).

**Steps**:
1. Implement `StartAsync(string listenLocalIp, int listenLocalPort)` method:
   - Call `EnsureInitialized()`
   - Check if already started (`_listenHandle >= 0`), log warning and return `false` if already started
   - Create callback delegate instance: `MSGCallBack callback = MessageCallback;`
   - **CRITICAL**: Use `GCHandle.Alloc(callback)` to pin delegate, save to `_callbackHandle`
   - Call `HikvisionSdk.NET_DVR_StartListen_V30(listenLocalIp, listenLocalPort, callback, IntPtr.Zero)`
   - If return value `< 0`, log error and release `GCHandle`
   - Save listen handle to `_listenHandle`
   - Log success
   - Return `listenHandle >= 0`
2. Implement `StopAsync()` method:
   - Check if already started (`_listenHandle >= 0`)
   - If started, call `HikvisionSdk.NET_DVR_StopListen_V30(_listenHandle)`
   - **CRITICAL**: If `_callbackHandle.HasValue`, call `_callbackHandle.Value.Free()` to release delegate
   - Reset `_listenHandle = -1` and `_callbackHandle = null`
   - Log operation
3. Add critical comment:
   ```csharp
   // CRITICAL: Use GCHandle to prevent delegate from being garbage collected.
   // The unmanaged SDK only stores a function pointer; the GC cannot know it is still in use.
   ```

**验收**：
- [x] Can successfully start listen service
- [x] Can successfully stop listen service
- [x] Duplicate listen startup is detected and rejected
- [x] Callback delegate is not garbage collected during listening
- [x] All resources are correctly released on stop

**产出**： Update `HikvisionLprService.cs`

---

### Task 2.5: Implement Callback Handling and Event Publishing

**Status**: Completed
**Priority**: High
**Estimated**: 6 hours

**Description**:
Implement SDK callback delegate to handle license plate recognition results and publish to Rx event stream.

**Steps**:
1. Implement callback method `MessageCallback(int lCommand, IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser)`:
   - Wrap entire callback in `try-catch` to prevent unhandled exceptions from crashing the process
   - Dispatch to different handler methods based on `lCommand`:
     - `0x2800` → `HandlePlateResult(pAlarmer, pAlarmInfo, dwBufLen)`
     - `0x3050` → `HandleItsPlateResult(pAlarmer, pAlarmInfo, dwBufLen)`
2. Implement private method `HandlePlateResult(IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen)`:
   - Use `Marshal.PtrToStructure` to parse `NET_DVR_ALARMER` structure
   - Extract device IP from `NET_DVR_ALARMER` (need to reference SDK documentation to determine fields)
   - Lookup device configuration: `_deviceConfigs.TryGetValue(deviceIp, out var config)`
   - Use `Marshal.PtrToStructure` to parse `NET_DVR_PLATE_RESULT` structure
   - Use `HikvisionEncodingHelper` to extract plate number (GBK encoding)
   - Create `LicensePlateRecognizedEvent`:
     - `PlateNumber` = plate number
     - `DeviceName` = `config?.Name ?? "Unknown"`
     - `Direction` = `config?.Direction ?? LicensePlateDirection.In`
     - `Timestamp` = `DateTime.Now`
   - Call `_plateRecognizedSubject.OnNext(event)`
   - Log operation
3. Implement private method `HandleItsPlateResult(IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen)`:
   - Similar to `HandlePlateResult`, but parse `NET_ITS_PLATE_RESULT` structure
   - Note: Structure fields are different, need to reference SDK documentation
4. Add thread-safety protection in callback:
   - Callback executes in unmanaged thread
   - Only perform data parsing and event publishing
   - Do not perform time-consuming operations
5. Add image saving logic (optional, if saving license plate images is needed)

**验收**：
- [x] Callback can correctly receive license plate recognition results
- [x] Can distinguish results from different devices
- [x] Event stream receives correct events
- [x] Events contain correct plate number, device name, direction, and timestamp
- [x] Callback exceptions do not crash the process
- [x] Detailed logging is present

**产出**： Update `HikvisionLprService.cs`

---

### Task 2.6: Implement Error Handling and Logging

**Status**: Completed
**Priority**: Medium
**Estimated**: 2 hours

**Description**:
Complete error handling and logging for all methods.

**Steps**:
1. Add parameter validation for all public methods:
   - `ArgumentNullException.ThrowIfNull()` for required parameters
   - Check strings are not empty (`string.IsNullOrWhiteSpace`)
2. Add error checking for all SDK calls:
   - Check return value indicates failure
   - On failure, call `HikvisionSdk.NET_DVR_GetLastError()` to get error code
   - Use `GetErrorDescription(errorCode)` to convert error code to readable description (reference `HikvisionService`)
3. Add detailed logging:
   - Initialization success/failure
   - Listen start/stop
   - Device add/update
   - Device online check results
   - License plate recognition results received in callback
   - All errors and exceptions
4. Avoid empty catch blocks:
   - All catch blocks must log exceptions
   - Consider whether exceptions need to be propagated

**验收**：
- [x] All errors are caught and logged
- [x] Logs provide sufficient debugging information
- [x] No empty catch blocks

**产出**： Update `HikvisionLprService.cs`

---

## 阶段 3： Testing and Integration

### Task 3.1: Create Mock Implementation

**Status**: Completed
**Priority**: Medium
**Estimated**: 1 hour

**Description**:
Create `MockHikvisionLprService` for unit testing.

**Steps**:
1. Create `MockHikvisionLprService.cs` file in `MaterialClient.Common.Tests/Mocks/` directory
2. Implement `IHikvisionLprService` interface
3. Add test-friendly properties and methods:
   - `List<LicensePlateRecognizedEvent> RecognizedEvents` - Record all recognition events
   - `bool IsOnlineReturnValue` - Control return value
   - `bool StartAsyncReturnValue` - Control return value
   - `void SimulatePlateRecognition(LicensePlateRecognizedEvent event)` - Simulate license plate recognition
4. Implement simple interface methods (mainly for test verification)

**验收**：
- [x] Mock class compiles successfully
- [x] Can be used for unit testing
- [x] Can simulate license plate recognition events

**产出**： `MaterialClient.Common.Tests/Mocks/MockHikvisionLprService.cs`

---

### Task 3.2: Write Unit Tests

**Status**: Completed
**Priority**: Medium
**Estimated**: 4 hours

**Description**:
Write unit tests for `HikvisionLprService` to verify core functionality.

**Steps**:
1. Create `HikvisionLprServiceTests.cs` file in `MaterialClient.Common.Tests/Services/Hikvision/` directory
2. Write test cases:
   - `AddOrUpdateDevice_ShouldAddNewDevice` - Verify adding device
   - `AddOrUpdateDevice_ShouldUpdateExistingDevice` - Verify updating device
   - `IsOnline_ShouldReturnTrueWhenDeviceIsOnline` - Verify online detection (need to Mock SDK calls)
   - `StartAsync_ShouldInitializeSdkAndStartListening` - Verify listen startup (need to Mock SDK calls)
   - `StopAsync_ShouldStopListeningAndCleanup` - Verify listen stop (need to Mock SDK calls)
   - `MessageCallback_ShouldPublishEvent` - Verify callback handling (use private accessor or InternalsVisibleTo)
3. Use Moq or similar Mock framework to simulate SDK calls (may need to wrap SDK as interface)
4. Verify event stream receives correct events

**验收**：
- [x] All tests pass
- [x] Tests cover major functionality paths
- [x] Tests cover error scenarios

**产出**： `MaterialClient.Common.Tests/Services/Hikvision/HikvisionLprServiceTests.cs`

---

### Task 3.3: Write Memory Leak Tests

**Status**: Completed
**Priority**: High
**Estimated**: 3 hours

**Description**:
Write long-running tests to verify the service's memory management is correct.

**Steps**:
1. Create `HikvisionLprServiceMemoryLeakTests.cs` file in `MaterialClient.Common.Tests/Services/Hikvision/` directory
2. Write test cases:
   - `StartStopRepeatedly_ShouldNotLeakMemory` - Start and stop listening repeatedly, check for memory growth
   - `LongRunningListener_ShouldNotLeakMemory` - Run listening for a long time, check for memory growth
   - `ManyDeviceEvents_ShouldNotLeakMemory` - Receive many license plate recognition events, check for memory growth
3. Use memory analysis tools or simple methods (like `GC.GetTotalMemory`) to detect memory leaks
4. Ensure all resources are correctly released:
   - GCHandle
   - Listen handle
   - Rx subscriptions

**验收**：
- [x] No continuous memory growth after repeated start/stop
- [x] Memory is stable after long runs
- [x] Memory is stable after processing many events
- [x] All resources are correctly released

**产出**： `MaterialClient.Common.Tests/Services/Hikvision/HikvisionLprServiceMemoryLeakTests.cs`

---

### Task 3.4: Write Integration Tests (Optional)

**Status**: Completed
**Priority**: Low
**Estimated**: 4 hours

**Description**:
Write integration tests to verify interaction with real Hikvision devices (requires real device or simulated environment).

**Steps**:
1. Create `HikvisionLprIntegrationTests.cs` file in `MaterialClient.Common.Tests/IntegrationTests/` directory
2. Configure test environment (real device IP and credentials)
3. Write test cases:
   - `ConnectToDevice_ShouldSucceed` - Verify device connection
   - `StartListeningAndReceivePlateResult_ShouldSucceed` - Verify listening and receiving results
   - `MultipleDevices_ShouldReceiveEventsFromAll` - Verify multi-device support
4. Add test configuration file (not committed to version control)

**验收**：
- [x] Can connect to real device
- [x] Can receive license plate recognition results
- [x] Multi-device scenarios work properly

**产出**： `MaterialClient.Common.Tests/IntegrationTests/HikvisionLprIntegrationTests.cs`

---

## 阶段 4： Documentation and Delivery

### Task 4.1: Update User Documentation

**Status**: Completed
**Priority**: Medium
**Estimated**: 2 hours

**Description**:
Update user documentation to explain how to configure and use Hikvision LPR functionality.

**Steps**:
1. Add Hikvision LPR configuration instructions to project documentation
2. Explain listen port configuration requirements (dedicated port, must not conflict with other services)
3. Explain device configuration steps (IP, port, username, password, channel)
4. Provide troubleshooting guide (common errors and solutions)
5. List required DLL files (HCNetSDK.dll, HCNetSDKCom.dll, etc.)

**验收**：
- [x] Documentation is clear and easy to understand
- [x] Contains all necessary configuration information
- [x] Provides troubleshooting guide

**产出**： Update project documentation

---

### Task 4.2: Code Review and Optimization

**Status**: Completed
**Priority**: Medium
**Estimated**: 2 hours

**Description**:
Perform code review, optimize implementation, and fix issues.

**Steps**:
1. Self-review code, check:
   - Code style follows project conventions
   - Are there performance issues
   - Are there security risks
   - Are comments clear
2. Perform performance optimization (if needed)
3. Fix discovered issues
4. Prepare code review checklist

**验收**：
- [x] Code follows project conventions
- [x] No obvious performance issues
- [x] No security risks
- [x] Comments are clear

**产出**： Optimized code

---

## 进度跟踪

**Phase 1 Progress**: 2/2 tasks completed (100%)
**Phase 2 Progress**: 6/6 tasks completed (100%)
**Phase 3 Progress**: 4/4 tasks completed (100%)
**Phase 4 Progress**: 2/2 tasks completed (100%)
**Overall Progress**: 14/14 tasks (100%)
