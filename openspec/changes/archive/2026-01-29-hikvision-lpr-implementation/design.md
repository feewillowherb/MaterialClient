# 设计：海康威视 LPR 服务实现

**变更 ID**：`hikvision-lpr-implementation`
**状态**：草稿
**创建日期**：2025-01-29

---

## 架构概览

本文说明海康威视 LPR 服务（`HikvisionLprService`）的架构设计、关键决策与技术实现要点。

### 系统上下文

服务通过 HCNetSDK 与海康威视 LPR 设备集成，通过单一监听端口接收多台设备的车牌识别结果，并通过 Rx 可观察流发布识别事件，以便与称重服务集成。

---

## 关键设计决策

### 1. 多设备共用单一监听端口

**决策**：使用 `NET_DVR_StartListen_V30` 在单一端口上启动监听，接收多台海康威视设备的识别结果。

**理由**：
- `NET_DVR_StartListen_V30` is designed to receive data from multiple devices on a single port
- Simplifies configuration management (only one listen address to configure)
- Reduces resource usage (single listen handle and callback)

**影响**：
- Interface design adjusted to global listen service mode (`StartAsync`/`StopAsync`)
- Device identification required through `NET_DVR_ALARMER` structure in callback

### 2. 使用 GCHandle 固定回调委托

**决策**：使用 `GCHandle.Alloc()` 固定 `MSGCallBack` 委托，避免监听期间被垃圾回收。

**理由**：
- HCNetSDK is unmanaged code, storing only function pointers
- GC cannot detect that unmanaged code is still using the delegate
- If delegate is collected, SDK crashes when calling the function pointer

**风险**：
- Memory leak if `GCHandle` is not released
- Must ensure `GCHandle.Free()` is called in `StopAsync`

### 3. 使用 Rx Subject 作为事件流

**决策**：使用 `Subject<LicensePlateRecognizedEvent>` 作为事件流核心，通过 `IObservable<T>` 暴露。

**理由**：
- Aligns with project's ReactiveUI pattern
- Supports multiple subscribers
- Provides rich operators (filter, buffer, throttle, etc.)
- Easy to test

**类型选择**：
- Using `Subject<T>` (does not replay historical events)
- License plate events are one-time, no need for replay

### 4. 监听配置与设备配置分离

**决策**：明确区分监听配置（本机 IP/端口）与设备配置（设备 IP/端口/凭据）。

**理由**：
- Listen configuration is the client's listen endpoint, controlled by the application
- Device configuration is Hikvision device connection information, used for active capture and device identification
- Clear configuration separation aids understanding and usage

### 5. 车牌中文使用 GBK 编码

**决策**：统一使用 GBK 编码处理车牌文本中的中文字符。

**理由**：
- Hikvision SDK returns Chinese text using GBK encoding
- License plates may contain Chinese characters (e.g., "京A12345")

**风险**：
- Some systems may not support GBK encoding
- Fallback to UTF-8 provided with warning logging

### 6. 回调中的线程安全

**决策**：SDK 回调在非托管线程执行；仅做数据解析与事件发布，不执行阻塞操作。

**理由**：
- Callbacks run in unmanaged thread pool, should not block
- Blocking operations would block SDK's internal processing
- Use Rx scheduler to marshal events to main thread if needed

### 7. 错误处理策略

**决策**：每次 SDK 调用后检查错误，调用 `NET_DVR_GetLastError()` 获取错误码并映射为可读描述。

**理由**：
- HCNetSDK error information is critical for debugging
- Unified error handling aids troubleshooting

---

## 数据流

### 被动抓拍流（设备推送）

1. Application starts → `StartAsync("192.168.1.10", 7200)` → `NET_DVR_StartListen_V30` → Listen handle stored
2. Hikvision device recognizes plate → Pushes data to `192.168.1.10:7200`
3. SDK invokes callback → `MessageCallback(0x2800, pAlarmer, pAlarmInfo, dwBufLen, pUser)`
4. Parse `NET_DVR_ALARMER` → Extract device IP
5. Lookup device config by IP → Get device name and direction
6. Parse `NET_DVR_PLATE_RESULT` → Extract plate number (GBK encoding)
7. Create `LicensePlateRecognizedEvent` → Publish to Rx stream
8. Subscribers receive event → Update weighing records

### 主动抓拍流（应用触发）

1. User triggers capture → `TriggerManualCaptureAsync(config)`
2. `NET_DVR_Login_V40` (login device) → `NET_DVR_ContinuousShoot` (trigger capture)
3. Result received via listen callback (same as passive capture flow)
4. Optional: `NET_DVR_Logout` (logout device)

---

## 错误处理

### 错误场景与处理

| 错误场景 | 检测方式 | 处理策略 | 日志级别 |
|----------------|------------------|-------------------|-----------|
| SDK initialization failed | `NET_DVR_Init()` returns `false` | Throw `InvalidOperationException` | Critical |
| Listen port occupied | `NET_DVR_StartListen_V30()` returns `< 0` | Log error, return `false`, prompt user to check port | Error |
| Device offline | `NET_DVR_Login_V40()` returns `< 0` | `IsOnline()` returns `false`, log error code | Warning |
| Callback exception | `try-catch` wraps callback | Log exception, don't affect other callbacks | Error |
| Memory leak | Memory leak tests | Fix resource leaks | Critical |
| Encoding unavailable | `NotSupportedException` | Fallback to UTF-8, log warning | Warning |

---

## 测试策略

### 单元测试

- **框架**：xUnit
- **Mock**：将 HCNetSDK 调用封装为 `IHikvisionSdk` 接口，使用 Moq
- **用例**：添加/更新设备、IsOnline、StartAsync、StopAsync、回调处理

### 内存泄漏测试

- **方法**：反复启停监听（1000 次）、长时间运行（1 小时）、大量事件（10000 次）
- **验证**：使用 `GC.GetTotalMemory()` 或 dotMemory
- **检查资源**：GCHandle、监听句柄、Rx 订阅

### 集成测试

- **环境**：真实海康威视设备或模拟环境
- **场景**：连接设备、接收识别结果、多设备
- **CI/CD**：标为需手动的测试（依赖硬件）

---

## 性能考虑

### 性能目标

- **Callback Processing Time**: < 1ms (don't block SDK)
- **Event Publishing Latency**: < 10ms (from receive to publish)
- **Memory Usage**: Stable, no continuous growth
- **Multi-Device Support**: At least 10 devices pushing simultaneously

### 优化策略

1. **Callback Optimization**: Only parse data, no blocking I/O
2. **Event Stream Optimization**: Use `RefCount()` for subscription management
3. **Memory Optimization**: Release unmanaged memory promptly, avoid large allocations in callback

---

## 安全考虑

### 安全风险与缓解

| Risk | Mitigation |
|------|-----------|
| Plaintext device credentials | Use encrypted storage (Windows Credential Manager) or config file encryption |
| Unauthorized listen port access | Use firewall, listen only on localhost (127.0.0.1) |
| Callback injection attacks | Validate all input data, no dynamic code execution in callback |
| DLL hijacking | Use strong name signing, place DLLs in application directory |

---

## 部署考虑

### 依赖

- **HCNetSDK DLLs**: HCNetSDK.dll, HCNetSDKCom.dll, etc.
- **Location**: Application directory (same as exe)
- **Version**: CH-HCNetSDKV6.1.9.48 or compatible

### 配置要求

- **Listen Port**: Must be dedicated, not conflict with other services, suggested range 7200-7299
- **Device Configuration**: Each device needs IP, port, username, password
- **Device-Side Configuration**: Devices must be configured to push results to client listen address

### 故障排查

1. **Listen startup fails**: Check port occupation, firewall settings, error code
2. **Device offline**: Use `IsOnline()` to check, verify network, check credentials
3. **Callback not triggered**: Check device push configuration, network connectivity, use Wireshark

---

## 未来增强

1. **Active Capture Functionality**: Implement `TriggerManualCaptureAsync()`
2. **Image Storage**: Automatically save license plate images, upload to cloud (OSS)
3. **Advanced Event Filtering**: Filter by device, direction, time, blacklist/whitelist
4. **Performance Monitoring**: Monitor callback processing time, recognition success rate
5. **Configuration Hot Update**: Add/remove devices at runtime without restart

---

## 参考

- **SDK Documentation**: HCNetSDK.h (CH-HCNetSDKV6.1.9.48)
- **Proposal**: `HikLpr_OpenSpec_Proposal.md`
- **Interface**: `IHikvisionLprService`
- **Reference Implementation**: `HikvisionService` (security camera service)
- **Project Conventions**: `openspec/project.md`
