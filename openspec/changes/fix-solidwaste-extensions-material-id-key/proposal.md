## Why

SolidWasteService、WeighingMatchingService 和 SolidWasteWeighingDetailViewModel 中使用 ExtraProperties 扩展键（`SolidWasteInfo.MaterialId`、`SolidWasteInfo.WaybillQuantity`）来存取物料信息，但 Waybill 实体已拥有 `MaterialId` 和 `OrderGoodsWeight` 直接字段，WeighingRecord 通过 `Materials` JSON 列表存储物料信息。扩展字段的冗余使用导致数据双重存储、一致性风险和不必要的代码复杂度。

## What Changes

- **移除 MaterialIdKey / WaybillQuantityKey 扩展键**：删除 `SolidWasteInfoExtensions` 中的 `MaterialIdKey`、`WaybillQuantityKey` 常量及对应的 `SetSolidWasteMaterialInfo`、`PatchSolidWasteMaterialInfo` 方法（WeighingRecord 和 Waybill 两套）
- **SolidWasteService 改用实体字段**：`SolidWasteQueryWaybillsAsync` 货名过滤、`SolidWasteBuildMaterialDictAsync` 材料字典构建、`SolidWasteMapToExportRow` 导出行映射均从 `waybill.MaterialId` 读取
- **WeighingMatchingService 改用实体字段**：`SolidWasteTransferProperties` 和 `UpdateSolidWasteInfoAsync` 中对 Waybill 的 `PatchSolidWasteMaterialInfo` 调用替换为直接设置 `waybill.MaterialId`；对 WeighingRecord 的 `PatchSolidWasteMaterialInfo` 调用移除
- **SolidWasteWeighingDetailViewModel 改用实体字段**：Waybill 分支从 `waybill.MaterialId` 读取；WeighingRecord 分支从 `record.Materials` 首项获取 MaterialId
- **更新相关单元测试**

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

- `solidwaste-excel-export`: 货名映射从 ExtraProperties `SolidWasteInfo.MaterialId` 改为直接使用 `Waybill.MaterialId` 字段

## Impact

| 文件路径 | 变更类型 | 变更原因 | 影响范围 |
|---------|---------|---------|---------|
| `MaterialClient.Common/Entities/SolidWasteInfoExtensions.cs` | 删除代码 | 移除 MaterialIdKey/WaybillQuantityKey 及相关方法 | 固废扩展方法 |
| `MaterialClient.Common/Services/SolidWasteService.cs` | 修改 | ExtraProperties → 实体字段 | 固废导出查询/映射 |
| `MaterialClient.Common/Services/WeighingMatchingService.cs` | 修改 | ExtraProperties → 实体字段 | 固废称重匹配/更新 |
| `MaterialClient/ViewModels/SolidWasteWeighingDetailViewModel.cs` | 修改 | ExtraProperties → 实体字段 | 固废称重详情 UI |
| `MaterialClient.Common.Tests/Tests/WeighingMatchingServiceSolidWasteTransferTests.cs` | 修改 | 移除扩展字段断言 | 固废转移测试 |
| `MaterialClient.Common.Tests/Tests/SolidWasteExcelExportTests.cs` | 修改 | 测试数据使用实体字段 | 固废导出测试 |
