## 1. 修复 VzvisionLprService OnPlateInfo 回调死锁

- [x] 1.1 移除 `OnPlateInfo` 中 `Observable.Return(Unit.Default).ObserveOn(RxApp.MainThreadScheduler).Subscribe(...)` 模式，改为直接调用 `MessageBus.Current.SendMessage`
- [x] 1.2 移除 `VzvisionLprService.cs` 中不再需要的 `using System.Reactive` 和 `using ReactiveUI` 引用（如适用）

## 2. VzvisionLprService 实现 IAsyncDisposable

- [x] 2.1 让 `VzvisionLprService` 实现 `IAsyncDisposable`，`DisposeAsync` 内调用 `StopAsync`
- [x] 2.2 在 `StopAsync` 中将 `_started = false` 移到 SDK 调用之前，防止关闭期间回调进入业务逻辑

## 3. VzvisionLprService SDK 调用添加超时保护

- [x] 3.1 将 `VzLPRClient_Close` 调用包裹在 `Task.Run` + `Task.WhenAny` 中，超时 3 秒
- [x] 3.2 将 `VzLPRClient_Cleanup` 调用包裹在 `Task.Run` + `Task.WhenAny` 中，超时 3 秒
- [x] 3.3 超时后记录警告日志，不抛异常

## 4. HikvisionLprService 实现 IAsyncDisposable

- [x] 4.1 让 `HikvisionLprService` 实现 `IAsyncDisposable`，`DisposeAsync` 内调用 `StopAsync`

## 5. App.OnApplicationExit 显式关闭硬件设备

- [x] 5.1 在 `App.axaml.cs` 的 `OnApplicationExit` 中，WebHost 停止后、ABP Shutdown 前，从 ServiceProvider 获取 `IDeviceManagerService` 并调用 `CloseAsync()`
- [x] 5.2 将整体超时从 3 秒调整为 10 秒，为硬件关闭留足时间
- [x] 5.3 为每个关闭步骤添加耗时日志，便于排查
