# Delta: plate-number-locking

本变更增加「道闸幽灵废弃」导致的锁定失效规则，与既有 `LockedAt` 最早优先及周期重置行为兼容。主规格见 `openspec/specs/plate-number-locking/spec.md`。

## ADDED Requirements

### Requirement: 幽灵会话废弃时失效对应锁定缓存

当 `EnablePlateRewrite = false` 且系统收到道闸侧发出的幽灵会话重置领域事件时，系统 MUST 使被废弃会话车牌对应的缓存记录不再参与「LockedAt 优先选择」；实现上 MAY 移除该车牌键或清空整个车牌缓存（与项目选定策略一致）。

#### Scenario: 废弃车牌不再成为最早 LockedAt 候选

- **WHEN** 缓存中存在车牌 A 的 `LockedAt` 记录且道闸已裁定车牌 A 所属会话为幽灵并已废弃
- **AND WHEN** 称重侧已处理幽灵会话重置领域事件
- **THEN** 系统 MUST NOT 在后续 `GetMostFrequentPlateNumber()` 中仅因车牌 A 的剩余 `LockedAt` 而返回车牌 A（除非车牌 A 因新的有效 LPR 再次写入缓存）

#### Scenario: 与称重周期重置的独立性

- **WHEN** 幽灵会话重置事件发生且车辆从未完成「称重周期结束」类状态回环
- **THEN** 系统 MUST 仍执行上述锁定失效行为
- **AND THEN** 该行为 MUST 不依赖 `ResetWeighingCycleAsync` 或 `OffScale` 周期迁移作为唯一触发条件
