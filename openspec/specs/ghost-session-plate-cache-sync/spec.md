# Purpose

定义道闸幽灵会话重置后，称重车牌缓存与 UI 推荐通过领域事件同步的行为契约，使「道闸已废弃的旧会话车牌」不再作为 `LockedAt` 候选。

## ADDED Requirements

### Requirement: 幽灵会话重置领域事件

系统 MUST 在道闸侧成功执行幽灵会话重置（`TryResetGhostSession` 返回 true 且 `_session.Reset()` 已完成，新会话已绑定新车牌）之后，通过 ReactiveUI `MessageBus` 发布一条领域事件，使称重侧能够失效对应缓存。

#### Scenario: 事件载荷包含被废弃车牌

- **WHEN** 幽灵会话重置成功
- **THEN** 领域事件 MUST 携带被废弃会话的车牌号（在调用 `Reset()` 之前从会话中读取的值）
- **AND THEN** 事件 MAY 携带新车牌、设备名、UTC 时间戳以支持日志与测试

### Requirement: 称重侧订阅并失效幽灵车牌缓存

`AttendedWeighingService`（或项目指定的单一订阅方）MUST 订阅上述领域事件，并在处理中使被废弃车牌不再参与 `GetMostFrequentPlateNumber()` 的结果，且 MUST 通过 `PlateNumberChangedMessage` 通知 UI。

#### Scenario: 移除废弃键后重算推荐

- **WHEN** 收到幽灵会话重置事件且采用「仅移除废弃车牌键」策略
- **THEN** 系统 MUST 从车牌缓存中移除与被废弃车牌对应的条目（键规则与正常 LPR 写入一致）
- **AND THEN** 系统 MUST 根据当前缓存重新计算推荐车牌并发送 `PlateNumberChangedMessage`（载荷为新的推荐值或 null）

#### Scenario: 整表清空策略

- **WHEN** 收到幽灵会话重置事件且采用「整表清空」策略
- **THEN** 系统 MUST 清空车牌缓存并发送 `PlateNumberChangedMessage(null)`，行为与 `ResetWeighingCycleAsync` 中的缓存清理一致
