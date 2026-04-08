# Delta: gate-io-session-management

本变更在既有「幽灵会话自动检测与重置」行为上增加**领域事件发布**要求。主规格见 `openspec/specs/gate-io-session-management/spec.md`。

## ADDED Requirements

### Requirement: 幽灵会话重置后发布领域事件

系统 MUST 在成功完成幽灵会话重置（满足既有「新车牌触发幽灵会话重置」场景，且 `Reset()` 已执行、新会话已创建）之后，立即发布一条用于称重侧同步的领域事件（见 `ghost-session-plate-cache-sync` 能力），事件 MUST 包含被废弃会话的车牌号。

#### Scenario: 重置成功后发布事件

- **WHEN** 「新车牌触发幽灵会话重置」场景已执行完毕且系统将开闸或继续处理当前 LRP
- **THEN** 系统 MUST 发布领域事件，且载荷 MUST 包含废弃前的会话车牌
- **AND THEN** 发布 MUST 发生在入口道闸异步开闸调用开始之前（同一线程内、在首次 `await` 开闸逻辑之前完成发送），以便订阅方尽快处理
