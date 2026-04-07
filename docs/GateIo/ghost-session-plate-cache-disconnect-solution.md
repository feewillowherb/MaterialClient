# 幽灵会话与车牌缓存（LockedAt）脱节：问题与解决方案

> **涉及代码**: `GateIoControlService.cs`、`AttendedWeighingService.cs`  
> **日期**: 2026-04-07  
> **文档类型**: 设计说明（解释 + 待实施建议）

## 1. 问题概述

当道闸侧判定**幽灵会话**（会话已激活、车辆从未上磅、又来新车牌）并执行 `_session.Reset()` 时，称重侧 `_plateNumberCache` 中**已写入的 `PlateNumberCacheRecord.LockedAt` 不会被清理。关闭「车牌重写」时，`GetMostFrequentPlateNumber()` 按**最早 `LockedAt`** 选牌，界面与业务仍可能显示**已被道闸逻辑废弃的旧车牌**。

这与「道闸会话已重置，但称重侧仍认为旧车牌被锁定」之间的**状态不一致**，本质是**两个子系统未联动**。

**关联文档**: 道闸会话长期占用的另一场景见 [session-stuck-analysis.md](./session-stuck-analysis.md)（侧重 `SessionActive` 无法清理）。本文侧重 **LockedAt / 车牌缓存** 与幽灵重置的脱节。

---

## 2. 现场日志佐证（2026-04-07）

以下片段按时间顺序摘自同一现场。道闸侧已用新车牌 **浙 A62J79** 重建会话并开闸，但紧随其后 `GetMostFrequentPlateNumber()` 仍按 **`LockedAt` 最早** 选中旧牌 **浙 A95F35**，与道闸会话已切换的事实矛盾。

```text
2026-04-07 08:20:21.993 +08:00 [WRN] 检测到幽灵会话(从未上磅)，新车牌触发重置: OldPlate=浙A95F35, OldEntrySide="A", OldDuration="00:43:05.4874198", NewPlate=浙A62J79, NewDevice=出场
2026-04-07 08:20:21.994 +08:00 [INF] 创建道闸会话: Device=出场, EntrySide="B", Plate=浙A62J79
2026-04-07 08:20:22.009 +08:00 [INF] 已发送 Vzvision I/O 自动复位输出: Device=出场, IoChannel=1, DurationMs=500
2026-04-07 08:20:22.009 +08:00 [INF] 收到 LPR 事件: 浙A62J79 来自 出场 (类型: "Vzvision")
2026-04-07 08:20:22.009 +08:00 [INF] 车牌号推荐匹配成功: 输入=浙A62J79, 推荐=浙A62J79, 差异数=0
2026-04-07 08:20:22.010 +08:00 [WRN] 车牌重写已关闭，使用 LockedAt 优先选择车牌: Plate=浙A95F35, LockedAt="2026-04-06T23:37:16.5155856Z", Color="Yellow"
```

**解读要点**:

1. **道闸与会话**: 幽灵重置后 `Plate` 已为浙 A62J79，设备为出场侧，逻辑与日志一致。
2. **称重与缓存**: 同一秒内 LPR 已处理浙 A62J79，但「车牌重写已关闭」分支仍选出浙 A95F35，因其 **`LockedAt` 仍早于** 新车牌本次写入缓存后的锁定时间（`LockedAt` 最早优先策略始终取旧牌）。
3. **结论**: 幽灵重置**未**使旧车牌在缓存中失效，与第 1 节、第 3 节所述脱节一致；修复需让幽灵废弃与 `_plateNumberCache` / `LockedAt` 联动（见第 5 节方案）。

---

## 3. 根因（为何 ResetWeighingCycleAsync 帮不上忙）

| 子系统 | 幽灵场景下的行为 |
|--------|------------------|
| `GateIoControlService` | `TryResetGhostSession` 调用 `_session.Reset()`，只清道闸会话字段。 |
| `AttendedWeighingService` | 车辆未上磅时称重状态**一直为** `OffScale`，不会发生「非 OffScale → OffScale」的迁移。 |
| 缓存清理入口 | `ClearPlateNumberCache()` 仅在 `ResetWeighingCycleAsync()` 中调用，而后者由称重状态回环触发。 |

因此：**幽灵重置不会触发 `ResetWeighingCycleAsync()`**，`_plateNumberCache`（含 `LockedAt`）**不会**被清空。

---

## 4. `AttendedWeighingService` 与 `GateIoControlService`：唯一真源评估

两服务当前**各自持有与车牌相关的状态**，未声明哪一侧是全局「当前业务车牌」的**唯一真源**（Single Source of Truth）。本节评估是否应合并真源，以及在不合并时的合理边界。

### 4.1 现状：两套并行事实

| 归属 | 状态载体 | 主要职责 | 与「当前车牌」的关系 |
|------|-----------|----------|----------------------|
| `GateIoControlService` | `GateIoSession`（`PlateNumber`、`SessionActive`、`EntrySide` 等） | 道闸 I/O、会话生命周期、幽灵会话判定与 `_session.Reset()` | **道闸会话当前绑定的车牌**：决定本轮开闸语境下的「会话车牌」 |
| `AttendedWeighingService` | `_plateNumberCache`、`LockedAt`、称重状态机 | LPR 聚合、推荐车牌、周期结束清空缓存 | **称重/UI 语境下的「推荐车牌」**：关闭重写时由 `LockedAt` 等规则导出 |

两者都消费 LPR（经 MessageBus），但**幽灵重置只更新前者**，后者无对应失效，因而出现用户可见层「道闸已是新车牌、推荐仍是旧车牌」的分裂。

### 4.2 「唯一真源」要统一的是什么

需要先区分**不同问题域**，避免把不相关的状态硬塞进一个对象：

1. **道闸授权与会话**（能否开闸、会话是否仍有效）：天然贴近 `GateIoControlService`，与设备侧、入口/出口侧强相关。
2. **称重业务与周期**（是否已上磅、是否已生成记录、周期结束是否清缓存）：天然贴近 `AttendedWeighingService`。
3. **用户可见的「当前车牌」**（界面、语音、未落库前的提示）：这是**横切关注点**，目前由 `GetMostFrequentPlateNumber()` 与道闸会话**分别**回答，缺一条规则说明「以谁为准、何时以谁为准」。

本次缺陷属于：在**幽灵废弃**这一业务事件上，(3) 应与 (1) 的决策**对齐**，或至少使 (2) 的缓存不再与 (1) 矛盾。

### 4.3 方案对比：合并真源 vs 双源 + 显式同步

| 策略 | 做法 | 优点 | 缺点与风险 |
|------|------|------|------------|
| **A. 物理唯一真源** | 将道闸会话与车牌缓存迁入同一服务或同一聚合根，由一处更新 | 状态天然一致，无「幽灵清了 A、缓存还记得 A」类问题 | 职责膨胀；`GateIoControlService` 与称重订阅/状态机交叉引用增多；单测与迭代成本上升 |
| **B. 逻辑唯一真源 + 分治存储**（推荐与第 5 节方案 A 一致） | 两服务仍各存所需副本，但通过 **领域事件**（如幽灵重置）声明「某会话车牌已废弃」，称重侧**失效或删除**对应缓存条目 | 边界清晰，改动面可控，符合现有 MessageBus 风格 | 需约定事件幂等、顺序与线程安全；仍存在「短暂不一致窗口」（毫秒级），通常可接受 |
| **C. UI 只读道闸 Plate** | 界面上「当前车」只绑定 `GateIoSession.PlateNumber`，缓存仅用于称重落库 | 展示层立刻与道闸一致 | 称重推荐、报表、未与道闸对齐的调用方仍可能读旧缓存；不治本 |

**评估结论**:

- **不必**为修复本问题而把整个车牌状态**物理合并**到单一服务；**应**在架构上明确：**幽灵废弃、周期结束**等对「推荐车牌」有语义的事件，必须由**单一协调路径**触发缓存失效（事件或将来提取的协调器），即 **逻辑上的唯一真源是「业务事件序列」**，而非某一静态字段。
- **`GateIoControlService` 适合作为「道闸会话车牌」的真源**；**`AttendedWeighingService` 适合作为「称重周期内 LPR 聚合与 LockedAt」的真源**。二者对「当前展示车牌」应在**事件层对齐**，而不是互相直接读对方私有字段（避免隐藏耦合）。

### 4.4 对后续设计的提示

- 若新增 `GhostGateSessionResetMessage`，其语义是：**道闸域已裁定旧会话车牌作废**；订阅方负责让**称重域的推荐结果**不再优于该作废决策（清条目或全清缓存）。
- 长期若引入 **方案 B（统一会话协调器）**，实质是把上述事件协调**收口**到一个模块，仍可与「双真源、显式同步」并存，只是集中发送与顺序保证更清晰。

---

## 5. 解决方案方向（可选）

以下方案按**耦合度从低到高**排列，实施时可择一或组合。

### 方案 A：MessageBus 事件（推荐）

**思路**: 在 `GateIoControlService` 确认执行幽灵会话重置（`_session.Reset()` 且即将为新车牌开闸）之后，发布一条**领域事件**，例如 `GhostGateSessionResetMessage`，载荷至少包含：

- 被废弃会话的车牌（重置前的 `_session.PlateNumber`，需在 `Reset()` 之前取出）
- 可选：设备名、时间戳，便于日志与测试

**订阅方**: `AttendedWeighingService`（或专门的小服务）在订阅中：

- **策略 A1（粗）**: 调用与 `ResetWeighingCycleAsync` 中相同的 `ClearPlateNumberCache()`，并发送 `PlateNumberChangedMessage(null)`（与现有清空行为一致）。
- **策略 A2（细）**: 仅从 `_plateNumberCache` 中 **Remove** 幽灵车牌对应键；若关闭重写且仍存在多锁定候选，再按现有 `LockedAt` 规则重算推荐车牌，并 `MessageBus` 推送 `PlateNumberChangedMessage`。

**取舍**:

- A1 实现快、行为与「周期结束清空」一致，但会丢掉**同一闸口尚未上磅、却已识别到的新车牌**的缓存（若幽灵重置后立即又有识别，通常仍会再次写入，需结合现场节奏评估）。
- A2 只移除废弃牌，对其它候选干扰小，实现与测试略复杂。

### 方案 A 与 `CreateWeighingRecordAsync` 的时序关系（落库车牌）

称重记录创建在 `AttendedWeighingService` 的 `CreateWeighingRecordAsync` 中，首行使用当前缓存推导车牌：

```csharp
var plateNumber = GetMostFrequentPlateNumber();
```

该路径**仅**在重量稳定流程中被调用：`OnWeightStabilizedAsync` 在抓拍等步骤之后 `await CreateWeighingRecordAsync(...)`，即状态已到达 `WeightStabilized`、车辆**已上磅**之后。

**与幽灵场景的关系**：

| 事实 | 说明 |
|------|------|
| 幽灵会话 | 判定条件包含「从未上磅」、称重状态长期为 `OffScale`，**不会**在同一轮幽灵 episode 里触发 `OnWeightStabilizedAsync` / `CreateWeighingRecordAsync`。 |
| 方案 A 生效时刻 | 幽灵重置当下发布事件，订阅方同步或异步清理 `_plateNumberCache`（去旧键或全清）。 |
| 落库时刻 | 真正建库发生在**后续**车辆上磅且重量稳定之后，与幽灵事件通常相隔**秒级至分钟级**，中间仍有多次 LPR 可刷新缓存。 |

**结论（可视为确定）**：在**典型业务流程**下，方案 A 对缓存的修正**会在** `CreateWeighingRecordAsync` 调用 `GetMostFrequentPlateNumber()` **之前早已发生**；落库时再用推荐车牌，**一般不再**受「幽灵车牌仍占 `LockedAt`」这一脱节影响。

**仍须区分的风险面**：同一 **`LicensePlateRecognizedMessage`** 内「称重订阅先于道闸」时，`OnPlateNumberRecognized` 里**那一次** `GetMostFrequentPlateNumber()` 仍可能短暂返回幽灵车牌（见前文订阅顺序）。该问题主要影响**即时 UI / 日志**，与**建记录**不在同一调用链、也不在相近时间点。

**边界**：若方案 A 采用 **A1 全清** 后、至上磅稳定前**几乎无新的 LPR**，`GetMostFrequentPlateNumber()` 可能为 `null` 或依赖后续规则，需由产品决定是否允许空牌落库或依赖其它补全策略（与幽灵脱节无关，属空缓存策略问题）。

### 方案 B：统一会话协调器（中长期）

将「道闸会话」与「当前推荐车牌 / 缓存策略」收敛到**单一组件**或明确的分层：**会话开始 / 幽灵废弃 / 周期结束** 三种出口统一调用同一套「缓存失效」API，避免两处各自维护。改动面较大，适合与道闸、称重其它重构一起做。

### 方案 C：仅在 UI 层屏蔽（不推荐作为唯一手段）

不清理缓存，仅根据 `GateIoControlService` 暴露的会话状态隐藏旧车牌。根因仍在，报表与后台若仍读 `GetMostFrequentPlateNumber()` 会不一致。

---

## 6. 推荐实施顺序

1. 定义并实现 **`GhostGateSessionResetMessage`**（或等价命名），在 `TryResetGhostSession` 成功路径、在调用 `_session.Reset()` **之前**保存旧车牌，重置后发布消息。
2. 在 `AttendedWeighingService` 订阅该消息：优先实现 **A1** 验证现场；若反馈「新车牌识别被误清」，再改为 **A2** 或「仅当缓存中只有幽灵单键时全清」等启发式规则。
3. 补充单元测试：模拟「先识别牌 A 写入 `LockedAt` → 幽灵重置为牌 B → 断言推荐车牌不为 A」；关闭与开启车牌重写各一组。

---

## 7. 代码锚点（实施时对照）

| 位置 | 说明 |
|------|------|
| `GateIoControlService.TryResetGhostSession` | 幽灵判定与 `_session.Reset()` 的唯一集中点，适合挂接消息发送。 |
| `AttendedWeighingService.ClearPlateNumberCache` | 已有整表清空与 `PlateNumberChangedMessage` 通知。 |
| `AttendedWeighingService.OnPlateNumberRecognized` | `LockedAt` 写入与 `AddOrUpdate` 逻辑。 |
| `AttendedWeighingService.GetMostFrequentPlateNumber` | `LockedAt` 最早优先的选择策略。 |
| `AttendedWeighingService.CreateWeighingRecordAsync` | 落库前调用 `GetMostFrequentPlateNumber()`；仅由 `OnWeightStabilizedAsync` 触发，与幽灵「未上磅」不在同一时间线。 |

---

## 8. 验收要点

- 关闭车牌重写时，幽灵会话被新车牌替换后，**推荐车牌不应再长期停留在旧车牌**。
- 正常完成一次称重周期（经 `ResetWeighingCycleAsync`）的行为与改造前一致。
- 无新增跨线程死锁：`GateIoControlService` 与 `AttendedWeighingService` 对共享状态的锁策略保持现有约定；若订阅中需动缓存，与 `_operationsLock` 或现有并发模型对齐。

---

## 9. 状态与实现跟踪

| 项目 | 说明 |
|------|------|
| 本文档 | 设计级背景；具体契约与任务见 OpenSpec 变更 |
| OpenSpec 变更 | [`openspec/changes/ghost-gate-session-plate-cache-sync/`](../../openspec/changes/ghost-gate-session-plate-cache-sync/)（`proposal.md`、`design.md`、`tasks.md`、delta 规格） |
| 实现 | 代码落地：`GhostGateSessionResetMessage`、`GateIoControlService` 发布事件，`AttendedWeighingService` 移除废弃键并 `PlateNumberChangedMessage` |
