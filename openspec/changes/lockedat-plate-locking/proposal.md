## Why

当前 `AttendedWeighingService` 在创建称重记录时使用 `GetMostFrequentPlateNumber()` 从缓存中挑选车牌。即使关闭了“车牌重写”（`WeighingConfiguration.EnablePlateRewrite=false`），在称重记录创建之前，缓存仍会随着后续识别结果变化，从而导致最终写入称重记录的车牌并非“第一次识别到的有效车牌”，与用户对“关闭重写即锁定车牌”的预期不一致。

需要提供一种在关闭重写时“锁定首个有效车牌（finalPlateNumber）”的行为，使称重记录在本次称重周期内车牌稳定、可预测，同时不引入复杂的并发协调要求。

## What Changes

- 当 `WeighingConfiguration.EnablePlateRewrite=false` 时，`AttendedWeighingService` 在收到首个有效 `finalPlateNumber` 后，记录该车牌的锁定时间 `LockedAt`，并在本称重周期内优先使用被锁定的车牌作为“当前推荐车牌”。
- `PlateNumberCacheRecord` 增加 `LockedAt`（替代 `isLocked` 概念），用于表达“该车牌在本称重周期第一次被锁定的时间”，且锁定时间在后续同车牌多次识别更新中保持不变。
- `GetMostFrequentPlateNumber()` 在存在被锁定车牌时，优先返回按 `LockedAt` 排序后的目标车牌（默认取最早锁定的车牌），不再受高/低优先级颜色、计数或“最新车牌”开关影响。
- 当 `WeighingConfiguration.EnablePlateRewrite=true` 时，系统保持现有行为（缓存按频次/最新时间选择；下磅时可重写最近称重记录车牌）。
- 称重周期重置（清空车牌缓存）后，锁定状态随缓存一起清除，下一次上磅重新开始锁定流程。

## Capabilities

### New Capabilities
- `plate-number-locking`: 定义在关闭车牌重写时，车牌识别结果如何被锁定（基于 `finalPlateNumber`）以及锁定后 `GetMostFrequentPlateNumber()` 的选择规则与周期重置行为。

### Modified Capabilities
- `attended-weighing`: 在有人值守称重流程中，“当前推荐车牌”的来源与稳定性要求发生变化：当关闭车牌重写时，推荐车牌必须在首个有效识别后锁定，不得在同一称重周期内跳变。

## Impact

- **影响代码路径**：
  - `MaterialClient.Common/Services/AttendedWeighingService.cs`
    - `OnPlateNumberRecognized(...)`：在 `EnablePlateRewrite=false` 时为 `finalPlateNumber` 写入 `LockedAt`
    - `GetMostFrequentPlateNumber()`：新增 locked 优先分支（按 `LockedAt` 排序选取）
    - `ClearPlateNumberCache()` / `ResetWeighingCycleAsync()`：确保锁定随缓存清空而重置
- **影响数据结构**：
  - `PlateNumberCacheRecord` 增加 `LockedAt` 字段（仅内存缓存，不涉及数据库迁移）
- **影响 UI 行为**：
  - `PlateNumberChangedMessage` 驱动的 UI 展示将随 `GetMostFrequentPlateNumber()` 的新规则，在关闭重写时更稳定（锁定后不跳变）。
- **兼容性与风险**：
  - 在关闭重写时，锁定优先级将覆盖现有的颜色优先级与“最新车牌”策略；这符合“锁定即稳定”的目标，但会改变部分边界场景下的推荐结果选择。
  - 并发场景允许多个车牌都被设置 `LockedAt`；`GetMostFrequentPlateNumber()` 将按时间排序选择目标车牌，从而保持可解释性（无需额外并发协调）。
