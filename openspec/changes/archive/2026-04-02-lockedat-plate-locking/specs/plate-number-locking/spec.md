## ADDED Requirements

### Requirement: 关闭车牌重写时锁定首个有效车牌
当 `WeighingConfiguration.EnablePlateRewrite = false` 时，系统 MUST 在本称重周期内锁定首个有效的 `finalPlateNumber`，并在后续车牌识别结果到达时保持“当前推荐车牌”稳定，不得在同一称重周期内跳变为其他车牌。

#### Scenario: 首次有效识别触发锁定
- **WHEN** 系统接收到一次车牌识别结果
- **AND WHEN** 该结果经过滤与推荐后得到非空的 `finalPlateNumber`
- **AND WHEN** `EnablePlateRewrite = false`
- **THEN** 系统 MUST 将该 `finalPlateNumber` 的缓存记录标记为锁定
- **AND THEN** 系统 MUST 记录锁定时间 `LockedAt`（UTC）

#### Scenario: 同一车牌重复识别不改变锁定时间
- **WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 同一个 `finalPlateNumber` 在本称重周期内再次被识别
- **THEN** 系统 MUST 更新该车牌缓存的计数与最近更新时间
- **AND THEN** 系统 MUST NOT 修改该车牌缓存记录中已存在的 `LockedAt`

#### Scenario: 并发或多源识别导致多个车牌均被锁定
- **WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 在极短时间窗口内收到多个不同的有效 `finalPlateNumber`（可能来自并发处理或多设备）
- **THEN** 系统 MAY 为多个车牌缓存记录写入 `LockedAt`
- **AND THEN** 系统 MUST 通过时间排序规则选择“当前推荐车牌”（见下述需求）

---

### Requirement: LockedAt 优先选择规则
当存在 `LockedAt != null` 的车牌缓存记录时，系统 MUST 优先从这些被锁定的候选中选择“当前推荐车牌”，且该选择 MUST 仅由 `LockedAt` 的排序决定（默认：选择最早 `LockedAt`）。

#### Scenario: 存在锁定候选时优先返回锁定车牌
- **WHEN** 系统计算当前推荐车牌
- **AND WHEN** 缓存中存在至少一个 `LockedAt != null` 的车牌
- **THEN** 系统 MUST 从所有 `LockedAt != null` 的车牌中选择 `LockedAt` 最早者并返回
- **AND THEN** 系统 MUST NOT 使用颜色优先级、识别次数或“最新车牌”策略覆盖该结果

#### Scenario: 不存在锁定候选时回退到原有选择逻辑
- **WHEN** 系统计算当前推荐车牌
- **AND WHEN** 缓存中不存在任何 `LockedAt != null` 的车牌
- **THEN** 系统 MUST 使用现有的选择逻辑（颜色优先级 + 计数/更新时间策略）确定推荐车牌

---

### Requirement: 称重周期重置清除锁定状态
当称重周期结束并重置时，系统 MUST 清空车牌缓存，从而清除所有 `LockedAt` 锁定状态；下一次称重周期 MUST 重新从首个有效 `finalPlateNumber` 开始锁定。

#### Scenario: 周期重置后锁定状态被清除
- **WHEN** 称重周期被重置（例如下磅流程完成并执行缓存清理）
- **THEN** 系统 MUST 清空车牌缓存
- **AND THEN** 系统 MUST 使后续推荐车牌计算不再受上一个周期的 `LockedAt` 影响
