## Context

当前 SolidWaste 模块中，物料 ID（MaterialId）和运单数量（WaybillQuantity）通过 ABP ExtraProperties 扩展机制存储，键名为 `SolidWasteInfo.MaterialId` 和 `SolidWasteInfo.WaybillQuantity`。但 Waybill 实体已拥有 `MaterialId`（`int?`）和 `OrderGoodsWeight`（`decimal?`）直接字段，WeighingRecord 通过 `Materials` JSON 列表（`List<WeighingRecordMaterial>`）存储物料信息。

扩展字段与实体字段的并存造成：
- 数据双重写入（`WeighingMatchingService` 同时设置 `waybill.MaterialId` 和 `PatchSolidWasteMaterialInfo`）
- 读取路径不一致（部分代码读实体字段，部分读 ExtraProperties）
- UI 层需要 fallback 逻辑（`SolidWasteWeighingDetailViewModel` 先读 ExtraProperties，再 fallback 到 `waybill.MaterialId`）

## Goals / Non-Goals

**Goals:**
- 所有对 Waybill 的 MaterialId 访问统一使用 `waybill.MaterialId` 实体字段
- 移除 `SolidWasteInfoExtensions` 中 MaterialIdKey / WaybillQuantityKey 及相关方法
- 更新 SolidWasteService、WeighingMatchingService、SolidWasteWeighingDetailViewModel 的读取路径
- 更新单元测试以匹配新行为

**Non-Goals:**
- 不修改 WeighingRecord 的 `Materials` JSON 存储机制
- 不修改 SolidWasteType / Street / OrderNumber / Shipper 等其他扩展字段（这些是 Waybill 实体不拥有的固废特有数据，保留扩展机制是合理的）
- 不涉及数据库迁移（字段已存在，无需 schema 变更）
- 不涉及 API 变更

## Decisions

### Decision 1: WeighingRecord 上的 MaterialIdKey 处理方式

**选择**：完全移除 WeighingRecord 上的 `SetSolidWasteMaterialInfo` / `PatchSolidWasteMaterialInfo` 扩展方法，不再通过 ExtraProperties 存储 MaterialId。

**替代方案**：
- A) 保留 WeighingRecord 扩展方法，仅移除 Waybill 的 → 会导致不一致的 API 表面
- B) 为 WeighingRecord 添加直接 MaterialId 字段 → 需要数据库迁移，超出范围

**理由**：WeighingRecord 通过 `Materials` 列表已存储物料信息，`SolidWasteInfo.MaterialId` 扩展键是冗余的。`WeighingMatchingService.UpdateSolidWasteInfoAsync` 中同时更新 `record.Materials` 和 `record.PatchSolidWasteMaterialInfo`，移除扩展写入不影响数据完整性。UI 层 `SolidWasteWeighingDetailViewModel` 需改为从 `record.Materials.FirstOrDefault()?.MaterialId` 读取。

### Decision 2: SolidWasteService 货名过滤改用实体字段

**选择**：`SolidWasteQueryWaybillsAsync` 中 `GoodsName` 过滤从 `w.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `w.MaterialId`。

**理由**：`w.MaterialId` 是直接 EF Core 映射字段，可以下推到 SQL 层过滤，无需在内存中遍历 ExtraProperties JSON。这同时改善了查询性能。

### Decision 3: SolidWasteMapToExportRow 中材料 ID 读取

**选择**：从 `waybill.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `waybill.MaterialId`。

**理由**：与 Decision 2 一致，统一使用实体字段。

### Decision 4: WeighingMatchingService 中 SolidWasteTransferProperties

**选择**：移除 `solidWasteMaterialId` 变量及其从 `primary.GetProperty<int?>("SolidWasteInfo.MaterialId")` 的读取和 `waybill.PatchSolidWasteMaterialInfo(solidWasteMaterialId, ...)` 写入。`waybill.MaterialId` 已在匹配流程的其他位置设置。

**理由**：`PatchSolidWasteMaterialInfo` 仅写入 ExtraProperties，移除后不影响 `waybill.MaterialId` 的正确性。

## Risks / Trade-offs

**[已有数据库中的 ExtraProperties 数据]** → 运行中的数据库中可能存在历史数据在 ExtraProperties 中存储了 `SolidWasteInfo.MaterialId`。由于 Waybill.MaterialId 字段在匹配流程中始终被正确设置，历史数据的实体字段和扩展字段应一致。新代码仅读取实体字段，不会丢失数据。

**[WeighingRecord UI 读取路径变更]** → `SolidWasteWeighingDetailViewModel` 从 ExtraProperties 改为 `record.Materials` 读取，需确保 `Materials` 列表在称重记录创建时已正确填充。从 `WeighingMatchingService` 代码看，`Materials` 在匹配完成时已被写入。
