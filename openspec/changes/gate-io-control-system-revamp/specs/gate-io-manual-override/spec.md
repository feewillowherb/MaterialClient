# 道闸 IO 人工干预接口

## ADDED Requirements

### Requirement: 状态查询接口
系统 MUST 提供接口查询道闸 IO 的当前状态。

#### Scenario: 同步查询当前状态
- **WHEN** 调用 `GetState()` 方法
- **THEN** 系统 MUST 立即返回当前 `GateIOState` 枚举值（`Idle`、`Locked`、`Opening`、`Error` 之一）

#### Scenario: 异步查询当前状态
- **WHEN** 调用 `GetStateAsync()` 方法
- **THEN** 系统 MUST 返回 `Task<GateIOState>`，结果为当前状态值
- **AND** MUST 在 10ms 内完成查询

#### Scenario: 查询包含状态上下文
- **WHEN** 调用 `GetStateWithDetailsAsync()` 方法
- **THEN** 系统 MUST 返回包含状态和上下文信息的对象
- **AND** 该对象 MUST 包含：`State`（当前状态）、`LockedDirection`（锁定时记录的进入方向）、`LockedDuration`（锁定持续时间）、`LastError`（最近的错误消息）

#### Scenario: 查询不改变状态
- **WHEN** 调用任意状态查询方法
- **THEN** 系统 MUST 不改变当前状态或触发任何事件

### Requirement: 异常状态重置接口
系统 MUST 提供接口重置道闸 IO 的异常状态。

#### Scenario: 重置 Error 状态为 Idle
- **WHEN** 当前状态为 `Error` 且调用 `ResetAsync()` 方法
- **THEN** 系统 MUST 将状态转换为 `Idle`
- **AND** MUST 关闭所有道闸 IO（调用 `CloseGateAsync()`）
- **AND** MUST 停止任何运行的定时器（如锁定写入定时器）
- **AND** MUST 广播 `GateIOStateChangedMessage` 消息

#### Scenario: 重置时记录操作日志
- **WHEN** 调用 `ResetAsync()` 方法
- **THEN** 系统 MUST 记录信息日志："人工重置道闸 IO 状态"
- **AND** MUST 记录操作时间戳和操作者（如果可用）

#### Scenario: 非 Error 状态下重置
- **WHEN** 当前状态不是 `Error` 且调用 `ResetAsync()` 方法
- **THEN** 系统 MUST 执行重置操作（强制转换为 `Idle`）
- **AND** MUST 记录警告日志："在非 Error 状态下执行重置"

#### Scenario: 重置操作返回结果
- **WHEN** 调用 `ResetAsync()` 方法
- **THEN** 系统 MUST 返回 `Task<bool>` 表示操作是否成功
- **AND** 成功时返回 `true`，失败时返回 `false`

### Requirement: 强制解锁接口
系统 MUST 提供接口强制解除道闸的锁定状态。

#### Scenario: 强制解锁 Locked 状态
- **WHEN** 当前状态为 `Locked` 且调用 `ForceUnlockAsync()` 方法
- **THEN** 系统 MUST 停止持续写入 0 的定时器
- **AND** MUST 将状态转换为 `Idle`
- **AND** MUST 广播 `GateIOStateChangedMessage` 消息

#### Scenario: 强制解锁时询问确认
- **WHEN** 调用 `ForceUnlockAsync()` 方法
- **THEN** 系统 MUST 在 UI 中显示确认对话框："确认要强制解锁道闸吗？当前可能有车辆在磅上"
- **AND** MUST 在用户确认后才执行解锁操作

#### Scenario: 强制解锁操作记录
- **WHEN** 调用 `ForceUnlockAsync()` 方法
- **THEN** 系统 MUST 记录警告日志："人工强制解锁道闸 IO，可能有安全风险"
- **AND** MUST 记录操作时间戳和原因（如果提供）

#### Scenario: 非 Locked 状态下强制解锁
- **WHEN** 当前状态不是 `Locked` 且调用 `ForceUnlockAsync()` 方法
- **THEN** 系统 MUST 记录警告日志："当前状态未锁定，无需解锁"
- **AND** MUST 返回成功（不执行实际操作）

### Requirement: 状态变更事件订阅
系统 MUST 允许订阅者监听道闸 IO 状态变更事件。

#### Scenario: 订阅状态变更事件
- **WHEN** 订阅者调用 `MessageBus.Current.Listen<GateIOStateChangedMessage>()`
- **THEN** 系统 MUST 向该订阅者发送所有后续状态变更消息

#### Scenario: 状态变更消息格式
- **WHEN** 道闸状态发生变更
- **THEN** 系统 MUST 发送 `GateIOStateChangedMessage` 消息
- **AND** 该消息 MUST 包含：`PreviousState`（变更前状态）、`CurrentState`（变更后状态）、`Timestamp`（时间戳）、`Reason`（变更原因）

#### Scenario: 多订阅者并行接收
- **WHEN** 多个订阅者监听状态变更事件
- **THEN** 系统 MUST 向所有订阅者广播消息
- **AND** MUST 并行通知（不阻塞其他订阅者）

#### Scenario: 取消订阅
- **WHEN** 订阅者释放订阅（`IDisposable.Dispose()`）
- **THEN** 系统 MUST 停止向该订阅者发送后续消息

### Requirement: 人工干预权限控制
系统 MUST 提供人工干预接口的权限控制机制。

#### Scenario: 所有操作员可重置
- **WHEN** 任意操作员角色调用重置接口
- **THEN** 系统 MUST 允许操作（当前版本不限制权限）

#### Scenario: 操作审计日志
- **WHEN** 执行任意人工干预操作（重置、强制解锁）
- **THEN** 系统 MUST 记录审计日志，包含：操作类型、操作时间、操作者用户名（如果可用）、操作结果

#### Scenario: 未来权限扩展预留
- **WHEN** 系统需要添加权限控制
- **THEN** 系统 MUST 支持通过配置或策略模式扩展权限验证逻辑
- **AND** MUST 不破坏现有接口签名

### Requirement: 人工操作 UI 集成
系统必须在 UI 中提供人工干预操作的入口。

#### Scenario: 状态栏显示当前状态
- **WHEN** 道闸 IO 状态发生变更
- **THEN** 系统 MUST 在状态栏显示当前状态文本（如"道闸：空闲"、"道闸：锁定中"、"道闸：异常"）
- **AND** MUST 根据状态使用不同颜色（Idle=绿色、Locked=黄色、Error=红色）

#### Scenario: 异常状态显示重置按钮
- **WHEN** 当前状态为 `Error`
- **THEN** 系统 MUST 在状态栏显示"重置"按钮
- **AND** MUST 按钮点击时调用 `ResetAsync()` 方法

#### Scenario: 锁定状态显示强制解锁按钮
- **WHEN** 当前状态为 `Locked`
- **THEN** 系统 MUST 在状态栏显示"强制解锁"按钮（可选，根据配置决定是否显示）
- **AND** MUST 按钮点击时调用 `ForceUnlockAsync()` 方法并显示确认对话框

#### Scenario: 操作结果显示提示
- **WHEN** 用户执行重置或强制解锁操作
- **THEN** 系统 MUST 在 UI 中显示操作结果提示（如"重置成功"、"强制解锁成功"）
- **AND** MUST 在操作失败时显示错误消息

### Requirement: 人工操作错误处理
系统 MUST 正确处理人工干预操作中的错误情况。

#### Scenario: 重置操作失败处理
- **WHEN** 调用 `ResetAsync()` 方法时发生异常
- **THEN** 系统 MUST 捕获异常并记录错误日志
- **AND** MUST 返回 `false` 表示操作失败
- **AND** MUST 在 UI 中显示错误提示："重置失败：{错误消息}"

#### Scenario: 强制解锁操作失败处理
- **WHEN** 调用 `ForceUnlockAsync()` 方法时发生异常
- **THEN** 系统 MUST 捕获异常并记录错误日志
- **AND** MUST 返回 `false` 表示操作失败
- **AND** MUST 在 UI 中显示错误提示："强制解锁失败：{错误消息}"

#### Scenario: IO 控制器调用失败处理
- **WHEN** 重置或解锁操作中调用 IO 控制器失败
- **THEN** 系统 MUST 记录错误日志
- **AND** MUST 尝试恢复状态（如保持在 `Error` 状态）
- **AND** MUST 通知用户操作失败

### Requirement: 人工操作并发控制
系统 MUST 正确处理并发的状态变更请求。

#### Scenario: 自动状态变更与人工重置并发
- **WHEN** 系统自动将状态转换为 `Error` 的同时，用户调用 `ResetAsync()`
- **THEN** 系统 MUST 使用线程安全的状态转换
- **AND** MUST 仅执行一次状态变更（后到的请求基于最新状态）

#### Scenario: 多次重置请求去重
- **WHEN** 用户快速多次点击重置按钮
- **THEN** 系统 MUST 仅执行一次重置操作
- **AND** MUST 忽略后续重复请求

#### Scenario: 锁定状态下的人工干预
- **WHEN** 状态为 `Locked` 且用户调用强制解锁
- **THEN** 系统 MUST 停止定时器并转换状态
- **AND** MUST 确保定时器线程安全停止

### Requirement: 人工操作历史记录
系统 MUST 记录人工干预操作的历史。

#### Scenario: 记录重置操作历史
- **WHEN** 执行 `ResetAsync()` 操作
- **THEN** 系统 MUST 将操作记录到历史列表中
- **AND** 历史记录 MUST 包含：时间戳、操作类型（Reset）、操作前状态、操作后状态

#### Scenario: 记录强制解锁操作历史
- **WHEN** 执行 `ForceUnlockAsync()` 操作
- **THEN** 系统 MUST 将操作记录到历史列表中
- **AND** 历史记录 MUST 包含：时间戳、操作类型（ForceUnlock）、操作前状态、操作后状态

#### Scenario: 查询操作历史
- **WHEN** 调用 `GetOperationHistoryAsync()` 方法
- **THEN** 系统 MUST 返回最近的操作历史列表（默认最近 100 条）
- **AND** MUST 按时间倒序排列

#### Scenario: 操作历史持久化
- **WHEN** 系统重启或关闭
- **THEN** 系统 MUST 将操作历史保存到文件或数据库（可选功能）
- **AND** MUST 在系统启动时加载历史记录
