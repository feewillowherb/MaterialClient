## 1. SolidWasteService 分页支持

- [x] 1.1 在 `ISolidWasteService` 中新增分页方法（如 `GetPagedExportRowsAsync(SolidWasteExportFilter filter, int pageIndex, int pageSize)` 返回 `Task<(IReadOnlyList<SolidWasteExportRow> Items, int TotalCount)>` 或等效 DTO），与现有 `GetExportRowsAsync` 并存
- [x] 1.2 在 `SolidWasteService` 中实现分页方法：复用现有 `QueryWaybillsAsync` 及 Provider/Material 映射逻辑，在得到过滤后的 Waybill 列表基础上按 pageIndex/pageSize 做 skip/take，并返回总条数
- [x] 1.3 确保 `ExcelExportService.ExportSolidWasteAsync` 仍仅调用 `GetExportRowsAsync(filter)`（全量），不传分页参数

## 2. DataManagementDialogViewModel 与分页逻辑

- [x] 2.1 在 `DataManagementDialogViewModel` 中增加 `PageSize`（默认 10）、`TotalPages` 的计算属性或字段，以及分页命令（如 `PageChangeCommand` 或首页/上一页/下一页/末页/跳转命令），与 Ursa `Pagination` 的 `CurrentPage`、`TotalCount`、`PageSize`、`Command` 绑定兼容
- [x] 2.2 将表格数据源改为仅当前页：`Records` 仅填充当前页的 `SolidWasteExportRow`，查询时调用 `GetPagedExportRowsAsync(filter, pageIndex, pageSize)` 并更新 `TotalCount`、`TotalPages`、`CurrentPage`
- [x] 2.3 查询按钮执行后重置到第 1 页并加载第 1 页数据；分页切换时保留当前查询条件仅请求对应页数据

## 3. 视图：Ursa 日期与分页组件

- [x] 3.1 在 `DataManagementDialogWindow.axaml` 中为 Ursa 声明 `xmlns:u="https://irihi.tech/ursa"`（若尚未声明）
- [x] 3.2 将进场日期起、止两个 Avalonia `DatePicker` 替换为 `u:DateTimePicker`，`DisplayFormat`/`PanelFormat` 设为 `yyyy-MM-dd`，`SelectedDate` 仍绑定 `StartDate`、`EndDate`
- [x] 3.3 将底部分页区域改为使用 `u:Pagination`，绑定 `CurrentPage`、`TotalCount`、`PageSize` 及翻页/跳转命令（与 `MaterialsSelectionPopup`、`GenericSelectionPopup` 用法一致）；移除或替换原有首页/上一页/下一页/末页占位按钮
- [x] 3.4 调整窗口 `Height`（或 MinHeight）使一屏可容纳标题栏、查询区、约 10 行表格行高与底部分页/按钮区，表格区域不设过大留白

## 4. 导出与查询条件一致性

- [x] 4.1 确认“导出”按钮仍通过 `BuildFilter()` 从当前 ViewModel 的 `StartDate`、`EndDate`、`PlateNumber`、`GoodsName`、`ProviderName` 构建 `SolidWasteExportFilter`，并调用 `IExcelExportService.ExportSolidWasteAsync(filter, outputPath)`，无需修改导出接口
- [x] 4.2 确认未接入真实数据时仍可回退到一条本地测试数据用于样式验收（仅当前页显示该条）
