# 道闸 IO 状态管理

## ADDED Requirements

### Requirement: 道闸状态定义
系统 MUST 定义四种明确的道闸 IO 状态：`Idle`（空闲）、`Locked`（锁定）、`Opening`（开闸中）、`Error`（异常）。

#### Scenario: 状态枚举定义
- **WHEN** 系统初始化道闸状态管理器
- **THEN** 系统 MUST 支持 `GateIOState` 枚举，包含 `Idle`、`Locked`、`Opening`、`Error` 四个值

#### Scenario: 状态语义清晰性
- **WHEN** 查询任意状态的语义
- **THEN** `Idle` 表示无车辆在磅，道闸可响应识别
- **AND** `Locked` 表示车辆上磅未稳定，所有道闸被锁定并持续写入 0
- **AND** `Opening` 表示地磅稳定后正在开闸
- **AND** `Error` 表示异常状态需要人工干预

### Requirement: 状态机转换规则
系统 MUST 强制执行严格的状态转换规则，禁止非法状态跳转。

#### Scenario: Idle 到 Locked 的合法转换
- **WHEN** 当前状态为 `Idle` 且收到车牌识别事件
- **THEN** 系统 MUST 转换状态为 `Locked`

#### Scenario: Locked 到 Opening 的合法转换
- **WHEN** 当前状态为 `Locked` 且收到地磅稳定事件（`WeightStabilized`）
- **THEN** 系统 MUST 转换状态为 `Opening`

#### Scenario: Opening 到 Idle 的合法转换
- **WHEN** 当前状态为 `Opening` 且开闸操作完成
- **THEN** 系统 MUST 转换状态为 `Idle`

#### Scenario: Locked 到 Error 的合法转换
- **WHEN** 当前状态为 `Locked` 且发生超时或异常（如超过 60 秒未稳定）
- **THEN** 系统 MUST 转换状态为 `Error`

#### Scenario: Error 到 Idle 的合法转换
- **WHEN** 当前状态为 `Error` 且收到人工重置命令
- **THEN** 系统 MUST 转换状态为 `Idle`

#### Scenario: 非法状态转换拒绝
- **WHEN** 尝试执行未定义的状态转换（如直接从 `Idle` 到 `Error`）
- **THEN** 系统 MUST 拒绝该转换并记录错误日志

### Requirement: 状态变更事件广播
系统 MUST 在每次状态变更后通过 ReactiveUI MessageBus 广播状态变更消息。

#### Scenario: 状态变更消息格式
- **WHEN** 道闸状态从 `StateA` 变更为 `StateB`
- **THEN** 系统 MUST 发送 `GateIOStateChangedMessage`，包含 `PreviousState`、`CurrentState`、`Timestamp` 字段

#### Scenario: 多订阅者支持
- **WHEN** 多个订阅者监听状态变更消息
- **THEN** 系统 MUST 向所有订阅者广播状态变更

#### Scenario: 订阅者处理失败隔离
- **WHEN** 某个订阅者处理状态变更消息时抛出异常
- **THEN** 系统 MUST 捕获异常并记录日志，不影响其他订阅者接收消息

### Requirement: 状态查询接口
系统 MUST 提供同步和异步的状态查询接口。

#### Scenario: 同步查询当前状态
- **WHEN** 调用 `GetState()` 方法
- **THEN** 系统 MUST 立即返回当前 `GateIOState` 值

#### Scenario: 异步查询当前状态
- **WHEN** 调用 `GetStateAsync()` 方法
- **THEN** 系统 MUST 返回 `Task<GateIOState>`，结果为当前状态值

#### Scenario: 状态查询不触发变更
- **WHEN** 仅查询状态而不执行操作
- **THEN** 系统 MUST 不改变当前状态或触发任何事件

### Requirement: 状态持久化
系统 MUST 将当前状态持久化到内存中，确保服务重启后可恢复初始状态。

#### Scenario: 服务重启后状态恢复
- **WHEN** 服务异常停止后重新启动
- **THEN** 系统 MUST 将状态重置为 `Idle`（默认初始状态）

#### Scenario: 状态持久化不依赖外部存储
- **WHEN** 系统运行中发生状态变更
- **THEN** 系统 MUST 仅将状态保存在内存中（如 `BehaviorSubject<GateIOState>`），不写入数据库或文件

### Requirement: 状态机生命周期管理
系统 MUST 提供服务的启动和停止方法，正确管理状态机和事件订阅的生命周期。

#### Scenario: 服务启动时初始化状态
- **WHEN** 调用 `StartAsync()` 方法
- **THEN** 系统 MUST 初始化状态为 `Idle`
- **AND** 订阅车牌识别和地磅状态事件流
- **AND** 启动状态机处理循环

#### Scenario: 服务停止时清理资源
- **WHEN** 调用 `StopAsync()` 方法
- **THEN** 系统 MUST 取消所有事件订阅
- **AND** 释放定时器资源（如锁定状态下的写入定时器）
- **AND** 将状态重置为 `Idle`

#### Scenario: 服务停止后拒绝操作
- **WHEN** 服务已停止且尝试调用状态变更方法
- **THEN** 系统 MUST 拒绝操作并返回错误或抛出异常
