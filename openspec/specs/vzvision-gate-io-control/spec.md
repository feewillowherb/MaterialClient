## ADDED Requirements

### Requirement: LPR 道闸 I/O 通用配置项
系统 MUST 提供面向 LPR 设备的道闸 I/O 通用配置能力，包括是否启用开关与 `IoChannel` 通道号，并将其持久化到现有 LPR 配置存储中。

#### Scenario: 在 Vzvision 配置中展示并编辑 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置 `LprDeviceType = Vzvision`
- **THEN** 系统 MUST 显示"是否启用道闸 I/O 功能"开关与 `IoChannel` 输入项，并允许编辑

#### Scenario: 非 Vzvision 设备不暴露 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置非 Vzvision 设备类型
- **THEN** 系统 MUST 不显示或不允许编辑道闸 I/O 配置项

#### Scenario: 保存并加载 I/O 配置
- **WHEN** 用户保存并重新打开设置
- **THEN** 系统 MUST 正确序列化和反序列化 `EnableGateIo` 与 `IoChannel`，保持值不丢失

#### Scenario: 非 Vzvision 设备保留配置但当前不执行
- **WHEN** 非 Vzvision 设备存在 `EnableGateIo` 与 `IoChannel` 配置
- **THEN** 系统 MUST 保留并加载配置值，但在运行时按能力门控判定为当前不支持

#### Scenario: 配置启动前验证
- **WHEN** 应用启动或配置保存后
- **THEN** 系统 MUST 验证进出口道闸配置是否成对存在
- **AND** MUST 验证每个方向最多配置一个道闸
- **AND** MUST 验证所有启用的道闸都有有效的 `IoChannel` 值

#### Scenario: 配置验证失败时禁用功能
- **WHEN** 配置验证失败
- **THEN** 系统 MUST 不启动道闸 IO 服务
- **AND** MUST 在状态栏显示错误提示
- **AND** MUST 记录详细的验证错误日志

### Requirement: 识别后触发开闸信号
系统 MUST 根据地磅稳定状态和车辆进入方向决定是否开闸，而非简单地在识别后立即开闸。

#### Scenario: 地磅稳定后根据方向开闸
- **WHEN** 收到地磅稳定状态事件（`WeightStabilized`）且道闸状态为 `Locked`
- **THEN** 系统 MUST 根据锁定时记录的车辆进入方向决定开闸目标
- **AND** MUST 调用 `VzLPRClient_SetIOOutputAutoResp(handle, targetIoChannel, 500)` 向对应出口道闸下发开闸脉冲

#### Scenario: 识别后不立即开闸
- **WHEN** 收到车牌识别事件但地磅状态为 `WaitingForStability`
- **THEN** 系统 MUST 不调用开闸接口
- **AND** MUST 进入 `Locked` 状态并锁定所有道闸

#### Scenario: 启用配置后识别触发锁定
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = true`，且收到车牌识别消息
- **THEN** 系统 MUST 检查地磅状态
- **AND** 如果地磅未稳定，MUST 进入 `Locked` 状态而非立即开闸

#### Scenario: 未启用配置时不触发开闸
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = false`，且收到车辆识别事件
- **THEN** 系统 MUST 不调用 I/O 开闸接口

#### Scenario: 非 Vzvision 设备记录未支持日志
- **WHEN** 设备类型不是 Vzvision，且识别后进入道闸 I/O 后置动作评估
- **THEN** 系统 MUST 不调用 I/O 开闸接口，并 MUST 记录"当前设备类型未支持道闸 I/O 功能"的日志

### Requirement: I/O 控制职责分离
系统 MUST 通过状态管理服务协调 IO 控制，状态服务订阅识别事件和地磅状态事件。

#### Scenario: 状态服务订阅识别和地磅事件
- **WHEN** `GateIOStateService` 启动
- **THEN** 系统 MUST 订阅 `LicensePlateRecognizedMessage` 和 `StatusChangedMessage` 事件
- **AND** MUST 根据两个事件的协调结果决定 IO 控制操作

#### Scenario: 识别服务与 I/O 服务解耦
- **WHEN** 发生识别事件处理流程
- **THEN** 系统 MUST 通过 MessageBus 发布/订阅与独立 I/O 控制服务执行开闸动作，而非在识别解析逻辑中直接调用 SDK 下发

#### Scenario: I/O 服务通过 MessageBus 订阅接收触发
- **WHEN** 识别服务发布车牌识别消息或专用 I/O 触发消息
- **THEN** I/O 控制服务 MUST 通过 MessageBus 订阅接收并执行业务门控判断

### Requirement: 地磅状态事件订阅
系统 MUST 订阅地磅状态变更事件，以实现基于稳定状态的道闸控制逻辑。

#### Scenario: 订阅地磅状态事件
- **WHEN** `GateIOStateService` 启动
- **THEN** 系统 MUST 通过 `MessageBus.Current.Listen<StatusChangedMessage>()` 订阅地磅状态事件

#### Scenario: 响应地磅稳定状态
- **WHEN** 收到 `StatusChangedMessage` 且状态为 `WeightStabilized`
- **THEN** 系统 MUST 检查当前道闸状态
- **AND** 如果为 `Locked`，MUST 执行解锁和开闸逻辑

### Requirement: 道闸锁定状态下的持续写入 0
系统 MUST 在锁定状态下持续向所有道闸 IO 写入 0 信号，确保道闸保持关闭。

#### Scenario: 锁定状态下周期性写入 0
- **WHEN** 道闸状态为 `Locked`
- **THEN** 系统 MUST 每 100ms 向所有道闸 IO 通道调用 `VzLPRClient_SetIOOutput` 写入 0 值
- **AND** MUST 使用定时器（如 `Observable.Interval`）实现周期性写入

#### Scenario: 解锁时停止写入定时器
- **WHEN** 道闸状态从 `Locked` 转换为其他状态
- **THEN** 系统 MUST 停止并释放持续写入 0 的定时器

### Requirement: 状态机模式管理道闸状态
系统 MUST 使用状态机模式管理道闸的锁定/解锁状态转换。

#### Scenario: 状态定义和转换
- **WHEN** `GateIOStateService` 运行
- **THEN** 系统 MUST 支持 `Idle`、`Locked`、`Opening`、`Error` 四种状态
- **AND** MUST 强制执行状态转换规则（如 Locked 仅能转换为 Opening 或 Error）

#### Scenario: 状态变更事件广播
- **WHEN** 道闸状态发生变更
- **THEN** 系统 MUST 通过 MessageBus 广播 `GateIOStateChangedMessage` 消息

### Requirement: 异常状态处理和人工重置
系统 MUST 提供异常状态的人工干预接口。

#### Scenario: 异常状态自动检测
- **WHEN** 锁定状态持续时间超过 60 秒（可配置）
- **THEN** 系统 MUST 自动转换状态为 `Error`
- **AND** MUST 记录超时错误日志

#### Scenario: 人工重置接口
- **WHEN** 状态为 `Error` 且用户调用 `ResetAsync()` 方法
- **THEN** 系统 MUST 将状态重置为 `Idle`
- **AND** MUST 关闭所有道闸并广播状态变更消息

#### Scenario: 强制解锁接口
- **WHEN** 状态为 `Locked` 且用户调用 `ForceUnlockAsync()` 方法
- **THEN** 系统 MUST 强制解除锁定并转换状态为 `Idle`
- **AND** MUST 要求用户确认操作
