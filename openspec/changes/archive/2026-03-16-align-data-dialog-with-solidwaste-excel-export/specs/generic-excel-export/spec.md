## ADDED Requirements

### Requirement: ExcelExportService 仅提供具体业务导出方法
系统 SHALL 通过 **ExcelExportService** 提供 Excel 导出能力，但**不**对外暴露通用“写 Excel”方法（如接受任意表头、行集合与映射的 `WriteAsync<T>`）。接口 SHALL 仅暴露**具体业务的导出方法**（例如固废运单导出：`ExportSolidWasteAsync(SolidWasteExportFilter filter, string outputPath)` 或通过 `ISolidWasteExcelExportService.ExportAsync` 实现）。通用“表头+行数据写入 .xlsx”的逻辑 SHALL 作为 ExcelExportService 的**内部实现**（private 方法），供各业务导出方法复用。

#### Scenario: 固废导出为第一个业务导出方法
- **WHEN** 调用方需要导出固废运单
- **THEN** 系统 SHALL 通过 `ISolidWasteExcelExportService.ExportAsync(filter, outputPath)`（由 ExcelExportService 实现）完成导出，不暴露通用 WriteAsync 给调用方

#### Scenario: 扩展新业务导出
- **WHEN** 后续新增其他业务（如标准称重导出）
- **THEN** 系统 SHALL 在 ExcelExportService 中增加对应的业务导出方法，内部复用同一套写表逻辑；不新增对外通用写表接口

### Requirement: 服务注册方式
ExcelExportService 实现类 SHALL 实现 `Volo.Abp.DependencyInjection.ITransientDependency`，由 ABP 按约定隐式注册；不通过扩展方法或 Module 显式注册。有构造注入依赖（如 ISolidWasteService、ILogger）时 SHALL 标注 `[AutoConstructor]`。
