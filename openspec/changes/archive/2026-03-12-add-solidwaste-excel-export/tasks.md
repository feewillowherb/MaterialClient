## 1. 依赖配置

- [x] 1.1 在 `Directory.Packages.props` 中添加 `ClosedXML` 包版本（最新稳定版）
- [x] 1.2 在 `MaterialClient.Common/MaterialClient.Common.csproj` 中添加 `ClosedXML` 包引用（不带版本号，由 CPM 管理）

## 2. 数据模型

- [x] 2.1 创建 `SolidWasteExportFilter` 过滤条件类（`MaterialClient.Common/Models/`），包含 `DateTime? StartDate`、`DateTime? EndDate`、`string? PlateNumber`、`string? GoodsName`、`string? ProviderName` 全部可空属性
- [x] 2.2 创建 `SolidWasteExportRow` 中间 DTO（`MaterialClient.Common/Models/`），包含与 sample.csv 17 列一一对应的属性
- [x] 2.3 创建 `ExportResult` 结果类，包含 `RowCount`、`FilePath`、`Success` 属性

## 3. 服务接口与实现

- [x] 3.1 创建 `ISolidWasteExcelExportService` 接口（`MaterialClient.Common/Services/`），定义 `ExportAsync(SolidWasteExportFilter filter, string outputPath)` 方法
- [x] 3.2 实现 `SolidWasteExcelExportService`，注入 `IRepository<Waybill, long>`、`IRepository<Provider, int>`、`IRepository<Material, int>`
- [x] 3.3 实现查询逻辑：固定过滤 `WeighingMode == SolidWaste` + `OrderType == Completed`，可空过滤 `AddDate` 日期范围、`PlateNumber`（Contains）、`GoodsName`（关联 Material.Name Contains）、`ProviderName`（关联 Provider.ProviderName Contains）
- [x] 3.4 实现 Waybill → SolidWasteExportRow 的映射逻辑，包括 ExtraProperties 字段解析（GetStreet、GetSolidWasteType、GetSolidWasteOrderNumber、GetShipper）和关联实体名称查询；上传结果/上传状态/上传时间三列固定为空
- [x] 3.5 实现 ClosedXML 写入逻辑：表头行、数据行、汇总行，时间格式化为 `yyyy-MM-dd HH:mm:ss`
- [x] 3.6 处理空数据场景：无符合条件的运单时生成仅含表头的 Excel 文件

## 4. 依赖注入注册

- [x] 4.1 在 ABP 模块中注册 `SolidWasteExcelExportService` 为 `ISolidWasteExcelExportService` 的实现

## 5. 测试

- [x] 5.1 编写 Waybill → SolidWasteExportRow 映射逻辑的单元测试，覆盖正常映射、null 字段安全处理、关联实体缺失场景、上传列为空
- [ ] 5.2 编写过滤逻辑的单元测试，覆盖全 null 参数、部分参数、模糊匹配场景
- [ ] 5.3 编写导出服务的集成测试，验证生成的 Excel 文件表头、数据行数、汇总行内容正确
