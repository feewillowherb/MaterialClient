## MODIFIED Requirements

### Requirement: 导出列结构对齐模板
系统 SHALL 按照以下 18 列顺序输出 Excel 数据：流水号、车号、称重类型、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间。

#### Scenario: 表头行与模板完全一致
- **WHEN** 生成的 Excel 文件被打开
- **THEN** 第一行（表头）SHALL 包含以下 18 列标题，顺序为：流水号、车号、称重类型、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间

#### Scenario: 字段映射正确性
- **WHEN** 一条固废运单被导出
- **THEN** 各字段 SHALL 按以下规则映射：流水号=`Waybill.OrderNo`，车号=`Waybill.PlateNumber`，称重类型=`DeliveryType` 映射（Receiving→"收料"，Sending→"发料"），发货/收货单位=`waybill.GetSolidWasteShippingAndReceivingUnits(providerName)` 的结果，货名=`Material.Name`（通过 ExtraProperties SolidWasteInfo.MaterialId 关联），毛重=`OrderTotalWeight`，皮重=`OrderTruckWeight`，净重=`OrderGoodsWeight`，备注=`Remark`，毛重时间=`JoinTime`，皮重时间=`OutTime`，所属街道=`Waybill.GetSolidWasteStreet()`，类型=`Waybill.GetSolidWasteType()`，联单编号=`Waybill.GetSolidWasteOrderNumber()`

#### Scenario: 收料模式下发货/收货单位正确映射
- **WHEN** 运单 `DeliveryType` 为 `Receiving`，`providerName` 为 "长巷村"，`Shipper` 属性为 "东部资源化处置点"
- **THEN** 发货单位 SHALL 为 "长巷村"，收货单位 SHALL 为 "东部资源化处置点"

#### Scenario: 发料模式下发货/收货单位对调
- **WHEN** 运单 `DeliveryType` 为 `Sending`，`providerName` 为 "长巷村"，`Shipper` 属性为 "东部资源化处置点"
- **THEN** 发货单位 SHALL 为 "东部资源化处置点"，收货单位 SHALL 为 "长巷村"

#### Scenario: Shipper 未设置时使用默认值
- **WHEN** 运单的 Shipper 属性未设置（为 null），`DeliveryType` 为 `Receiving`，`providerName` 为 "长巷村"
- **THEN** 发货单位 SHALL 为 "长巷村"，收货单位 SHALL 为 "东部资源化处置点"（默认值）

#### Scenario: Shipper 未设置时发料模式对调仍使用默认值
- **WHEN** 运单的 Shipper 属性未设置（为 null），`DeliveryType` 为 `Sending`，`providerName` 为 "长巷村"
- **THEN** 发货单位 SHALL 为 "东部资源化处置点"（默认值），收货单位 SHALL 为 "长巷村"

#### Scenario: 关联实体不存在时安全处理
- **WHEN** 运单的 ProviderId 或 SolidWasteInfo.MaterialId 指向已删除或不存在的记录
- **THEN** 对应的发货单位或货名字段 SHALL 输出为空字符串，不抛出异常

### Requirement: 导出汇总行
系统 SHALL 在数据行之后追加一行汇总行，格式与 `sample.csv` 末尾行一致。

#### Scenario: 汇总行内容正确
- **WHEN** 导出包含 N 条运单数据
- **THEN** 汇总行的第 1 列 SHALL 为运单总数 N，第 7 列为所有毛重之和，第 8 列为所有皮重之和，第 9 列为所有净重之和，其余列为空

## ADDED Requirements

### Requirement: 固废发货/收货单位领域规则
系统 SHALL 在 `SolidWasteInfoExtensions` 中提供 `GetSolidWasteShippingAndReceivingUnits(this Waybill waybill, string providerName)` 静态扩展方法，封装发货/收货单位的确定规则。

#### Scenario: Waybill 收料模式
- **WHEN** `waybill.DeliveryType == DeliveryType.Receiving` 且 `providerName` 为 "长巷村"
- **THEN** 方法 SHALL 返回 `(ShippingUnit: "长巷村", ReceivingUnit: waybill.GetProperty<string>(ShipperKey) ?? DefaultShipper)`

#### Scenario: Waybill 发料模式
- **WHEN** `waybill.DeliveryType == DeliveryType.Sending` 且 `providerName` 为 "长巷村"
- **THEN** 方法 SHALL 返回 `(ShippingUnit: waybill.GetProperty<string>(ShipperKey) ?? DefaultShipper, ReceivingUnit: "长巷村")`

### Requirement: SolidWaste 扩展方法命名规范化
`SolidWasteInfoExtensions` 中所有方法 SHALL 使用 `SolidWaste` 前缀以确保模块边界清晰。`ISolidWasteInfo` 接口 SHALL 同步更新方法签名。

#### Scenario: Street 相关方法重命名
- **WHEN** 代码调用 `SetStreet` 或 `GetStreet`
- **THEN** 方法名 SHALL 分别为 `SetSolidWasteStreet` 和 `GetSolidWasteStreet`，行为不变

#### Scenario: Shipper 相关方法重命名
- **WHEN** 代码调用 `SetShipper` 或 `GetShipper`
- **THEN** 方法名 SHALL 分别为 `SetSolidWasteShipper` 和 `GetSolidWasteShipper`，行为不变

#### Scenario: SolidWasteService 私有方法命名规范化
- **WHEN** `SolidWasteService` 内部定义私有方法
- **THEN** 以下方法 SHALL 添加 `SolidWaste` 前缀：`QueryWaybillsAsync` → `SolidWasteQueryWaybillsAsync`，`BuildProviderDictAsync` → `SolidWasteBuildProviderDictAsync`，`BuildMaterialDictAsync` → `SolidWasteBuildMaterialDictAsync`，`MapToExportRow` → `SolidWasteMapToExportRow`

### Requirement: WeighingTicketDto 发货/收货单位使用领域规则
`WeighingMatchingService.CreateWeighingTicketDtoAsync` SHALL 调用 `GetSolidWasteShippingAndReceivingUnits` 领域方法来确定发货/收货单位，而非在 Service 层内联判断。

#### Scenario: 称重票据 DTO 使用领域方法
- **WHEN** `CreateWeighingTicketDtoAsync` 被调用并传入固废模式的 Waybill
- **THEN** 发货/收货单位 SHALL 通过 `waybill.GetSolidWasteShippingAndReceivingUnits(providerName)` 获取，而非 Service 层内联 if/else 判断
