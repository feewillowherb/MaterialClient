## Purpose

定义订单同步（`SynchronizationOrderInputDto`）中固废（固体废物）相关字段的扩展，包括称重模式标识和固废信息嵌套对象，以支持固废订单向下游系统的数据同步。

## Requirements

### Requirement: SynchronizationOrderInputDto 包含称重模式字段
`SynchronizationOrderInputDto` SHALL 包含 `WeighingMode` 属性（类型 `int?`），用于标识订单的称重模式。`null` 或 `0` 表示标准模式（向后兼容），`1` 表示固废模式。

#### Scenario: 标准 Waybill 转换后 WeighingMode 为 null
- **WHEN** 一个 `WeighingMode.Standard` 的 Waybill 通过 `FromWaybill()` 转换为 `SynchronizationOrderInputDto`
- **THEN** DTO 的 `WeighingMode` 值为 `null`，JSON 序列化后请求体不包含该字段或其值为 null

#### Scenario: 固废 Waybill 转换后 WeighingMode 为 1
- **WHEN** 一个 `WeighingMode.SolidWaste` 的 Waybill 通过 `FromWaybill()` 转换为 `SynchronizationOrderInputDto`
- **THEN** DTO 的 `WeighingMode` 值为 `1`

### Requirement: SynchronizationOrderInputDto 包含固废信息嵌套对象
`SynchronizationOrderInputDto` SHALL 包含 `SolidWasteInfo` 属性（类型 `SolidWasteInfoDto?`），用于承载固废订单特有的业务信息。仅在 `WeighingMode=1` 时需要填充该字段。

#### Scenario: 标准 Waybill 转换后 SolidWasteInfo 为 null
- **WHEN** 一个 `WeighingMode.Standard` 的 Waybill 通过 `FromWaybill()` 转换
- **THEN** DTO 的 `SolidWasteInfo` 值为 `null`

#### Scenario: 固废 Waybill 转换后 SolidWasteInfo 包含完整数据
- **WHEN** 一个 `WeighingMode.SolidWaste` 的 Waybill 且已设置固废信息（`SolidWasteType="HW01"`、`Street="XX街道"`、`SolidWasteOrderNumber="LD-2026-001"`、`Shipper="东部资源化处置点"`）通过 `FromWaybill()` 转换
- **THEN** DTO 的 `SolidWasteInfo` 不为 null，且 `SolidWasteType`、`Street`、`SolidWasteOrderNumber`、`Shipper` 值与 Waybill 扩展属性一致

### Requirement: SolidWasteInfoDto 定义固废信息数据结构
`SolidWasteInfoDto` SHALL 包含以下四个属性：`SolidWasteType`（`string?`）、`Street`（`string?`）、`SolidWasteOrderNumber`（`string?`）、`Shipper`（`string?`）。所有属性均可为 null。

#### Scenario: SolidWasteInfoDto 的 JSON 序列化结构
- **WHEN** 一个 `SolidWasteInfoDto` 实例被 JSON 序列化
- **THEN** 输出包含 `SolidWasteType`、`Street`、`SolidWasteOrderNumber`、`Shipper` 四个字段

### Requirement: FromWaybill 从 Waybill 扩展属性映射固废信息
`SynchronizationOrderInputDto.FromWaybill()` SHALL 在转换时读取 `Waybill` 的 `WeighingMode` 枚举值。当 `WeighingMode == SolidWaste` 时，从 `Waybill` 的扩展属性中读取固废信息（通过 `SolidWasteInfoExtensions` 方法）并填充 `SolidWasteInfoDto`。

#### Scenario: 固废 Waybill 的固废信息通过扩展方法读取
- **WHEN** `FromWaybill()` 处理一个 `WeighingMode.SolidWaste` 的 Waybill
- **THEN** 方法调用 `waybill.GetSolidWasteType()`、`waybill.GetSolidWasteStreet()`、`waybill.GetSolidWasteOrderNumber()`、`waybill.GetSolidWasteShipper()` 获取固废字段值

#### Scenario: 固废 Waybill 未设置部分固废字段
- **WHEN** `FromWaybill()` 处理一个 `WeighingMode.SolidWaste` 的 Waybill，但 `SolidWasteType` 未设置（返回 null）
- **THEN** `SolidWasteInfoDto.SolidWasteType` 为 `null`，转换不抛出异常

### Requirement: 固废联单编号长度校验
`SynchronizationOrderInputDto.FromWaybill()` SHALL 在构建 `SolidWasteInfoDto` 时校验 `SolidWasteOrderNumber` 长度不超过 100 字符。当超过限制时，SHALL 抛出 `ArgumentException`。

#### Scenario: 联单编号长度在限制内
- **WHEN** `FromWaybill()` 处理一个固废 Waybill，`SolidWasteOrderNumber` 为 50 字符的字符串
- **THEN** 转换成功完成，`SolidWasteInfoDto.SolidWasteOrderNumber` 值正确

#### Scenario: 联单编号超过 100 字符
- **WHEN** `FromWaybill()` 处理一个固废 Waybill，`SolidWasteOrderNumber` 为 101 字符的字符串
- **THEN** 抛出 `ArgumentException`，异常消息包含长度限制信息

#### Scenario: 联单编号为 null
- **WHEN** `FromWaybill()` 处理一个固废 Waybill，`SolidWasteOrderNumber` 为 null
- **THEN** 转换成功完成，`SolidWasteInfoDto.SolidWasteOrderNumber` 为 `null`
