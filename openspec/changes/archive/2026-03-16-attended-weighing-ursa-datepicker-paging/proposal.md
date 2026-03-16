## Why

台账管理对话框当前使用 Avalonia 原生 DatePicker，与项目其他界面（如导出筛选、称重记录）采用的 Ursa 组件风格不一致；同时对话框一次性加载全部查询结果且分页区域仅为占位，与 Excel 导出“同一条件、同一数据集”的预期不一致，且无法在界面上按页浏览。需要统一日期控件为 Ursa、实现真实分页，并让页面展示与导出在查询条件和结果集上保持一致（所见即所得），并控制窗口高度以适配每页 10 条。

## What Changes

- 将数据管理对话框中的进场日期起止两个 **DatePicker** 改为 **Ursa** 的日期/时间选择组件（与 `ExportFilterDialog`、`WeighingRecordListView` 一致），仅需日期时使用日期格式。
- 为台账管理对话框实现 **真实分页**：查询条件与 `SolidWasteExportFilter` 一致，数据由服务端/服务层按分页返回当前页条目，表格只绑定当前页数据，不再一次性加载全部记录。
- **所见即所得**：页面查询条件与 Excel 导出使用同一套 `SolidWasteExportFilter`；导出时使用当前对话框的查询条件，导出结果与“当前查询条件下的全部匹配记录”一致（即导出仍为全量匹配数据，页面按分页显示同一数据集）。
- 调整对话框 **窗口高度**，使一屏刚好显示约 **1 页 10 条** 表格行，避免留白或需滚动多页才填满一屏。

## Capabilities

### New Capabilities

- 无（本次为对现有数据管理对话框的修改与增强）。

### Modified Capabilities

- `attended-weighing-data-management-dialog-layout`：日期控件改为 Ursa 组件；分页从纯布局改为真实分页（每页条数可配置，默认 10 条）；查询与导出共用同一过滤条件与数据语义；窗口尺寸约束为每页 10 条高度。

## Impact

- **视图**：`MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml`（DatePicker → Ursa，分页区绑定命令/属性，窗口高度）。
- **ViewModel/逻辑**：`DataManagementDialogWindow.axaml.cs` 内 `DataManagementDialogViewModel` 及查询/导出/分页逻辑（当前页、总条数、总页数、分页命令、仅加载当前页数据）。
- **服务**：`MaterialClient.Common/Services/SolidWasteService.cs` 需支持分页查询（如 `GetExportRowsAsync` 增加 skip/take 或新增分页接口），以便页面按页取数；`ExcelExportService` 仍使用现有 `GetExportRowsAsync(filter)` 导出全量匹配数据，无需改接口签名。
- **依赖**：已引用 `Irihi.Ursa`，仅使用其日期/时间选择与分页组件，无新增包。
