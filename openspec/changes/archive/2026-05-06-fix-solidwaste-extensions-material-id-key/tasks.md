## 1. 移除 SolidWasteInfoExtensions 中的 MaterialIdKey / WaybillQuantityKey

- [x] 1.1 删除 `MaterialIdKey` 和 `WaybillQuantityKey` 常量定义
- [x] 1.2 删除 WeighingRecord 的 `SetSolidWasteMaterialInfo` 和 `PatchSolidWasteMaterialInfo` 扩展方法
- [x] 1.3 删除 Waybill 的 `SetSolidWasteMaterialInfo` 和 `PatchSolidWasteMaterialInfo` 扩展方法

## 2. 更新 SolidWasteService

- [x] 2.1 `SolidWasteQueryWaybillsAsync`：将 `w.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `w.MaterialId`
- [x] 2.2 `SolidWasteBuildMaterialDictAsync`：将 `w.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `w.MaterialId`
- [x] 2.3 `SolidWasteMapToExportRow`：将 `waybill.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `waybill.MaterialId`

## 3. 更新 WeighingMatchingService

- [x] 3.1 `UpdateSolidWasteInfoAsync` WeighingRecord 分支：移除 `record.PatchSolidWasteMaterialInfo(input.MaterialId, null)` 调用
- [x] 3.2 `UpdateSolidWasteInfoAsync` Waybill 分支：移除 `waybill.PatchSolidWasteMaterialInfo(input.MaterialId, waybill.OrderGoodsWeight)` 调用
- [x] 3.3 `CreateWeighingTicketDtoAsync`：将 `waybill.GetProperty<int?>("SolidWasteInfo.MaterialId")` 改为 `waybill.MaterialId`
- [x] 3.4 `SolidWasteTransferProperties`：移除 `solidWasteMaterialId` 变量及其从 ExtraProperties 的读取和 `waybill.PatchSolidWasteMaterialInfo` 调用

## 4. 更新 SolidWasteWeighingDetailViewModel

- [x] 4.1 Waybill 分支：移除从 ExtraProperties 读取 `SolidWasteInfo.MaterialId` / `SolidWasteInfo.WaybillQuantity` 的代码，直接使用 `waybill.MaterialId`
- [x] 4.2 WeighingRecord 分支：移除从 ExtraProperties 读取 `SolidWasteInfo.MaterialId` / `SolidWasteInfo.WaybillQuantity` 的代码，改为从 `record.Materials.FirstOrDefault()?.MaterialId` 读取

## 5. 更新单元测试

- [x] 5.1 更新 `WeighingMatchingServiceSolidWasteTransferTests`：移除 `SetProperty("SolidWasteInfo.MaterialId", ...)` 和对应断言
- [x] 5.2 更新 `SolidWasteExcelExportTests`：将测试数据中的 `SetProperty("SolidWasteInfo.MaterialId", ...)` 改为直接设置 `waybill.MaterialId`
