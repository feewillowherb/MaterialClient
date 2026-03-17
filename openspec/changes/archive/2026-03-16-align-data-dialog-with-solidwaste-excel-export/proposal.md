# 提案：数据管理对话框与固废 Excel 导出对齐（职责分离）

## Why

当前数据管理对话框（`DataManagementDialogWindow`）中的台账表格列与固废 Excel 导出（`SolidWasteExcelExportService`）的 17 列不一致；且查询与导出逻辑均写在 `SolidWasteExcelExportService` 内，数据获取与 Excel 写入耦合在一起，不利于复用与测试。需要：（1）将对话框定位为 Excel 导出的预览界面并实现所见即所得；（2）**职责分离**：新增 **SolidWasteService** 作为固废列表数据的**唯一数据源**，对话框与 Excel 导出均从该服务取数；（3）将 **SolidWasteExcelExportService** 改为基于**通用 Excel 导出接口**实现，由业务层暴露不同接口并注入不同数据源，便于后续其他业务导出复用。

## What Changes

- **新增 SolidWasteService（唯一数据源）**：提供按 `SolidWasteExportFilter` 查询并返回 `SolidWasteExportRow` 列表的能力。所有需要“固废导出行数据”的消费者（数据管理对话框、固废 Excel 导出）均只依赖该服务，不再在导出服务内重复实现查询与映射逻辑。
- **ExcelExportService 仅提供具体业务导出方法**：**不**对外暴露通用 WriteAsync；接口仅暴露具体业务的导出方法（如固废运单导出）。实现类 ExcelExportService 内部保留“表头+行数据写 .xlsx”的通用逻辑为 **private** 方法，供各业务导出方法复用；固废导出逻辑（17 列、汇总行、ExportResult）合并进 ExcelExportService，依赖 SolidWasteService 取数。
- **删除 SolidWasteExcelExportService 类**：ExcelExportService 实现 **ISolidWasteExcelExportService**（保留 `ExportAsync(filter, outputPath)`），保证现有调用方无需改签名；数据源与写出职责仍分离（数据由 SolidWasteService，写出由 ExcelExportService 内部实现）。
- **表格列与导出一致**：将 `DataManagementDialogWindow` 内 DataGrid 的列与 `SolidWasteExcelExportService` 的 17 列完全一致，数据源改为 **SolidWasteService** 的查询结果（`SolidWasteExportRow`），保证所见即所得。
- **查询条件与导出一致**：对话框查询区与 `SolidWasteExportFilter` 对齐；查询时调用 **SolidWasteService** 获取列表并绑定表格；导出按钮仍调用 `ISolidWasteExcelExportService.ExportAsync`（其内部使用同一 SolidWasteService），保证预览与导出一致。
- **新增导出按钮**：在对话框底部增加“导出”按钮，使用当前筛选条件调用 `ISolidWasteExcelExportService.ExportAsync`，或先选路径再导出。

## Capabilities

### New Capabilities

- **solidwaste-service**：固废列表数据的唯一数据源。提供按 `SolidWasteExportFilter` 查询并返回 `SolidWasteExportRow` 列表的接口；封装 Waybill 查询、Provider/Material 关联与映射逻辑，供数据管理对话框与固废 Excel 导出共同使用。
- **excel-export（具体业务导出）**：ExcelExportService 仅提供**具体业务的导出方法**（如固废运单导出），不暴露通用 WriteAsync；通用“写表体”为内部实现。固废导出由 ExcelExportService 实现 ISolidWasteExcelExportService（ExportAsync(filter, outputPath)），依赖 SolidWasteService 取数。

### Modified Capabilities

- **solidwaste-excel-export**：固废 Excel 导出接口行为不变（仍接受 filter + outputPath），**实现由 ExcelExportService 承担**（不再存在独立的 SolidWasteExcelExportService 类）；ExcelExportService 依赖 SolidWasteService 获取数据，内部实现 17 列、汇总行与写文件。
- **attended-weighing-data-management-dialog-layout**：对话框表格列与固废 17 列一致；表格数据源来自 **SolidWasteService**（按当前筛选条件查询）；查询区与 `SolidWasteExportFilter` 一致；增加“导出”按钮（调用 `ISolidWasteExcelExportService`）；所见即所得。

## Impact

- **新增代码**：`ISolidWasteService` / `SolidWasteService`，位于 `MaterialClient.Common/Services/`。
- **重构**：**ExcelExportService** 不再暴露通用 WriteAsync，仅提供具体业务导出方法；固废导出逻辑合并进 ExcelExportService，实现 **ISolidWasteExcelExportService**；**删除** `SolidWasteExcelExportService.cs` 类文件。现有 `ISolidWasteExcelExportService` 对外签名保持不变。
- **受影响代码**：`DataManagementDialogWindow`（列定义、查询区、导出按钮）、其 ViewModel 或打开方需注入 **ISolidWasteService** 用于表格数据、**ISolidWasteExcelExportService** 用于导出（实现类为 ExcelExportService）。
- **依赖关系**：SolidWasteService 依赖 Waybill/Provider/Material 仓储；ExcelExportService 依赖 ISolidWasteService、ClosedXML 等，实现 ISolidWasteExcelExportService；对话框依赖 SolidWasteService + ISolidWasteExcelExportService。
- **服务注册**：采用 **ABP 集成式 + 隐式 + AutoConstructor**。各服务实现类实现 `ITransientDependency`（或相应 ABP 依赖接口）并标注 `[AutoConstructor]`，由 ABP 按约定扫描注册；不提供扩展方法、不在 Module 中显式注册，与项目内惯例（如 `SoundDeviceService`）一致。
