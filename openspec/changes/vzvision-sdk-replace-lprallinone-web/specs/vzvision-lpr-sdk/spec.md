# Delta Spec: vzvision-lpr-sdk（新增能力）

## ADDED Requirements

### Requirement: Vz LPR SDK 进程生命周期

系统 MUST 在首次使用 Vzvision LPR 能力前调用全局初始化（`VzLPRClient_Setup`），并在应用退出或明确释放 LPR 子系统时调用清理（`VzLPRClient_Cleanup`），且保证与多设备 `Open`/`Close` 的配对关系可验证。

#### Scenario: 应用退出时释放 SDK

- **WHEN** 桌面应用进程正常退出或设备子系统被关闭
- **THEN** 系统 MUST 关闭所有已打开的设备句柄并调用与 `Setup` 对称的清理路径，避免原生资源泄漏

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
- **THEN** 系统 MUST NOT 在违反 UI 或服务线程假设的前提下直接更新 UI 或访问非线程安全单例；发布消息或调用业务逻辑 MUST 经约定的同步/调度机制

---

### Requirement: 主动抓拍（软件触发）

系统 MUST 在用户或业务触发 `ILprDevice.TriggerCaptureAsync`（Vzvision 实现）时，对目标设备句柄调用 **`VzLPRClient_ForceTrigger`**（与 `public static extern int VzLPRClient_ForceTrigger(int handle);` 一致），且 MUST NOT 默认使用 `VzLPRClient_ForceTriggerEx`，除非经文档或联调确认必须采用 TCP 扩展触发；且 MUST NOT 再依赖 HTTP Comet 或 `CallDeviceStatus` 响应携带 `manualTrigger`。

#### Scenario: 触发抓拍成功路径

- **WHEN** 配置有效且设备已连接
- **THEN** 系统 MUST 返回已完成的触发调用（或按 SDK 契约可判定为已下发触发），识别结果 MUST 仍仅通过 `LicensePlateRecognizedMessage` 传递

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
