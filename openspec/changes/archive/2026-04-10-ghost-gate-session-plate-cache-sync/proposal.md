# Proposal: Ghost gate session plate cache sync

## Why

道闸在判定幽灵会话并重置 `_session` 时，称重侧 `_plateNumberCache` 与 `LockedAt` 不会失效。关闭车牌重写时，`GetMostFrequentPlateNumber()` 仍按最早 `LockedAt` 选中**已被道闸废弃的旧车牌**，界面与推荐与道闸会话不一致。需在**幽灵废弃**这一业务事件上，让称重域缓存与道闸域决策对齐，而不依赖称重状态回环（`ResetWeighingCycleAsync`）。

设计说明与人因分析见：`docs/GateIo/ghost-session-plate-cache-disconnect-solution.md`。

## What Changes

- 新增领域消息（例如 `GhostGateSessionResetMessage`），在 `TryResetGhostSession` 成功并重置会话**之后**发布，载荷包含被废弃会话车牌（`Reset()` 前读出），可选新车牌、设备名、时间戳。
- `AttendedWeighingService`（或等价订阅方）订阅该消息：失效幽灵车牌对应的缓存条目（**A2**）或整表清空（**A1**），并发送 `PlateNumberChangedMessage`，使 UI 与 `GetMostFrequentPlateNumber()` 与道闸一致。
- 单元测试覆盖：关闭/开启车牌重写下，幽灵重置后推荐车牌不再长期停留在旧车牌；订阅顺序导致的瞬时差异在可接受范围内或有补发消息策略（见 design）。

## Capabilities

### New Capabilities

- `ghost-session-plate-cache-sync`: 道闸幽灵会话重置与称重车牌缓存（含 `LockedAt`）之间的 MessageBus 同步，以及 UI 通知（`PlateNumberChangedMessage`）约定。

### Modified Capabilities

- `gate-io-session-management`: 在现有「幽灵会话自动检测与重置」行为上，增加「重置成功后 MUST 发布幽灵重置领域事件」的要求（载荷与时机见 delta spec）。
- `plate-number-locking`: 增加「当收到幽灵会话废弃事件时，MUST 使被废弃车牌的锁定状态失效（移除键或整表清空），并更新推荐/UI」的 delta，与现有 `LockedAt` 最早优先规则兼容。

## Impact

- **代码**: `GateIoControlService`（发布消息）、`AttendedWeighingService`（订阅与缓存/UI）、`MaterialClient.Common/Events`（新消息类型）。
- **测试**: `AttendedWeighingServiceTests` 或同类集成/单元测试扩展。
- **行为**: 关闭车牌重写时，幽灵换车后推荐车牌与界面应跟随修正；**BREAKING** 不适用（对外 API 无破坏性变更，属行为修正）。
