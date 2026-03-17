## MODIFIED Requirements

### Requirement: 实现归属
固废 Excel 导出 SHALL 由 **ExcelExportService** 实现（实现 **ISolidWasteExcelExportService**）；**不**存在独立的 SolidWasteExcelExportService 类。ExcelExportService 依赖 **ISolidWasteService** 取数，内部实现 17 列表头、行→列值映射、汇总行与写文件，并实现 `Volo.Abp.DependencyInjection.ITransientDependency`，由 ABP 隐式注册。

### Requirement: 固废运单 Excel 导出接口
系统 SHALL 提供 `ISolidWasteExcelExportService` 接口，支持将固废模式（`WeighingMode.SolidWaste`）的已完成运单导出为 `.xlsx` 格式的 Excel 文件。接口接受 `SolidWasteExportFilter` 过滤条件对象和输出文件路径，所有过滤参数均为可空。**实现上** SHALL 由 **ExcelExportService** 实现该接口：以 **SolidWasteService** 为唯一数据源获取导出行数据，内部完成固废 17 列、汇总行与 .xlsx 写入，不内含 Waybill 查询与行映射逻辑（由 SolidWasteService 提供）。

#### Scenario: 使用完整过滤条件导出
- **WHEN** 调用 `ExportAsync(filter, outputPath)` 并传入包含日期范围、车牌号、货名、发货单位的过滤条件
- **THEN** 系统通过 SolidWasteService 查询满足条件的 `SolidWasteExportRow` 列表，由 ExcelExportService 将数据写入指定路径的 .xlsx 文件，并返回包含导出行数和成功状态的 `ExportResult`

#### Scenario: 所有过滤参数为 null 时导出全部数据
- **WHEN** 调用 `ExportAsync` 且 `SolidWasteExportFilter` 的所有属性均为 null
- **THEN** 系统通过 SolidWasteService 获取全部满足 `WeighingMode == SolidWaste` 且 `OrderType == Completed` 的运单行数据，并导出为 .xlsx

#### Scenario: 仅传入部分过滤条件
- **WHEN** 调用 `ExportAsync` 且仅设置了 `StartDate` 和 `EndDate`，其余参数为 null
- **THEN** 系统通过 SolidWasteService 仅按 `AddDate` 日期范围过滤并返回行数据，再写入 Excel

#### Scenario: 过滤结果为空时导出空文件
- **WHEN** 调用 `ExportAsync` 但 SolidWasteService 返回空列表
- **THEN** 系统生成仅包含表头行的 .xlsx 文件，`ExportResult.RowCount` 为 0

### Requirement: 可空过滤参数定义
系统 SHALL 继续提供 `SolidWasteExportFilter` 过滤条件类，包含以下全部可空参数：`DateTime? StartDate`（AddDate 起始）、`DateTime? EndDate`（AddDate 截止）、`string? PlateNumber`（车牌号）、`string? GoodsName`（货名）、`string? ProviderName`（发货单位）。过滤语义由 **SolidWasteService** 实现，与本文档中导出列结构、汇总行等要求一致。

#### Scenario: 车牌号模糊匹配
- **WHEN** 过滤条件中 `PlateNumber` 为 "浙A96"
- **THEN** 系统 SHALL 返回所有 `Waybill.PlateNumber` 包含 "浙A96" 的运单（由 SolidWasteService 查询并映射为导出行）

#### Scenario: 货名模糊匹配
- **WHEN** 过滤条件中 `GoodsName` 为 "装修"
- **THEN** 系统 SHALL 返回所有关联 Material.Name 包含 "装修" 的运单（由 SolidWasteService 实现）

#### Scenario: 发货单位模糊匹配
- **WHEN** 过滤条件中 `ProviderName` 为 "长巷"
- **THEN** 系统 SHALL 返回所有关联 Provider.ProviderName 包含 "长巷" 的运单（由 SolidWasteService 实现）

### Requirement: 导出列结构对齐模板
系统 SHALL 按照以下 17 列顺序输出 Excel 数据，与既有模板一致：流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间。列数据 SHALL 来自 **SolidWasteService** 返回的 `SolidWasteExportRow`，由 **ExcelExportService** 内部完成列定义与写入。

#### Scenario: 表头行与模板完全一致
- **WHEN** 生成的 Excel 文件被打开
- **THEN** 第一行（表头）SHALL 包含上述 17 列标题及顺序

#### Scenario: 字段映射正确性
- **WHEN** 一条固废运单被导出
- **THEN** 各字段 SHALL 与 `SolidWasteExportRow` 属性对应一致（流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间），映射逻辑由 SolidWasteService 保证，ExcelExportService 负责写出

#### Scenario: 上传相关列
- **WHEN** 任意运单被导出
- **THEN** 上传结果、上传状态、上传时间列 SHALL 按 SolidWasteExportRow 中值输出（由 SolidWasteService 提供）

#### Scenario: 关联实体不存在时安全处理
- **WHEN** 运单的 ProviderId 或 SolidWasteInfo.MaterialId 指向已删除或不存在的记录
- **THEN** SolidWasteService 返回的对应发货单位或货名字段 SHALL 为空字符串，ExcelExportService 写出后不抛出异常

### Requirement: 导出汇总行
系统 SHALL 在数据行之后追加一行汇总行，格式与既有规范一致。汇总行由 **ExcelExportService** 在写入数据行后内部追加。

#### Scenario: 汇总行内容正确
- **WHEN** 导出包含 N 条运单数据
- **THEN** 汇总行的第 1 列 SHALL 为运单总数 N，第 6 列为所有毛重之和，第 7 列为所有皮重之和，第 8 列为所有净重之和，其余列为空

### Requirement: 时间格式统一
系统 SHALL 将所有时间字段（毛重时间、皮重时间）格式化为 `yyyy-MM-dd HH:mm:ss`。该格式 SHALL 由 **SolidWasteService** 在生成 `SolidWasteExportRow` 时保证，ExcelExportService 按行数据写出。

#### Scenario: 时间字段格式化输出
- **WHEN** 运单的 JoinTime 为 `2026-03-04 08:10:16`
- **THEN** Excel 中毛重时间列 SHALL 显示为 `2026-03-04 08:10:16`

#### Scenario: 时间字段为 null 时输出空
- **WHEN** 运单的 JoinTime 或 OutTime 为 null
- **THEN** SolidWasteService 返回的对应时间列为空字符串，Excel 中对应列 SHALL 为空
