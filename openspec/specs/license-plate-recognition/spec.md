# 车牌识别 规范

## 目的
待定 - 由变更 hikvision-lpr-integration 归档后创建。归档后更新目的。

## 需求

### 需求：海康威视设备配置字段

系统应支持车牌识别设备的海康威视专用配置字段。

#### 场景：用户添加海康威视 LPR 设备配置
- **假设** 系统配置为 `LprDeviceType = Hikvision`
- **当** 用户在设置窗口中添加新的车牌识别设备
- **则** 系统应：
  - 显示海康威视专用配置字段：UserName、Password、Port、Channel
  - 将 Channel 字段默认值设为 "1"
  - 将 Channel 字段显示为只读（禁用）
  - 允许用户输入 UserName、Password、Port

#### 场景：用户查看已有海康威视 LPR 配置
- **假设** 已有填好海康威视专用字段的 LPR 配置
- **且** `LprDeviceType = Hikvision`
- **当** 用户打开设置窗口
- **则** 系统应：
  - 显示所有海康威视专用字段及其已保存值
  - UserName 显示已配置值
  - Password 以掩码显示（PasswordChar="●"）
  - Port 显示已配置值
  - Channel 以只读显示，值为 "1"

#### 场景：用户将设备类型切换为 Vzvision
- **假设** 当前 `LprDeviceType = Hikvision`
- **且** 海康威视专用字段可见
- **当** 用户将 `LprDeviceType` 改为 `Vzvision`（原 `LprAllInOne`，已重命名）
- **则** 系统应：
  - 隐藏海康威视专用字段中的 Channel（及与海康绑定的展示规则）
  - 在内存中保留海康威视字段值（不丢失），以便用户切回海康时使用
  - 显示通用 LPR 字段（Name、Ip、Direction）
  - 显示 Vzvision SDK 连接所需字段：UserName、Password、Port（可编辑，具体标签与掩码规则与实现一致）
  - 无需重启窗口即可更新 UI

---

### 需求：按设备类型动态显示字段

系统应根据所选 `LprDeviceType` 动态显示或隐藏海康威视专用配置字段，以及 Vzvision SDK 连接字段。

#### 场景：设备类型为 Hikvision 时显示海康威视字段
- **假设** 用户在设置窗口中
- **且** `LprDeviceType = Hikvision`
- **则** 系统应显示：UserName（可编辑）、Password（可编辑带掩码）、Port（可编辑）、Channel（只读，固定值 "1"）

#### 场景：设备类型为 Vzvision 时显示 SDK 连接字段且不显示海康 Channel
- **假设** 用户在设置窗口中且 `LprDeviceType = Vzvision`（原 `LprAllInOne`）
- **则** 系统应显示通用字段 Name、Ip、Direction，以及 UserName、Password、Port（用于 `VzLPRClient_Open`）
- **且** 系统不应显示海康专用 Channel 字段

#### 场景：设备类型为 Huaxiazhixin 时不显示海康威视字段
- **假设** 用户在设置窗口中且 `LprDeviceType = Huaxiazhixin`
- **则** 系统不应显示海康威视专用字段，仅显示 Name、Ip、Direction（华夏智信设备配置将在后续变更中实现）

---

### 需求：海康威视字段的 JSON 配置持久化

系统应将海康威视专用配置字段持久化到 JSON，并在加载设置时正确恢复，同时兼容旧数据；对 Vzvision 设备，同一 `LicensePlateRecognitionConfig` 中的 Port/UserName/Password MUST 可被持久化与恢复以支持 SDK 连接。

#### 场景：保存海康威视 LPR 配置到 JSON
- **假设** 用户已配置海康威视 LPR（含 Name、Ip、Direction、UserName、Password、Port、Channel）
- **当** 用户在设置窗口点击保存
- **则** 系统应将所有字段序列化到 `SettingsEntity.LicensePlateRecognitionConfigsJson`，并保存到 SQLite

#### 场景：从 JSON 加载海康威视 LPR 配置
- **假设** 数据库中存在含完整海康威视字段的 JSON
- **当** 用户打开设置窗口
- **则** 系统应反序列化并正确显示所有海康威视字段及通用字段

#### 场景：加载旧配置 JSON（向后兼容）
- **假设** 数据库中存在不含海康威视字段的旧 JSON
- **当** 用户打开设置窗口
- **则** 系统应成功反序列化、正确加载已有字段、将新海康威视字段设为 null，且无需手动数据迁移

#### 场景：混合设备类型配置持久化
- **假设** 用户配置了多台 LPR（含海康威视与 Vzvision）
- **当** 用户保存并重新加载
- **则** 系统应正确序列化/反序列化各设备配置并保持完整

#### 场景：持久化中设备类型枚举迁移
- **假设** 历史 JSON 中存在已弃用的设备类型字面量 `LprAllInOne`
- **当** 用户加载设置或升级后首次启动
- **则** 系统 MUST 将其解析或迁移为 `Vzvision`（或提供等价兼容层），且不得静默丢失该条设备配置

---

### 需求：海康威视 LPR 服务接口定义

系统应定义海康威视 LPR 设备集成的服务接口，为后续实现确立契约。

#### 场景：已定义服务接口
- **则** 系统应在命名空间 `MaterialClient.Common.Services.Hikvision` 中定义 `IHikvisionLprService` 接口
- **且** 声明方法：ConnectAsync、DisconnectAsync、StartListeningAsync、StopListeningAsync
- **且** 声明属性：PlateRecognized（IObservable）、IsConnected
- **且** 为所有成员提供 XML 文档注释，不包含实现代码（实现在单独提案中）

#### 场景：接口遵循 ReactiveUI 模式
- **则** 应使用 IObservable 表示事件流、Task 表示异步操作，并遵循现有 ReactiveUI 模式与依赖注入

**说明**：本需求仅确立接口定义；实际实现与 HCNetSDK 集成由单独提案覆盖。

---

## ADDED Requirements

### Requirement: 识别后动作按供应商能力门控
系统 MUST 在 MessageBus 驱动的车牌识别后置动作中按设备类型进行能力门控，未声明支持某能力的供应商不得触发该能力。

#### Scenario: Vzvision 可触发道闸 I/O 后置动作
- **WHEN** `LprDeviceType = Vzvision` 且识别消息到达 MessageBus 后置动作编排
- **THEN** 系统 MAY 进入道闸 I/O 执行分支（仍受 `EnableGateIo` 配置约束）

#### Scenario: 非 Vzvision 不触发道闸 I/O 后置动作
- **WHEN** `LprDeviceType != Vzvision` 且识别消息到达 MessageBus 后置动作编排
- **THEN** 系统 MUST 跳过道闸 I/O 执行分支，并继续其他不依赖该能力的识别流程

#### Scenario: 非支持设备输出可观测日志
- **WHEN** `LprDeviceType != Vzvision` 且道闸 I/O 功能被启用或进入评估流程
- **THEN** 系统 MUST 输出“当前设备类型暂未支持道闸 I/O 功能”的日志，帮助定位能力差异

---

### 需求：支持通过测试接口注入识别事件

系统应支持通过本地测试接口触发识别事件，以在无真实抓拍回调时驱动车牌识别后续流程。

#### 场景：测试接口发布的识别事件进入统一事件通道
- **当** 测试车牌注入接口接收到合法请求
- **则** 系统应通过 `MessageBus.Current` 发布 `LicensePlateRecognizedMessage`
- **且** 该消息应与真实设备识别消息共享同一消费通道

#### 场景：测试接口事件在来源标识上可区分
- **当** 系统根据测试接口请求构造 `LicensePlateRecognizedMessage`
- **则** 系统应为来源相关字段赋予可识别值（由请求提供或由默认策略补齐）
- **且** 下游日志或联调输出应能区分“测试注入”与“设备回调”来源
