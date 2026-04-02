## Context

`AttendedWeighingService` 通过 `OnPlateNumberRecognized(...)` 持续更新 `_plateNumberCache`，并通过 `GetMostFrequentPlateNumber()` 在以下场景取用“当前推荐车牌”：

- 创建称重记录时：`CreateWeighingRecordAsync(...)` 使用 `GetMostFrequentPlateNumber()` 作为 `WeighingRecord.PlateNumber` 的初始值。
- UI 展示时：`OnPlateNumberRecognized(...)` 每次更新缓存后发布 `PlateNumberChangedMessage(mostFrequent)`，UI 订阅后显示 `MostFrequentPlateNumber`。
- 下磅周期重置时：`ResetWeighingCycleAsync()` 会清空缓存并重置周期标志。

当前即使关闭 `WeighingConfiguration.EnablePlateRewrite`（仅禁用下磅后的“重写称重记录车牌”），在称重记录创建之前，缓存仍会随识别结果变化，导致“最终写入称重记录的车牌”可能不是“第一次有效识别车牌（finalPlateNumber）”。该变更引入 `LockedAt`，在关闭重写时对车牌选择进行周期内锁定，使车牌稳定且可解释。

约束与现状：

- `OnPlateNumberRecognized(...)` 为同步方法，不适合在内部 `await GetConfigurationAsync()`；配置需以可同步读取的方式提供（例如在服务内缓存开关值，并在设置保存/刷新时更新）。
- `_plateNumberCache` 使用 `ConcurrentDictionary<string, PlateNumberCacheRecord>`，允许并发更新；本变更不引入“全局唯一锁定”并发协调要求，允许多个候选被锁定，但需提供确定的排序选择规则。

## Goals / Non-Goals

**Goals:**

- 当 `EnablePlateRewrite=false` 时：
  - 首次收到有效 `finalPlateNumber` 后，为其写入 `LockedAt`（仅首次写入，不随同车牌重复识别更新）。
  - `GetMostFrequentPlateNumber()` 在存在 `LockedAt` 候选时，优先按时间排序选择（默认：最早 `LockedAt`）。
  - 使称重记录创建时的 `PlateNumber` 在同一称重周期内稳定（不被后续识别“改写选择结果”影响）。
  - 使 UI 的 `MostFrequentPlateNumber` 在锁定后稳定（因为其来源也依赖 `GetMostFrequentPlateNumber()`）。

- 当 `EnablePlateRewrite=true` 时：
  - 保持现有选择逻辑（颜色优先级 + `_enableLatestPlateNumber` 的 Count/LastUpdateTime 选择策略）。
  - 保持下磅时 `TryReWritePlateNumberAsync()` 的重写行为不变。

**Non-Goals:**

- 不保证“严格意义上的第一个识别一定获胜”的并发一致性（不引入额外并发协调/互斥/原子选举）。
- 不改变数据库结构或持久化格式（`LockedAt` 仅存在于内存缓存中）。
- 不改变车牌推荐/过滤规则本身（仍以 `finalPlateNumber = RecommendPlateNumber(FilterHangingCharacter(raw))` 为准）。

## Decisions

### Decision 1: 用 `LockedAt: DateTime?` 表达锁定，而不是 `isLocked: bool`

**选择**：在 `PlateNumberCacheRecord` 中增加 `LockedAt`（可空时间戳），并以“是否为 null”判断是否锁定。

**理由**：

- 支持“允许多个 locked，但按时间排序”的需求：多个候选拥有各自 `LockedAt`，可稳定排序选取。
- 避免 `LastUpdateTime` 被频繁刷新导致“锁定先后顺序”语义漂移。
- 与现有 `ConcurrentDictionary` 更新模式兼容：在 `AddOrUpdate` 的更新委托中可保持 `LockedAt` 不变。

**替代方案**：

- `isLocked` + “返回任意 locked”：可行但缺乏可解释的选择规则；且并发下多个 locked 时“第一个”语义不成立。
- 单独的 `_lockedPlateNumberKey`：能提供更强一致性，但会引入额外状态与并发协调（与本次目标不符）。

### Decision 2: 锁定写入点在 `OnPlateNumberRecognized(...)`，锁定对象为 `finalPlateNumber`

**选择**：在 `OnPlateNumberRecognized(...)` 得到 `finalPlateNumber` 后、更新缓存时，如果 `EnablePlateRewrite=false`，为该 `finalPlateNumber` 写入 `LockedAt`（若此前未写入）。

**理由**：

- `finalPlateNumber` 已包含过滤与推荐逻辑，是对外“可用车牌”的真实候选。
- 状态机切换点（如 `OffScale -> WaitingForStability`）发生在识别结果到达之前，无法代表“首个有效识别”。
- 锁定发生在识别事件入口，语义清晰：首个有效识别到来就锁定。

### Decision 3: `GetMostFrequentPlateNumber()` 增加“LockedAt 优先”分支，默认选择最早锁定的候选

**选择**：在现有高/低优先级与 Count/LastUpdateTime 选择逻辑之前，先筛选所有 `LockedAt != null` 的候选；若存在：

- 取 `LockedAt` 最早的车牌作为返回值（默认规则）。

**理由**：

- 与用户期望“关闭重写后首个有效车牌不再变化”最一致。
- 允许多个候选被锁定时提供确定结果（按时间排序）。
- 一旦锁定生效，覆盖颜色优先级与 `_enableLatestPlateNumber` 策略，确保“稳定性”优先。

**可配置性**：本变更默认“最早 LockedAt”；若未来需要“最新 LockedAt”，可调整排序，但不在本次范围内。

### Decision 4: `EnablePlateRewrite` 在服务内以同步可读的方式缓存

**选择**：将 `EnablePlateRewrite` 的当前值缓存为服务成员字段（与 `_enableLatestPlateNumber` 类似），在配置加载与设置保存刷新时更新；`OnPlateNumberRecognized(...)` 只读取该字段。

**理由**：

- `OnPlateNumberRecognized(...)` 是同步路径，不应引入异步等待与潜在阻塞。
- 配置本身来自 `SettingsService`，与其它缓存字段更新方式一致。

## Risks / Trade-offs

- **[锁定覆盖现有优先级策略]**：当 `EnablePlateRewrite=false` 且出现 `LockedAt` 候选后，颜色优先级与“最新车牌”策略不再影响返回值  
  → **Mitigation**：此为目标行为（稳定性优先）；在 specs 中明确该优先级顺序。

- **[并发下多个候选被锁定]**：允许多个 `LockedAt`，最终按时间排序选择，可能与“严格首个识别”存在极端竞态差异  
  → **Mitigation**：设计上接受该差异；并在 specs 中定义锁定与选择的确定规则（按 `LockedAt` 最早）。

- **[时间源一致性]**：使用 `DateTime.UtcNow` 作为锁定时间，系统时间漂移可能影响排序  
  → **Mitigation**：沿用现有代码对时间的使用方式（`LastUpdateTime` 已用 `UtcNow`）；排序仅在短时间窗内，风险可接受。

- **[配置缓存滞后]**：设置切换 `EnablePlateRewrite` 与缓存刷新之间存在短暂窗口  
  → **Mitigation**：在设置保存事件中及时刷新配置缓存；并保持行为“以当前缓存值为准”。

