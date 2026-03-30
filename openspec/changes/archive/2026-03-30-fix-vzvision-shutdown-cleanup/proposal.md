## Why

`VzvisionLprService` 在应用关闭时导致 ABP Shutdown 无限阻塞。根本原因是 `OnPlateInfo` 回调中使用 `ObserveOn(RxApp.MainThreadScheduler)` 调度到 UI 线程，关闭时 UI 线程不可用形成死锁；同时服务未实现 `IAsyncDisposable`，`StopAsync` 不会被 ABP 容器自动调用，SDK 句柄和连接资源泄漏。日志证实：`OnApplicationExit` 输出 "正在关闭 ABP 应用程序..." 后再无后续日志，进程被强制终止。

## What Changes

- 修复 `VzvisionLprService.OnPlateInfo` 回调中的 `ObserveOn(RxApp.MainThreadScheduler)` 死锁问题
- 让 `VzvisionLprService` 实现 `IAsyncDisposable`，确保 ABP 容器自动调用清理
- 为 `StopAsync` 中的 SDK 同步调用（`VzLPRClient_Close`、`VzLPRClient_Cleanup`）添加超时保护
- 在 `App.OnApplicationExit` 中显式调用 `DeviceManagerService.CloseAsync`，确保硬件先于 ABP 关闭

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

（无 spec 级别行为变更，仅为内部实现修复）

## Impact

- **`VzvisionLprService.cs`**：修复回调死锁、实现 `IAsyncDisposable`、SDK 调用加超时
- **`HikvisionLprService.cs`**：实现 `IAsyncDisposable`（同样存在未自动清理的问题）
- **`DeviceManagerService.cs`**：实现 `IAsyncDisposable`，委托清理子设备
- **`App.axaml.cs`**：退出流程中增加显式硬件关闭步骤
