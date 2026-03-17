## MODIFIED Requirements

### Requirement: Query area layout and controls
数据管理对话框的顶部区域 SHALL 提供与示例截图一致的查询条件布局，包括但不限于车牌号、货名、发货单位、进场日期起止等输入或选择控件。进场日期的起止两个日期选择器 SHALL 使用 Ursa 的日期/时间选择组件（与项目内 `ExportFilterDialog`、`WeighingRecordListView` 一致），仅需日期时使用日期格式（如 `yyyy-MM-dd`），以保证与项目其他界面组件风格统一。

#### Scenario: Render query condition controls
- **WHEN** 数据管理对话框打开
- **THEN** 对话框顶部按设计要求渲染查询条件控件（文本框、Ursa 日期选择器等），并与截图中的排列顺序和对齐方式大体一致

#### Scenario: Date controls use Ursa component
- **WHEN** 用户查看进场日期起、止两个日期控件
- **THEN** 两者均为 Ursa 提供的日期/时间选择组件（如 `u:DateTimePicker` 配置为仅日期显示），非 Avalonia 原生 `DatePicker`

### Requirement: Ledger table columns and row style
数据管理对话框中间区域 SHALL 使用表格控件展示台账记录，其列集合、顺序与固废 Excel 导出一致（17 列：流水号、车号、发货单位、收货单位、货名、毛重、皮重、净重、备注、毛重时间、皮重时间、所属街道、类型、联单编号、上传结果、上传状态、上传时间）。表格 SHALL 仅绑定并展示**当前页**的数据（由分页查询得到），不一次性加载全部匹配记录，并采用条纹行、只读等样式。

#### Scenario: Render ledger table with representative columns
- **WHEN** 数据管理对话框打开并已执行查询
- **THEN** 表格控件展示上述 17 列及列头文本，且仅显示当前页的若干条记录（如 10 条），列宽、文本对齐足以支撑与导出 Excel 的列对应

#### Scenario: Table shows only current page
- **WHEN** 用户切换分页（如下一页、跳转页）
- **THEN** 表格内容更新为对应页的数据，总条数不变；不在此前将全部匹配记录加载到客户端

### Requirement: Pagination and footer controls
数据管理对话框底部区域 SHALL 提供可用的分页与操作区域：使用 Ursa 分页组件（如 `u:Pagination`）或与之等效的绑定方式，支持首页/上一页/下一页/末页及跳转页，并展示当前页、总页数、总条数；分页与表格数据联动，切换页时表格仅显示该页数据。右侧或合适位置 SHALL 保留“导出”“确定”等按钮。

#### Scenario: Render pagination and summary area
- **WHEN** 数据管理对话框打开并已执行查询
- **THEN** 底部显示分页控件（Ursa Pagination 或等效）、总条数/总页数/当前页信息，以及导出、确定等按钮

#### Scenario: Pagination changes current page data
- **WHEN** 用户点击下一页或跳转到指定页
- **THEN** 当前页序号更新，表格重新加载并仅显示该页数据，总条数保持不变

### Requirement: Data management dialog layout for attended weighing
系统在收料称重窗口中点击“数据管理”按钮时，SHALL 弹出一个用于浏览称重台账的对话框，该对话框承载真实查询、分页与导出交互。对话框 UI 由专门的视图文件（当前实现为 `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml`）提供，其具体类型可以是 Avalonia `Window` 或其他能支持模态行为的容器。

#### Scenario: Open dialog from attended weighing window
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击“数据管理”按钮
- **THEN** 系统显示一个模态对话框，覆盖在当前窗口之上，并展示台账列表页面布局；在对话框关闭之前，用户不能与 `AttendedWeighingWindow` 进行交互（例如点击称重相关按钮），以保证对话框行为为真正的“模态”

## ADDED Requirements

### Requirement: Same filter for query and export (WYSIWYG)
数据管理对话框的查询条件与 Excel 导出 SHALL 使用同一套过滤条件：查询与导出均基于相同的 `SolidWasteExportFilter`（起止日期、车牌号、货名、发货单位）。导出时 SHALL 使用当前对话框上的查询条件调用导出服务，导出的结果为该条件下**全部**匹配记录；页面表格则按分页显示同一数据集下的当前页，从而实现“所见即所得”（条件一致、结果集一致，页面仅分页展示）。

#### Scenario: Export uses current query condition
- **WHEN** 用户在对话框中设置查询条件并点击“导出”
- **THEN** 系统使用与当前查询相同的 `SolidWasteExportFilter` 调用导出服务，导出的 Excel 包含该条件下全部匹配运单，而非仅当前页

#### Scenario: Page and export share same result set semantics
- **WHEN** 用户先查询再分页浏览或导出
- **THEN** 表格当前页与导出文件所代表的数据集均来自同一过滤条件与同一业务结果集，仅展示方式为分页与全量之别

### Requirement: Window height for one page of rows
数据管理对话框的窗口高度 SHALL 固定或约束为可容纳约一页表格行（默认每页 10 条），即标题栏、查询区、约 10 行表格行高与底部分页/按钮区域在一屏内完整显示，避免窗口过高留白或需额外滚动才能看到一页完整内容。

#### Scenario: Window fits one page of table
- **WHEN** 用户打开数据管理对话框并执行查询
- **THEN** 窗口高度使表格区域约显示 10 行数据行，底部完整可见，无需为“看满一页”而滚动窗口

#### Scenario: Page size is ten by default
- **WHEN** 系统加载分页数据
- **THEN** 每页条数默认 SHALL 为 10，与窗口高度设计一致
