# Design: Ghost gate session plate cache sync

## Context

- `GateIoControlService` 在 `TryResetGhostSession` 中重置 `GateIoSession`，不触及 `AttendedWeighingService._plateNumberCache`。
- `AttendedWeighingService` 在关闭车牌重写时依 `LockedAt` 最早优先返回推荐车牌；`ClearPlateNumberCache` 仅在 `ResetWeighingCycleAsync` 中调用，幽灵场景称重长期 `OffScale`，不会触发。
- UI 通过 `PlateNumberChangedMessage` 更新 `AttendedWeighingViewModel.MostFrequentPlateNumber`（见 `AttendedWeighingViewModel.StartPlateNumberChangedMessageBusSubscription`）。
- 人因文档：`docs/GateIo/ghost-session-plate-cache-disconnect-solution.md`。

## Goals / Non-Goals

**Goals:**

- 幽灵重置成功后，称重侧使被废弃车牌不再作为 `LockedAt` 候选（移除键或整表清空），并推送 `PlateNumberChangedMessage`，使推荐与 UI 与道闸会话新车牌一致或可接受（如 A1 短暂为空）。
- 事件载荷包含废弃前会话车牌，便于 A2 精确删除。
- 与现有 MessageBus、ABP 单例生命周期兼容；注意 `_operationsLock` 与缓存并发。

**Non-Goals:**

- 不合并 `GateIoControlService` 与 `AttendedWeighingService` 为单一物理真源（见文档 §4）。
- 不在本变更中实现「统一会话协调器」长期方案。
- 不修改道闸幽灵判定条件本身（与 `2026-03-27-ghost-session-reset-on-new-plate` 行为保持一致，仅增加称重侧联动）。

## Decisions

1. **消息命名与载荷**  
   - 采用 `GhostGateSessionResetMessage`（或项目命名约定下的等价类型）。  
   - **必须**：`AbandonedPlateNumber`（`Reset()` 前从 `_session.PlateNumber` 读取）。  
   - **可选**：`NewPlateNumber`、`DeviceName`、`OccurredAtUtc`，便于日志、测试与 A1 后补种缓存（若产品需要）。

2. **策略优先级：先 A2，可回退 A1**  
   - **A2**：`_plateNumberCache` 移除 `AbandonedPlateNumber` 键（忽略大小写与键规范化需与缓存键一致），再 `GetMostFrequentPlateNumber()`，发送 `PlateNumberChangedMessage(推荐)`。避免误清新车牌条目。  
   - **A1**：调用现有 `ClearPlateNumberCache()`（已含 `PlateNumberChangedMessage(null)`）。实现更快；若现场反馈新车牌被误清，再切换或加「新车牌补种」。

3. **发布时机**  
   - 在 `_session.Reset()` **完成**且新会话字段已写入**之后**、**第一次 `await OpenGateAsync` 之前**同步 `SendMessage`，使同线程内后续订阅者优先于异步开闸；称重侧订阅内应尽快更新缓存。  
   - 不与 `LicensePlateRecognizedMessage` 顺序强绑定；瞬时 UI 仍可能先错后纠，接受或通过二次 `PlateNumberChangedMessage` 收敛。

4. **订阅位置**  
   - `AttendedWeighingService.StartAsync` 中与现有 `LicensePlateRecognizedMessage` 订阅并列注册，`StopAsync` 释放。  
   - 避免 `GateIoControlService` 直接引用 `AttendedWeighingService` 类型，保持解耦。

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| 同一 LPR 消息内称重先于道闸，先打出错误「优先选择车牌」日志 | 事件到达后再发 `PlateNumberChangedMessage`；文档已说明与落库无关 |
| A1 全清后短暂无车牌 | 依赖后续 LPR；可选载荷中带 `NewPlateNumber` 补种 |
| 键格式与 `AbandonedPlateNumber` 不一致导致 Remove 失败 | 使用与 `_plateNumberCache` 相同的规范化键（与 `OnPlateNumberRecognized` 一致） |
| 死锁：消息处理与 `_operationsLock` | 在订阅回调中进入缓存前采用与 `OnPlateNumberRecognized` 相同锁策略；避免嵌套持锁调用道闸 |

## Migration Plan

- 无数据库迁移。部署后行为变更：幽灵换车后 UI/推荐不再长期滞留旧车牌。  
- 回滚：还原提交并移除订阅；恢复为仅道闸重置、称重缓存不联动（已知缺陷状态）。

## Open Questions

- 是否必须在首版实现 `NewPlateNumber` 补种以消除 A1 空窗（产品确认）。  
- 是否在集成测试中固定 `MessageBus` 订阅顺序（通常不必）。
