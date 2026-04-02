## ADDED Requirements

### Requirement: 关闭车牌重写时称重记录创建使用锁定车牌
当 `WeighingConfiguration.EnablePlateRewrite = false` 时，系统在创建称重记录时 MUST 使用“当前推荐车牌”的锁定结果（基于 `finalPlateNumber` 的 `LockedAt` 规则）作为 `WeighingRecord.PlateNumber` 的初始值，从而保证同一称重周期内称重记录车牌稳定。

#### Scenario: 创建称重记录时使用锁定车牌
- **WHEN** 系统进入称重记录创建流程
- **AND WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 当前存在可用的锁定车牌候选（`LockedAt != null`）
- **THEN** 系统 MUST 将 `LockedAt` 最早的车牌作为称重记录的 `PlateNumber`

#### Scenario: 无锁定候选时使用原有推荐规则
- **WHEN** 系统进入称重记录创建流程
- **AND WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 当前不存在任何锁定车牌候选
- **THEN** 系统 MUST 退回到原有的推荐车牌选择逻辑来决定 `PlateNumber`
