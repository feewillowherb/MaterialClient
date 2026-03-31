## 1. 领域模型：新增发货/收货单位领域方法

- [x] 1.1 在 `SolidWasteInfoExtensions` 中新增 `GetSolidWasteShippingAndReceivingUnits(this Waybill waybill, string providerName)` 静态扩展方法，返回 `(string ShippingUnit, string ReceivingUnit)` 元组，封装收料/发料模式下的发货/收货单位对调规则

## 2. 领域模型：方法命名规范化

- [x] 2.1 在 `SolidWasteInfoExtensions` 中将 WeighingRecord 扩展的 `SetStreet`/`GetStreet` 重命名为 `SetSolidWasteStreet`/`GetSolidWasteStreet`，`SetShipper`/`GetShipper` 重命名为 `SetSolidWasteShipper`/`GetSolidWasteShipper`
- [x] 2.2 在 `SolidWasteInfoExtensions` 中将 Waybill 扩展的 `SetStreet`/`GetStreet` 重命名为 `SetSolidWasteStreet`/`GetSolidWasteStreet`，`SetShipper`/`GetShipper` 重命名为 `SetSolidWasteShipper`/`GetSolidWasteShipper`
- [x] 2.3 更新 `ISolidWasteInfo` 接口，同步重命名 `SetStreet`→`SetSolidWasteStreet`、`GetStreet`→`GetSolidWasteStreet`、`SetShipper`→`SetSolidWasteShipper`、`GetShipper`→`GetSolidWasteShipper`

## 3. Service 层：修复导出错误 + 命名规范化

- [x] 3.1 在 `SolidWasteService.MapToExportRow` 中使用 `waybill.GetSolidWasteShippingAndReceivingUnits(providerName)` 替换原有的 `ShippingUnit = providerName` / `ReceivingUnit = waybill.GetShipper()` 逻辑，修复发料模式下发货/收货单位未对调的 Bug
- [x] 3.2 在 `SolidWasteService` 中将 `MapToExportRow` 重命名为 `SolidWasteMapToExportRow`，`QueryWaybillsAsync` 重命名为 `SolidWasteQueryWaybillsAsync`，`BuildProviderDictAsync` 重命名为 `SolidWasteBuildProviderDictAsync`，`BuildMaterialDictAsync` 重命名为 `SolidWasteBuildMaterialDictAsync`
- [x] 3.3 更新 `SolidWasteService.MapToExportRow` 中对 `waybill.GetStreet()` 的调用为 `waybill.GetSolidWasteStreet()`，`waybill.GetShipper()` 的调用为 `waybill.GetSolidWasteShipper()`（如果仍被引用）

## 4. Service 层：WeighingMatchingService 适配

- [x] 4.1 在 `WeighingMatchingService.CreateWeighingTicketDtoAsync` 中使用 `waybill.GetSolidWasteShippingAndReceivingUnits(providerName)` 替换内联的 if/else 对调逻辑（第 189-199 行）
- [x] 4.2 在 `WeighingMatchingService` 中将所有 `SetStreet`/`GetStreet` 调用更新为 `SetSolidWasteStreet`/`GetSolidWasteStreet`，`SetShipper`/`GetShipper` 调用更新为 `SetSolidWasteShipper`/`GetSolidWasteShipper`

## 5. UI 层：ViewModel 适配

- [x] 5.1 在 `AttendedWeighingDetailViewModel` 中将 `record.GetStreet()` 更新为 `record.GetSolidWasteStreet()`，`waybill.GetStreet()` 更新为 `waybill.GetSolidWasteStreet()`

## 6. 测试层：更新测试以匹配重命名

- [x] 6.1 更新 `SolidWasteExcelExportTests` 中对 `SolidWasteService.MapToExportRow` 的调用为 `SolidWasteService.SolidWasteMapToExportRow`，并更新使用 `SetStreet`/`GetStreet`/`SetShipper`/`GetShipper` 的地方
- [x] 6.2 更新 `WeighingMatchingServiceSolidWasteTransferTests` 中对 `GetShipper()`/`GetStreet()`/`SetStreet()` 的调用为 `GetSolidWasteShipper()`/`GetSolidWasteStreet()`/`SetSolidWasteStreet()`

## 7. 编译验证

- [x] 7.1 执行 `dotnet build` 确认零编译错误
