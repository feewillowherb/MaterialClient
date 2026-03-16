## MODIFIED Requirements

### Requirement: Data management dialog layout for attended weighing
系统在收料称重窗口中点击“数据管理”按钮时，SHALL 弹出一个用于浏览固废导出数据的对话框。该对话框作为固废 Excel 导出的预览与操作入口：表格列、数据来源与固废 Excel 导出一致；表格数据 SHALL 来自 **SolidWasteService**（唯一数据源），并提供“导出”按钮以将当前筛选结果通过 `ISolidWasteExcelExportService` 导出为 Excel，实现所见即所得。对话框 UI 由专门的视图文件（当前实现为 `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml`）提供，其具体类型可以是 Avalonia `Window` 或其他能支持模态行为的容器。

#### Scenario: Open dialog from attended weighing window
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击“数据管理”按钮
- **THEN** 系统显示一个模态对话框，覆盖在当前窗口之上，并展示与固废 Excel 导出一致的表格列及查询区；在对话框关闭之前，用户不能与 `AttendedWeighingWindow` 进行交互（例如点击称重相关按钮），以保证对话框行为为真正的“模态”

### Requirement: Query area layout and controls
数据管理对话框的顶部区域 SHALL 提供与固废导出筛选条件一致的查询控件，对应 `SolidWasteExportFilter` 的字段：进场日期（AddDate）起止、车牌号、货名、发货单位。查询结果 SHALL 通过 **SolidWasteService** 按当前筛选条件获取，与调用 `ISolidWasteExcelExportService.ExportAsync` 时使用的数据源一致，以保证预览即导出。

#### Scenario: Render query condition controls
- **WHEN** 数据管理对话框打开
- **THEN** 对话框顶部渲染与 `SolidWasteExportFilter` 对应的查询条件控件（起止日期、车牌号、货名、发货单位等），排列与对齐方式满足使用需求

#### Scenario: Query drives same data as export
- **WHEN** 用户设置筛选条件并触发查询（或打开对话框时使用当前筛选条件加载数据）
- **THEN** 系统 SHALL 调用 **SolidWasteService** 获取与当前 `SolidWasteExportFilter` 对应的 `SolidWasteExportRow` 列表并绑定表格；该数据与使用相同 filter 调用 `ISolidWasteExcelExportService.ExportAsync` 将写入 Excel 的行数据一致（列、顺序、内容一致）

### Requirement: Ledger table columns and row style
数据管理对话框中间区域 SHALL 使用表格控件展示与固废 Excel 导出完全一致的列集合与顺序。列 SHALL 与固废 Excel 导出的 17 列一致（与 SolidWasteService 返回的 `SolidWasteExportRow` 结构一致），列头文本依次为：流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间。表格数据源 SHALL 使用与导出服务相同的行模型（如 `SolidWasteExportRow`），并采用条纹行、悬浮/选中高亮等样式以便阅读与验收。

#### Scenario: Render ledger table with export columns
- **WHEN** 数据管理对话框打开
- **THEN** 表格控件展示上述 17 列及列头文本，列顺序与固废 Excel 导出写入的列顺序一致，列宽、文本对齐、单元格间距足以支撑阅读与视觉验收

#### Scenario: Table data matches export row model
- **WHEN** 表格绑定数据源
- **THEN** 每行数据对应与 `SolidWasteExportRow` 一致的字段（流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间），所见即导出所得

### Requirement: Pagination and footer controls
数据管理对话框底部区域 SHALL 渲染分页和操作按钮区域，包括首页/上一页/下一页/末页按钮、跳转页输入/选择控件、当前页/总页数/总条数信息展示，“确定”或关闭类按钮，以及“导出”按钮。导出按钮 SHALL 使用当前界面上的筛选条件调用 `ISolidWasteExcelExportService.ExportAsync` 将当前筛选结果导出为 Excel（路径由用户选择或使用配置的默认路径）；导出内部使用同一 SolidWasteService 数据源，保证所见即所得。

#### Scenario: Render pagination and summary area
- **WHEN** 数据管理对话框打开
- **THEN** 底部显示分页按钮、跳页控件以及统计信息文本，并在右侧或合适位置显示“确定”/关闭类按钮与“导出”按钮，其整体布局满足视觉与操作验收

#### Scenario: Export button exports current filter result
- **WHEN** 用户点击“导出”按钮并确认保存路径（若需要）
- **THEN** 系统使用当前对话框上的筛选条件构建 `SolidWasteExportFilter`，调用 `ISolidWasteExcelExportService.ExportAsync` 将当前筛选结果导出为 Excel 文件；导出与表格均基于 SolidWasteService 的同一数据源，导出内容与表格中展示的数据一致（所见即所得）

### Requirement: Single test record for style verification
系统在渲染数据管理对话框且尚未接入真实查询数据时，SHALL 可使用至少一条符合固废导出行结构（与 `SolidWasteExportRow` 一致）的本地测试数据填充台账表格，以便对列头、行背景、选中状态、对齐方式和数值格式进行样式验收，该测试数据不依赖任何远端接口且不会被持久化。接入真实查询后，表格以查询结果为准。

#### Scenario: Display built-in test record
- **WHEN** 数据管理对话框首次打开且尚未接入真实数据
- **THEN** 台账表格可显示一条或多条预置测试记录，字段结构与 `SolidWasteExportRow` 一致（流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间），用于列与样式视觉检查

### Requirement: Avalonia implementation constraints for dialog view
数据管理对话框的 XAML 实现 SHALL 符合当前项目的 Avalonia 约定，以保证编译通过与绑定正确。表格绑定的数据源 SHALL 为与 `SolidWasteExportRow` 兼容的集合（如 `ObservableCollection<SolidWasteExportRow>` 或等效展示模型）。

#### Scenario: DataGrid binding and properties
- **WHEN** 对话框内使用 `DataGrid` 绑定台账集合
- **THEN** 使用 `ItemsSource` 绑定数据源（不使用 WPF 风格的 `Items`）；不设置 `CanUserAddRows`（Avalonia.Controls.DataGrid 无此属性）；只读行为通过 `IsReadOnly="True"` 表达

#### Scenario: Compiled bindings and x:DataType
- **WHEN** 项目启用 `AvaloniaUseCompiledBindingsByDefault`
- **THEN** 对话框根元素（如 `Window`）上 SHALL 声明合适的 `xmlns`（如 `xmlns:local`）及 `x:DataType`（指向对话框类型或对应 ViewModel），使 `{Binding Records}`、`{Binding CurrentPage}` 等编译绑定可解析；表格控件（`DataGrid`）上 SHALL 不设置会覆盖其 `DataContext` 的 `x:DataType`，以免 `ItemsSource` 等绑定无法解析
