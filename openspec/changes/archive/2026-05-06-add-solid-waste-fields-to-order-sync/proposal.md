## Why

当前订单同步接口（`SynchronizationOrderInputDto`）仅支持标准模式订单数据的同步，无法传递固废（WeighingMode=SolidWaste）订单特有的业务信息（固废类型、街道/位置、固废联单编号、发货单位）。本地 `Waybill` 实体已通过 `SolidWasteInfoExtensions` 支持固废信息的存储，但 `FromWaybill()` 转换方法未将固废字段映射到 DTO 中，导致固废订单同步时丢失关键业务数据。

## What Changes

- 在 `SynchronizationOrderInputDto` 中新增 `WeighingMode`（`int?`）和 `SolidWasteInfo`（嵌套 DTO）字段
- 新建 `SolidWasteInfoDto` 类，包含 `SolidWasteType`、`Street`、`SolidWasteOrderNumber`、`Shipper` 四个属性
- 修改 `SynchronizationOrderInputDto.FromWaybill()` 方法，当 `Waybill.WeighingMode == SolidWaste` 时自动填充 `WeighingMode` 和 `SolidWasteInfo`
- 新增客户端校验逻辑：固废模式下 `SolidWasteInfo` 不可为 null；`SolidWasteOrderNumber` 最大 100 字符

## Capabilities

### New Capabilities
- `solid-waste-order-sync`: 订单同步接口的固废字段扩展，包含 DTO 新增、FromWaybill 映射和数据校验

### Modified Capabilities
（无现有 capability 的需求级别变更）

## Impact

- **DTO 层**：`SynchronizationOrderInputDto` 新增 2 个属性；新建 `SolidWasteInfoDto` 类
- **API 调用**：`POST /api/Order/SynchronizationOrder` 和 `POST /api/Order/SynchronizationModifyOrder` 的请求体结构扩展（向后兼容，新增字段均为 nullable）
- **转换逻辑**：`SynchronizationOrderInputDto.FromWaybill()` 需要读取 Waybill 的 `WeighingMode` 和固废扩展属性
- **依赖**：`MaterialClient.Common.Entities.Enums.WeighingMode` 枚举、`SolidWasteInfoExtensions` 扩展方法（已存在，无需修改）
