# 提案：数据管理对话框与固废 Excel 导出对齐（职责分离）

## Why

当前数据管理对话框（`DataManagementDialogWindow`）中的台账表格列与固废 Excel 导出（`SolidWasteExcelExportService`）的 17 列不一致；且查询与导出逻辑均写在 `SolidWasteExcelExportService` 内，数据获取与 Excel 写入耦合在一起，不利于复用与测试。需要：（1）将对话框定位为 Excel 导出的预览界面并实现所见即所得；（2）**职责分离**：新增 **SolidWasteService** 作为固废列表数据的**唯一数据源**，对话框与 Excel 导出均从该服务取数；（3）将 **SolidWasteExcelExportService** 改为基于**通用 Excel 导出接口**实现，由业务层暴露不同接口并注入不同数据源，便于后续其他业务导出复用。

## What Changes

- **新增 SolidWasteService（唯一数据源）**：提供按 `SolidWasteExportFilter` 查询并返回 `SolidWasteExportRow` 列表的能力。所有需要“固废导出行数据”的消费者（数据管理对话框、固废 Excel 导出）均只依赖该服务，不再在导出服务内重复实现查询与映射逻辑。
- **通用 Excel 导出接口**：抽象出与业务无关的“将表头 + 行数据写入 Excel 文件”的通用能力（接口 + 实现），接受列定义与数据源（如 `IEnumerable<TRow>` 或等效），不包含固废业务查询逻辑。不同业务通过各自接口暴露，并注入不同数据源（如固废业务注入 SolidWasteService）。
- **SolidWasteExcelExportService 改为业务门面**：保留现有 `ISolidWasteExcelExportService.ExportAsync(SolidWasteExportFilter, string outputPath)` 对外 API；内部改为：先通过 **SolidWasteService** 按 filter 取数，再通过**通用 Excel 导出**将行数据写入文件（含固废特有的表头、汇总行等约定）。数据源与写入职责分离。
- **表格列与导出一致**：将 `DataManagementDialogWindow` 内 DataGrid 的列与 `SolidWasteExcelExportService` 的 17 列完全一致，数据源改为 **SolidWasteService** 的查询结果（`SolidWasteExportRow`），保证所见即所得。
- **查询条件与导出一致**：对话框查询区与 `SolidWasteExportFilter` 对齐；查询时调用 **SolidWasteService** 获取列表并绑定表格；导出按钮仍调用 `ISolidWasteExcelExportService.ExportAsync`（其内部使用同一 SolidWasteService），保证预览与导出一致。
- **新增导出按钮**：在对话框底部增加“导出”按钮，使用当前筛选条件调用 `ISolidWasteExcelExportService.ExportAsync`，或先选路径再导出。

## Capabilities

### New Capabilities

- **solidwaste-service**：固废列表数据的唯一数据源。提供按 `SolidWasteExportFilter` 查询并返回 `SolidWasteExportRow` 列表的接口；封装 Waybill 查询、Provider/Material 关联与映射逻辑，供数据管理对话框与固废 Excel 导出共同使用。
- **generic-excel-export**：通用 Excel 导出能力。提供与业务无关的“按列定义将行数据写入 .xlsx”的接口与实现；业务层通过不同接口暴露、并注入不同数据源（如固废业务注入 SolidWasteService + 固废列定义与汇总行逻辑）。

### Modified Capabilities

- **solidwaste-excel-export**：固废 Excel 导出接口行为不变（仍接受 filter + outputPath），但实现改为依赖 **SolidWasteService** 获取数据、依赖**通用 Excel 导出**写入文件；不再内含 Waybill 查询与映射逻辑，仅负责固废业务侧的列定义与汇总行等约定。
- **attended-weighing-data-management-dialog-layout**：对话框表格列与固废 17 列一致；表格数据源来自 **SolidWasteService**（按当前筛选条件查询）；查询区与 `SolidWasteExportFilter` 一致；增加“导出”按钮（调用 `ISolidWasteExcelExportService`）；所见即所得。

## Impact

- **新增代码**：`ISolidWasteService` / `SolidWasteService`（或等效命名），位于 `MaterialClient.Common/Services/`；通用 Excel 导出接口与实现（如 `IExcelExportService` / `ExcelExportService` 或按列+数据源泛型设计）。
- **重构**：`SolidWasteExcelExportService` 内部移除查询与映射实现，改为调用 SolidWasteService + 通用 Excel 导出；现有 `ISolidWasteExcelExportService` 对外签名可保持不变。
- **受影响代码**：`DataManagementDialogWindow`（列定义、查询区、导出按钮）、其 ViewModel 或打开方需注入 **SolidWasteService** 用于表格数据、**ISolidWasteExcelExportService** 用于导出。
- **依赖关系**：SolidWasteService 依赖 Waybill/Provider/Material 仓储；通用 Excel 导出仅依赖 ClosedXML 等写文件能力；固废导出门面依赖 SolidWasteService + 通用 Excel 导出；对话框依赖 SolidWasteService + ISolidWasteExcelExportService。
- **服务注册**：采用**集成方式**，不在 Module 内零散注册。在 `MaterialClient.Common` 中提供扩展方法（如 `AddSolidWasteExportServices`），在扩展内集中注册上述服务；宿主或主模块调用该扩展完成注册。
