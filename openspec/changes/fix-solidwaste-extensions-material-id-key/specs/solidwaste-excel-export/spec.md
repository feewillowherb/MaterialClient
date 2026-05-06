## MODIFIED Requirements

### Requirement: 导出列结构对齐模板
系统 SHALL 按照以下 18 列顺序输出 Excel 数据：流水号、车号、称重类型、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间。

#### Scenario: 表头行与模板完全一致
- **WHEN** 生成的 Excel 文件被打开
- **THEN** 第一行（表头）SHALL 包含以下 18 列标题，顺序为：流水号、车号、称重类型、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间

#### Scenario: 字段映射正确性
- **WHEN** 一条固废运单被导出
- **THEN** 各字段 SHALL 按以下规则映射：流水号=`Waybill.OrderNo`，车号=`Waybill.PlateNumber`，称重类型=`DeliveryType` 映射（Receiving→"收料"，Sending→"发料"），发货/收货单位=`waybill.GetSolidWasteShippingAndReceivingUnits(providerName)` 的结果，货名=`Material.Name`（通过 `Waybill.MaterialId` 直接字段关联），毛重=`OrderTotalWeight`，皮重=`OrderTruckWeight`，净重=`OrderGoodsWeight`，备注=`Remark`，毛重时间=`JoinTime`，皮重时间=`OutTime`，所属街道=`Waybill.GetSolidWasteStreet()`，类型=`Waybill.GetSolidWasteType()`，联单编号=`Waybill.GetSolidWasteOrderNumber()`

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
- **WHEN** 运单的 ProviderId 或 `Waybill.MaterialId` 指向已删除或不存在的记录
- **THEN** 对应的发货单位或货名字段 SHALL 输出为空字符串，不抛出异常

### Requirement: 货名过滤使用实体字段
系统 SHALL 在 `SolidWasteService.SolidWasteQueryWaybillsAsync` 中使用 `Waybill.MaterialId` 直接字段进行货名过滤，而非 ExtraProperties 扩展键。

#### Scenario: 通过货名过滤运单
- **WHEN** `SolidWasteExportFilter.GoodsName` 为 "装修"
- **THEN** 系统 SHALL 查询 `Material.Name` 包含 "装修" 的 Material 记录，返回 `Waybill.MaterialId` 匹配的运单

#### Scenario: MaterialId 为 null 的运单不参与货名过滤匹配
- **WHEN** 运单的 `MaterialId` 为 null 且 `GoodsName` 过滤条件不为空
- **THEN** 该运单 SHALL 被排除在过滤结果之外

### Requirement: 材料字典构建使用实体字段
系统 SHALL 在 `SolidWasteService.SolidWasteBuildMaterialDictAsync` 中使用 `Waybill.MaterialId` 直接字段收集材料 ID，而非 ExtraProperties 扩展键。

#### Scenario: 从运单收集材料 ID 构建字典
- **WHEN** 运单列表包含 `MaterialId` 为 10、20、10 的三条运单
- **THEN** 系统 SHALL 收集去重后的 `{10, 20}` 作为查询条件，返回 `{10: "装修垃圾", 20: "绿化垃圾"}` 的字典

#### Scenario: 所有运单 MaterialId 为 null 时返回空字典
- **WHEN** 所有运单的 `MaterialId` 均为 null
- **THEN** 系统 SHALL 返回空字典，不执行数据库查询
