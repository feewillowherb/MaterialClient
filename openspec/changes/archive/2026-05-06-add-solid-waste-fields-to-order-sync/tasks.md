## 1. DTO 定义

- [x] 1.1 在 `SynchronizationOrderInputDto.cs` 中新建 `SolidWasteInfoDto` 类，包含 `SolidWasteType`（`string?`）、`Street`（`string?`）、`SolidWasteOrderNumber`（`string?`）、`Shipper`（`string?`）四个属性
- [x] 1.2 在 `SynchronizationOrderInputDto` 类中新增 `WeighingMode`（`int?`）属性
- [x] 1.3 在 `SynchronizationOrderInputDto` 类中新增 `SolidWasteInfo`（`SolidWasteInfoDto?`）属性

## 2. FromWaybill 转换逻辑

- [x] 2.1 在 `FromWaybill()` 方法中，当 `waybill.WeighingMode == WeighingMode.SolidWaste` 时设置 `WeighingMode = 1`
- [x] 2.2 在 `FromWaybill()` 方法中，当 `waybill.WeighingMode == SolidWaste` 时，通过 `SolidWasteInfoExtensions` 读取固废字段并构建 `SolidWasteInfoDto` 赋值给 `SolidWasteInfo`
- [x] 2.3 在构建 `SolidWasteInfoDto` 时，校验 `SolidWasteOrderNumber` 长度不超过 100 字符，超限时抛出 `ArgumentException`

## 3. 验证

- [x] 3.1 编译项目，确认无编译错误
- [x] 3.2 验证标准模式 Waybill 转换后 `WeighingMode` 为 null、`SolidWasteInfo` 为 null（向后兼容）
