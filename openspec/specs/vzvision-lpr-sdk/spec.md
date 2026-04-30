# Vzvision LPR SDK 规范

## 目的
定义 Vzvision 一体机 SDK 集成契约，覆盖生命周期、连接回调、触发抓拍、在线状态与旧 HTTP 路由依赖移除。

## 需求

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

---

### Requirement: 主动抓拍（软件触发）

系统 MUST 在用户或业务触发 `ILprDevice.TriggerCaptureAsync`（Vzvision 实现）时，对目标设备句柄调用 **`VzLPRClient_ForceTrigger`**（与 `public static extern int VzLPRClient_ForceTrigger(int handle);` 一致），且 MUST NOT 默认使用 `VzLPRClient_ForceTriggerEx`，除非经文档或联调确认必须采用 TCP 扩展触发；且 MUST NOT 再依赖 HTTP Comet 或 `CallDeviceStatus` 响应携带 `manualTrigger`。在执行上述逻辑前，系统 MUST 先检查 `SystemSettings.EnableTriggerLprCapture` 功能开关，仅当其为 `true` 时才执行抓拍。

#### Scenario: 触发抓拍成功路径

- **WHEN** `EnableTriggerLprCapture` 为 `true`
- **AND** 配置有效且设备已连接
- **THEN** 系统 MUST 返回已完成的触发调用（或按 SDK 契约可判定为已下发触发），识别结果 MUST 仍仅通过 `LicensePlateRecognizedMessage` 传递

#### Scenario: 功能开关禁用时跳过触发

- **WHEN** `EnableTriggerLprCapture` 为 `false`
- **THEN** 系统 MUST NOT 调用 `VzLPRClient_ForceTrigger`
- **AND** 系统 MUST NOT 调用 `VzLPRClient_ForceTriggerEx`

---

### Requirement: LPR 主动抓拍功能开关

系统 MUST 在 `SystemSettings` 中提供 `EnableTriggerLprCapture` 布尔配置项，作为通用 LPR 主动抓拍功能的全局总开关。`AttendedWeighingService.TriggerLprCaptureForAllAsync` 方法 MUST 在方法体最前面检查该配置，当配置为 `false` 时 MUST NOT 执行任何抓拍逻辑，仅记录信息级日志并返回。默认值 MUST 为 `false`。

#### Scenario: 功能开关启用时正常执行抓拍

- **WHEN** `SystemSettings.EnableTriggerLprCapture` 为 `true`
- **AND** 调用 `TriggerLprCaptureForAllAsync(phase)`
- **THEN** 系统 MUST 继续执行后续守卫检查（设备类型、服务注入、设备配置）及抓拍逻辑

#### Scenario: 功能开关禁用时跳过抓拍

- **WHEN** `SystemSettings.EnableTriggerLprCapture` 为 `false`
- **AND** 调用 `TriggerLprCaptureForAllAsync(phase)`
- **THEN** 系统 MUST 记录信息级日志（包含 phase 参数）
- **AND** 系统 MUST NOT 执行任何后续抓拍逻辑
- **AND** 方法 MUST 正常返回

#### Scenario: 配置文件缺少该字段时使用默认值

- **WHEN** 配置文件中未包含 `EnableTriggerLprCapture` 字段
- **THEN** 系统 MUST 将该值解析为 `false`
- **AND** 行为 MUST 与显式设置为 `false` 一致

---

### Requirement: Vzvision 在线状态

系统 MUST 基于 `VzLPRClient_IsConnected`（及可选业务策略，如最近一次识别时间窗口）实现 `ILprDeviceOnlineStatusService` 对 Vzvision 设备类型的在线查询；语义可与原「Comet 轮询即在线」不同，但 MUST 在行为上可测试且一致。

#### Scenario: 查询单台设备在线

- **WHEN** 调用方提供有效的 `LicensePlateRecognitionConfig`（含 IP）且当前系统设备类型为 Vzvision
- **THEN** 系统 MUST 返回布尔在线状态，且不得依赖已移除的 `CallDeviceStatus` HTTP 轮询

---

### Requirement: 移除对 LprAllInOne HTTP 端点的依赖

系统 MUST NOT 将 `MinimalWebHostService` 中的 `/api/CarLicense/CallDeviceMessage` 或 `/api/CarLicense/CallDeviceStatus` 作为 Vzvision 一体机的必用集成方式；上述路由中与 Vzvision 相关的实现 MUST 被删除或不再注册。华夏智信、地磅测试等其它路由不受影响。

#### Scenario: 非海康场景下宿主仍可服务其它端点

- **WHEN** `MinimalWebHostService` 已启动且仍存在华夏智信回调或地磅测试 API 等需求
- **THEN** 系统 MUST 继续提供这些端点所需行为，且不得因删除 Vzvision Web 路由而破坏其功能
