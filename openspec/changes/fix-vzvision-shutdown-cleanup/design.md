## Context

`VzvisionLprService` 和 `HikvisionLprService` 通过 P/Invoke 调用非托管 SDK（`VzLPRSDK.dll`、`HCNetSDK.dll`），持有设备连接句柄、GCHandle 等非托管资源。当前两个服务均未实现 `IAsyncDisposable`，ABP 容器在 `ShutdownAsync` 时不会自动调用 `StopAsync`，导致 SDK 资源泄漏。

此外，`VzvisionLprService.OnPlateInfo` 回调中使用 `ObserveOn(RxApp.MainThreadScheduler)` 调度到 UI 线程发送 MessageBus 消息。应用关闭时 UI 线程不可用，SDK 的 `VzLPRClient_Close`/`Cleanup` 等待回调线程退出，回调线程等待 UI 线程，形成死锁。

`App.OnApplicationExit` 的退出流程中没有显式调用 `DeviceManagerService.CloseAsync`，硬件设备关闭完全依赖 ABP 容器的不可控释放顺序。

## Goals / Non-Goals

**Goals:**

- 消除 `VzvisionLprService.OnPlateInfo` 中的死锁风险
- 让 `VzvisionLprService` 和 `HikvisionLprService` 在 ABP Shutdown 时自动清理资源
- 确保 `App.OnApplicationExit` 中硬件设备先于 ABP 关闭
- SDK 同步 P/Invoke 调用有超时保护，避免无限阻塞

**Non-Goals:**

- 不修改 `AttendedWeighingService.StopAsync` 的 5 分钟超时（属于另一个问题）
- 不重构 `DeviceManagerService` 的整体架构
- 不修改 `HikvisionService`（抓拍服务，使用登录/登出模式，无持久连接）

## Decisions

### Decision 1: OnPlateInfo 回调直接发送 MessageBus 消息

移除 `ObserveOn(RxApp.MainThreadScheduler)` 调度。`MessageBus.Current.SendMessage` 本身线程安全，不需要 UI 线程。

**替代方案**：使用 `TryObserveOn` 带超时 → 排除，增加复杂度且 MessageBus 不需要 UI 线程。

### Decision 2: VzvisionLprService 和 HikvisionLprService 实现 IAsyncDisposable

`DisposeAsync` 内部调用已有的 `StopAsync()`。ABP Autofac 容器在释放单例时自动调用。

**替代方案**：仅在 `DeviceManagerService` 实现 `IAsyncDisposable` → 排除，如果 `DeviceManagerService` 未被 Autofac 释放（释放顺序问题），子服务仍然泄漏。

### Decision 3: SDK 同步调用用 Task.Run + WhenAny 加超时

`VzLPRClient_Close` 和 `VzLPRClient_Cleanup` 包裹在 `Task.Run` 中，配合 3 秒超时。超时后放弃等待并记录警告日志，不抛异常。

### Decision 4: App.OnApplicationExit 显式调用 DeviceManagerService.CloseAsync

在 `WebHostService.DisposeAsync()` 之后、`AbpApplication.ShutdownAsync()` 之前，从 DI 容器获取 `IDeviceManagerService` 并调用 `CloseAsync()`。这确保硬件设备在 ABP 容器释放之前已关闭，避免 SDK 回调线程与 Autofac 释放之间的竞争。

### Decision 5: StopAsync 中设置 _started = false 在 SDK 调用之前

当前 `StopAsync` 在所有 SDK 调用完成后才设置 `_started = false`。改为在 SDK 调用之前设置，防止关闭过程中新回调进入业务逻辑。

## Risks / Trade-offs

- **[SDK 超时后资源泄漏]** → 超时后 SDK 句柄可能未完全释放。可接受，因为进程即将退出，OS 会回收资源。
- **[VzLPRClient_Cleanup 后 SDK 不可用]** → 正常行为，应用关闭后不需要 SDK。
- **[MessageBus 消息时序变化]** → 移除 UI 线程调度后，消息可能在非 UI 线程处理。下游订阅者（如 `GateIoControlService`）已经在 `Task.Run` 中处理消息，不受影响。
