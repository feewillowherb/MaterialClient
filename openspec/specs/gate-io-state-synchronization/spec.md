# Purpose

提供道闸 I/O 控制与称重状态机的同步能力，根据称重状态自动触发道闸动作。

## ADDED Requirements

### Requirement: 订阅称重状态变化
系统 MUST 订阅 `StatusChangedMessage` 以实时感知称重状态转换。

#### Scenario: 启动时订阅状态变化消息
- **WHEN** `LprGateIoControlService.StartAsync()` 被调用
- **THEN** 系统 MUST 订阅 `MessageBus.Current.Listen<StatusChangedMessage>()`
- **AND** 系统 MUST 为每个状态变化调用 `OnStatusChanged(AttendedWeighingStatus)`

#### Scenario: 停止时释放状态变化订阅
- **WHEN** `LprGateIoControlService.StopAsync()` 被调用
- **THEN** 系统 MUST 释放 `_statusSubscription` 订阅
- **AND** 系统 MUST 设置 `_statusSubscription = null`

### Requirement: 状态转换与会话同步
系统 MUST 根据称重状态转换同步道闸会话状态。

#### Scenario: OffScale 状态转换
- **WHEN** 接收到 `StatusChangedMessage(OffScale)`
- **THEN** 系统 MUST 调用 `ClearSession()` 清理会话状态
- **AND** 系统 MUST 记录日志："称重状态 OffScale，清理道闸会话"

#### Scenario: WaitingForStability 状态转换
- **WHEN** 接收到 `StatusChangedMessage(WaitingForStability)`
- **THEN** 系统 MUST 设置内部标志"禁止 LRP 开闸"
- **AND** 系统 MUST 记录日志："称重状态 WaitingForStability，禁止 LRP 开闸"

#### Scenario: WeightStabilized 状态转换
- **WHEN** 接收到 `StatusChangedMessage(WeightStabilized)`
- **THEN** 系统 MUST 保持"禁止 LRP 开闸"标志
- **AND** 系统 MUST 记录日志："称重状态 WeightStabilized，禁止 LRP 开闸"

#### Scenario: WaitingForDeparture 状态转换
- **WHEN** 接收到 `StatusChangedMessage(WaitingForDeparture)` 且会话已激活且出口未开闸（`ExitOpened = false`）
- **THEN** 系统 MUST 计算出口侧（`exitSide = EntrySide == A ? B : A`）
- **AND** 系统 MUST 查找出口侧的 LPR 配置（`Direction == exitSide` 且 `EnableGateIo == true`）
- **AND** 系统 MUST 调用 Vzvision SDK 打开出口道闸（`SetIoOutputAutoRespAsync`，500ms 脉冲）
- **AND** 系统 MUST 设置 `ExitOpened = true`
- **AND** 系统 MUST 记录日志："称重状态 WaitingForDeparture，打开出口道闸: ExitSide={ExitSide}"

#### Scenario: WaitingForDeparture 状态转换（出口已开闸）
- **WHEN** 接收到 `StatusChangedMessage(WaitingForDeparture)` 且 `ExitOpened = true`
- **THEN** 系统 MUST 跳过出口开闸（防止重复开闸）
- **AND** 系统 MUST 记录调试日志："出口道闸已开，跳过重复触发"

### Requirement: 状态同步错误处理
系统 MUST 优雅处理状态同步过程中的错误，不影响称重主流程。

#### Scenario: 状态变化处理失败不影响称重
- **WHEN** `OnStatusChanged()` 执行过程中发生异常
- **THEN** 系统 MUST 记录错误日志
- **AND** 系统 MUST 不抛出异常或中断称重状态机
- **AND** 系统 MUST 继续处理后续状态变化消息

#### Scenario: 出口开闸失败不影响会话状态
- **WHEN** 在 `WaitingForDeparture` 状态下打开出口道闸失败（SDK 调用异常）
- **THEN** 系统 MUST 记录错误日志
- **AND** 系统 MUST 保持 `ExitOpened = false`（允许用户通过遥控器手动开闸）
- **AND** 系统 MUST 不阻止后续状态转换

### Requirement: 状态门控逻辑
系统 MUST 根据当前称重状态决定是否允许 LRP 触发开闸。

#### Scenario: OffScale 状态允许入口开闸
- **WHEN** 当前称重状态为 `OffScale` 且会话未激活且收到 LRP 识别事件
- **THEN** 系统 MUST 允许入口开闸（创建会话并调用 SDK）

#### Scenario: WaitingForStability 状态禁止 LRP 开闸
- **WHEN** 当前称重状态为 `WaitingForStability` 且收到 LRP 识别事件
- **THEN** 系统 MUST 拒绝开闸并记录日志："称重状态 WaitingForStability，禁止 LRP 开闸"

#### Scenario: WeightStabilized 状态禁止 LRP 开闸
- **WHEN** 当前称重状态为 `WeightStabilized` 且收到 LRP 识别事件
- **THEN** 系统 MUST 拒绝开闸并记录日志："称重状态 WeightStabilized，禁止 LRP 开闸"

#### Scenario: WaitingForDeparture 状态禁止入口 LRP 开闸
- **WHEN** 当前称重状态为 `WaitingForDeparture` 且收到 LRP 识别事件
- **THEN** 系统 MUST 拒绝开闸并记录日志："称重状态 WaitingForDeparture，禁止 LRP 开闸（等待自动打开出口）"
