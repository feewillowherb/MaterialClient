# Agent 行为准则

## OpenSpec 工作流（可选）

若采用规范驱动流程，可参考：

- **探索** (`/opsx:explore`)：理解问题与上下文
- **提案** (`/opsx:propose`)：产出 proposal、design、tasks、spec delta
- **实施** (`/opsx:apply`)：按 Spec/Design 实施代码
- **归档** (`/opsx:archive`)：变更完成后归档

```
openspec/
├── specs/          ← 功能规范
├── changes/        ← 待实施变更的规范化定义
└── project.md      ← 项目约定和技术栈
```

- 查看活跃变更：`openspec list`
- 查看功能规范：`openspec list --specs`
- 验证变更：`openspec validate [change-id] --strict`

## 项目约定（服务注册）

- **服务注册**：优先采用 **ABP 集成式 + 隐式 + AutoConstructor**。服务实现类实现 ABP 依赖接口（如 `ITransientDependency`、`ISingletonDependency`）并标注 `[AutoConstructor]`，由 ABP 按约定扫描注册，无需在 Module 或扩展方法中显式注册。参考实现：`SoundDeviceService`（实现 `ISoundDeviceService, ISingletonDependency` + `[AutoConstructor]`）。仅在确有"集成一组服务"的跨模块需求时再考虑扩展方法集中注册。

## 项目约定（应用关闭与资源清理）

本项目涉及大量非托管硬件资源（串口、SDK 句柄、GCHandle），应用关闭时极易导致超时或死锁。

- **SDK 回调禁止调度到 UI 线程**：非托管 SDK 回调中不得使用 `ObserveOn(RxApp.MainThreadScheduler)`。应用关闭时 UI 线程可能不可用，会导致 SDK Close 等待回调线程 → 回调线程等待 UI 线程的死锁。`MessageBus.Current.SendMessage` 本身线程安全，直接在回调线程调用即可。
- **非托管资源单例必须实现 IAsyncDisposable**：所有持有 SDK 句柄、串口、GCHandle 等非托管资源的 `ISingletonDependency` 必须实现 `IAsyncDisposable`，确保 ABP 容器自动清理。
- **SDK P/Invoke 调用必须加超时保护**：`VzLPRClient_Close`、`NET_DVR_StopListen_V30` 等同步 SDK 调用可能无限阻塞，必须用 `Task.Run` + `Task.WhenAny` 加超时。
- **退出顺序**：`App.OnApplicationExit` 必须先停 WebHost → 显式关闭硬件设备 → 再 ABP Shutdown，不能仅依赖 Autofac 自动释放（释放顺序不可控）。
- **StopAsync 超时为秒级**：服务关闭时等待 pending 操作的超时不得超过 5 秒。
