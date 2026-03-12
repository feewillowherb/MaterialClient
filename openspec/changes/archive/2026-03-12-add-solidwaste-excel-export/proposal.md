## Why

固废模式（SolidWaste）下的运单数据目前无法导出为 Excel 文件。业务方需要定期将称重数据以标准格式导出，用于对接城管固废监管系统的数据上报、日报/月报统计以及纸质台账存档。导出格式需严格对齐现有的纸质台账模板（参见 `sample.csv`）。

## What Changes

- 新增 `ISolidWasteExcelExportService` 导出接口和实现，位于 `MaterialClient.Common/Services/`
- 引入第三方 Excel 库依赖（ClosedXML 或 Magicodes.IE.Excel，需在 design.md 中评估确定）
- 查询 `Waybill` 数据，固定筛选 `WeighingMode == WeighingMode.SolidWaste`，支持以下可空过滤参数：
  - `AddDate` 日期范围（`DateTime? startDate`, `DateTime? endDate`）
  - 车牌号（`string? plateNumber`）
  - 货名（`string? goodsName`）
  - 发货单位（`string? providerName`）
- 按照 `sample.csv` 模板格式输出 `.xlsx` 文件，包含：
  - 17 列标准字段映射（流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间）
  - 其中上传结果、上传状态、上传时间三列固定输出为空
  - 末尾汇总行（总车次、毛重合计、皮重合计、净重合计）
- 部分字段需从 `Waybill.ExtraProperties`（SolidWasteInfo 扩展）和关联实体（`Provider`、`Material`）中解析

## Capabilities

### New Capabilities
- `solidwaste-excel-export`: 固废模式运单 Excel 导出服务，提供多条件可空过滤查询并导出为 `.xlsx` 文件的能力

### Modified Capabilities
（无现有 spec 需要修改）

## Impact

- **新增依赖**: 需要在 `Directory.Packages.props` 中添加 Excel 库包引用，在 `MaterialClient.Common.csproj` 中引用
- **新增代码**: `MaterialClient.Common/Services/` 下新增导出服务接口和实现
- **数据访问**: 需要通过 `IRepository<Waybill, long>` 查询运单，并关联 `Provider`、`Material` 获取名称
- **ExtraProperties 依赖**: 需使用 `SolidWasteInfoExtensions` 读取街道、联单编号、固废类型、发货单位等字段
