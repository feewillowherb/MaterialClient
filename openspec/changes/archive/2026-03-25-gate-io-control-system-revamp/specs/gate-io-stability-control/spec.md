# 道闸 IO 稳定状态控制

## ADDED Requirements

### Requirement: 车辆上磅锁定逻辑
系统 MUST 在车辆上磅但未进入稳定状态时自动锁定所有道闸 IO 并持续写入 0 信号。

#### Scenario: 识别后上磅未稳定触发锁定
- **WHEN** 收到车牌识别事件（`LicensePlateRecognizedMessage`）且当前地磅状态为 `WaitingForStability`
- **THEN** 系统 MUST 转换道闸状态为 `Locked`
- **AND** MUST 锁定所有进出口道闸 IO 通道
- **AND** MUST 向所有锁定道闸持续写入 0 信号

#### Scenario: 锁定期间持续写入 0
- **WHEN** 道闸状态为 `Locked`
- **THEN** 系统 MUST 每 100ms 向所有道闸 IO 通道写入 0 信号
- **AND** MUST 使用定时器（如 `Observable.Interval`）实现周期性写入
- **AND** MUST 在定时器回调中捕获并记录写入失败异常

#### Scenario: 锁定后记录车辆进入方向
- **WHEN** 进入 `Locked` 状态
- **THEN** 系统 MUST 记录触发锁定的车牌识别消息中的 `Direction` 字段（`In` 或 `Out`）
- **AND** MUST 将该方向保存到状态上下文中，用于后续开闸决策

### Requirement: 地磅稳定后解锁开闸逻辑
系统 MUST 在地磅进入稳定状态后释放锁定，并根据车辆进入方向打开对应的出口道闸。

#### Scenario: 地磅稳定后自动解锁
- **WHEN** 收到地磅稳定状态事件（`StatusChangedMessage` 且状态为 `WeightStabilized`）且当前道闸状态为 `Locked`
- **THEN** 系统 MUST 停止持续写入 0 的定时器
- **AND** MUST 转换道闸状态为 `Opening`

#### Scenario: 进口识别后打开出口道闸
- **WHEN** 地磅稳定且记录的车辆进入方向为 `In`（进口）
- **THEN** 系统 MUST 调用 IO 控制器打开出口方向的道闸
- **AND** MUST 使用 500ms 自动复位的开闸脉冲

#### Scenario: 出口识别后打开进口道闸
- **WHEN** 地磅稳定且记录的车辆进入方向为 `Out`（出口）
- **THEN** 系统 MUST 调用 IO 控制器打开进口方向的道闸
- **AND** MUST 使用 500ms 自动复位的开闸脉冲

#### Scenario: 开闸完成后重置状态
- **WHEN** 开闸操作完成（IO 控制器返回成功）
- **THEN** 系统 MUST 转换道闸状态为 `Idle`
- **AND** MUST 广播状态变更消息

### Requirement: 地磅状态事件订阅
系统 MUST 订阅地磅状态变更事件流，实时响应地磅状态变化。

#### Scenario: 订阅 StatusChangedMessage
- **WHEN** 道闸 IO 状态管理服务启动
- **THEN** 系统 MUST 通过 `MessageBus.Current.Listen<StatusChangedMessage>()` 订阅地磅状态事件

#### Scenario: 处理地磅状态 WaitingForStability
- **WHEN** 收到 `StatusChangedMessage` 且状态为 `WaitingForStability`
- **THEN** 系统 MUST 检查是否收到车牌识别事件
- **AND** 如果已收到识别事件，MUST 进入 `Locked` 状态

#### Scenario: 处理地磅状态 WeightStabilized
- **WHEN** 收到 `StatusChangedMessage` 且状态为 `WeightStabilized`
- **THEN** 系统 MUST 检查当前道闸状态
- **AND** 如果当前为 `Locked`，MUST 执行解锁和开闸逻辑

#### Scenario: 处理地磅状态 OffScale
- **WHEN** 收到 `StatusChangedMessage` 且状态为 `OffScale`
- **THEN** 系统 MUST 检查当前道闸状态
- **AND** 如果当前为 `Locked` 或 `Opening`，MUST 转换为 `Idle` 状态

### Requirement: 车牌识别事件订阅
系统 MUST 订阅车牌识别事件流，识别车辆进入方向并触发状态机逻辑。

#### Scenario: 订阅 LicensePlateRecognizedMessage
- **WHEN** 道闸 IO 状态管理服务启动
- **THEN** 系统 MUST 通过 `MessageBus.Current.Listen<LicensePlateRecognizedMessage>()` 订阅车牌识别事件

#### Scenario: 记录车辆进入方向
- **WHEN** 收到 `LicensePlateRecognizedMessage` 消息
- **THEN** 系统 MUST 提取并记录消息中的 `Direction` 字段（`In` 或 `Out`）
- **AND** MUST 将方向信息保存到状态上下文中

#### Scenario: 识别后检查地磅状态触发锁定
- **WHEN** 收到 `LicensePlateRecognizedMessage` 消息
- **THEN** 系统 MUST 查询当前地磅状态
- **AND** 如果地磅状态为 `WaitingForStability`，MUST 立即进入 `Locked` 状态

### Requirement: 超时保护机制
系统 MUST 在锁定状态下提供超时保护，防止无限期锁定。

#### Scenario: 锁定超时自动进入异常状态
- **WHEN** 道闸状态为 `Locked` 且持续时间超过 60 秒（可配置）
- **THEN** 系统 MUST 停止持续写入 0 的定时器
- **AND** MUST 转换状态为 `Error`
- **AND** MUST 记录超时错误日志

#### Scenario: 超时阈值可配置
- **WHEN** 系统管理员需要调整超时阈值
- **THEN** 系统 MUST 支持通过配置文件（如 `appsettings.json`）设置 `GateIO:LockTimeoutSeconds` 参数
- **AND** MUST 使用配置值作为超时判断依据

### Requirement: 方向识别逻辑
系统 MUST 根据车辆进入方向确定打开哪个出口的道闸。

#### Scenario: 进口进入打开出口道闸
- **WHEN** 车辆从进口方向进入（`Direction = In`）且地磅稳定
- **THEN** 系统 MUST 打开出口方向的道闸 IO 通道
- **AND** MUST 不打开进口道闸

#### Scenario: 出口进入打开进口道闸
- **WHEN** 车辆从出口方向进入（`Direction = Out`）且地磅稳定
- **THEN** 系统 MUST 打开进口方向的道闸 IO 通道
- **AND** MUST 不打开出口道闸

#### Scenario: 方向信息缺失时的默认行为
- **WHEN** `LicensePlateRecognizedMessage` 消息中 `Direction` 字段为空或无效
- **THEN** 系统 MUST 记录警告日志
- **AND** MUST 使用默认方向（如 `In`）或根据配置决定行为
- **AND** MUST 不触发开闸操作

### Requirement: 状态协调逻辑
系统 MUST 正确协调车牌识别事件和地磅状态事件的时序关系。

#### Scenario: 先识别后上磅的正常流程
- **WHEN** 先收到 `LicensePlateRecognizedMessage`，后收到地磅状态 `WaitingForStability`
- **THEN** 系统 MUST 在地磅进入 `WaitingForStability` 时进入 `Locked` 状态

#### Scenario: 先上磅后识别的延迟流程
- **WHEN** 先收到地磅状态 `WaitingForStability`，后收到 `LicensePlateRecognizedMessage`
- **THEN** 系统 MUST 在收到识别消息时进入 `Locked` 状态

#### Scenario: 地磅稳定但未收到识别消息
- **WHEN** 收到地磅状态 `WeightStabilized` 但从未收到 `LicensePlateRecognizedMessage`
- **THEN** 系统 MUST 不触发开闸操作
- **AND** MUST 保持当前状态（如 `Idle`）

#### Scenario: 多次识别事件的处理
- **WHEN** 在 `Locked` 状态下收到多个 `LicensePlateRecognizedMessage` 消息
- **THEN** 系统 MUST 忽略后续识别消息（仅使用第一次识别的方向）
- **AND** MUST 记录调试日志
