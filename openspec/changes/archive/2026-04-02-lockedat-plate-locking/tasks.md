## 1. Data model & configuration plumbing

- [x] 1.1 为 `PlateNumberCacheRecord` 增加 `LockedAt`（`DateTime?`）字段，并确认其在缓存更新中可保持“只写一次”的语义
- [x] 1.2 在 `AttendedWeighingService` 内增加同步可读的 `EnablePlateRewrite` 缓存字段（与 `_enableLatestPlateNumber` 类似），并在配置加载/刷新路径中更新该字段

## 2. Locking behavior in plate recognition pipeline

- [x] 2.1 在 `OnPlateNumberRecognized(...)` 中以 `finalPlateNumber` 为 key 更新缓存时：当 `EnablePlateRewrite=false` 且 `LockedAt` 为空时写入 `LockedAt = DateTime.UtcNow`
- [x] 2.2 确保同一 `finalPlateNumber` 的重复识别不会覆盖既有 `LockedAt`（仅更新 `Count`、`LastUpdateTime`、`ColorType`）
- [x] 2.3 验证并发/多设备识别下允许多个不同 key 获得 `LockedAt`，且不会引发异常或阻塞（无需额外并发协调）

## 3. Selection logic: LockedAt-first

- [x] 3.1 在 `GetMostFrequentPlateNumber()` 最前面增加 locked 优先分支：若存在 `LockedAt != null` 的候选，返回 `LockedAt` 最早的车牌
- [x] 3.2 确保 locked 分支命中时不再执行颜色优先级/Count/“最新车牌”策略（以满足稳定性要求）
- [x] 3.3 当不存在 locked 候选时，保持原有选择逻辑完全不变（含高/低优先级颜色窗口与 `_enableLatestPlateNumber` 行为）

## 4. Cycle reset & integration points

- [x] 4.1 确认 `ResetWeighingCycleAsync()`/`ClearPlateNumberCache()` 清空缓存后，`LockedAt` 状态随周期被清除，下一周期重新锁定
- [x] 4.2 验证 `CreateWeighingRecordAsync(...)` 在 `EnablePlateRewrite=false` 且存在 locked 候选时，写入称重记录的车牌为 `LockedAt` 最早者（通过 `GetMostFrequentPlateNumber()` 路径实现）

## 5. Tests & observability

- [x] 5.1 增加/更新单元测试覆盖 `EnablePlateRewrite=false` 时的锁定行为：首个有效 `finalPlateNumber` 锁定、重复识别不改 `LockedAt`
- [x] 5.2 增加/更新单元测试覆盖“多个 LockedAt 候选”时的选择规则：返回 `LockedAt` 最早者
- [x] 5.3 增加/更新单元测试覆盖回退逻辑：无 locked 候选时仍按原颜色优先级与 `_enableLatestPlateNumber` 规则选择
