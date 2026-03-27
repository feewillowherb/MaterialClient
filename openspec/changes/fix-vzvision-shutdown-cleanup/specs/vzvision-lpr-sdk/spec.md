## MODIFIED Requirements

### Requirement: Vz LPR SDK 进程生命周期

系统 MUST 在首次使用 Vzvision LPR 能力前调用全局初始化（`VzLPRClient_Setup`），并在应用退出或明确释放 LPR 子系统时调用清理（`VzLPRClient_Cleanup`），且保证与多设备 `Open`/`Close` 的配对关系可验证。服务 MUST 实现 `IAsyncDisposable`，ABP 容器释放时自动调用 `StopAsync`。SDK 同步调用（`VzLPRClient_Close`、`VzLPRClient_Cleanup`）MUST 有超时保护（建议 3 秒），超时后放弃等待并记录警告，不抛异常。

#### Scenario: 应用退出时释放 SDK

- **WHEN** 桌面应用进程正常退出或设备子系统被关闭
- **THEN** 系统 MUST 关闭所有已打开的设备句柄并调用与 `Setup` 对称的清理路径，避免原生资源泄漏

#### Scenario: IAsyncDisposable 自动清理

- **WHEN** ABP Autofac 容器执行 ShutdownAsync 释放 VzvisionLprService 单例
- **THEN** 系统 MUST 自动调用 `StopAsync` 完成资源清理

#### Scenario: SDK 调用超时不死锁

- **WHEN** `VzLPRClient_Close` 或 `VzLPRClient_Cleanup` 因设备网络异常而阻塞
- **THEN** 系统 MUST 在超时（3 秒）后放弃等待并记录警告日志，MUST NOT 无限阻塞

---

### Requirement: Vzvision 设备连接与车牌回调

系统 MUST 使用 `VzLPRClient_Open`（及配置中的 IP、端口、用户名、密码）建立与一体机的连接，注册 `VzLPRClient_SetPlateInfoCallBack`，在回调中从 `TH_PlateResult` 解析车牌与颜色信息，并发布 `LicensePlateRecognizedMessage`，且 `DeviceType` MUST 为 `LprDeviceType.Vzvision`（或项目最终采用的等价枚举值）。本变更**不**包含 **`VzLPRClient_StartRealPlay`**：系统 MUST NOT 为实现车牌业务而调用实时视频预览/浏览（无 HWND 预览窗口路径）。

#### Scenario: 无实时画面预览

- **WHEN** Vzvision LPR 集成按本变更运行
- **THEN** 系统 MUST NOT 依赖 `VzLPRClient_StartRealPlay` 完成车牌识别与 MessageBus 上报

#### Scenario: 收到识别结果并发布 MessageBus

- **WHEN** SDK 以回调方式上报至少一条有效车牌结果
- **THEN** 系统 MUST 向 ReactiveUI `MessageBus` 发送 `LicensePlateRecognizedMessage`，其中包含非空车牌号、映射后的颜色类型（若可解析）、来自配置的设备名称、以及正确的设备类型枚举值

#### Scenario: 回调线程安全

- **WHEN** 非托管回调在任意线程执行
- **THEN** 系统 MUST 直接在回调线程调用 `MessageBus.Current.SendMessage`（线程安全），MUST NOT 使用 `ObserveOn(RxApp.MainThreadScheduler)` 调度到 UI 线程，避免应用关闭时死锁
