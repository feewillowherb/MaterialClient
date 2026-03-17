## ADDED Requirements

### Requirement: Data management dialog layout for attended weighing
系统在收料称重窗口中点击“数据管理”按钮时，SHALL 弹出一个用于浏览称重台账的对话框，该对话框仅用于 UI 布局和样式验收，不承载真实业务交互逻辑。对话框 UI 由专门的视图文件（当前实现为 `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml`）提供，其具体类型可以是 Avalonia `Window` 或其他能支持模态行为的容器。

#### Scenario: Open dialog from attended weighing window
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击“数据管理”按钮
- **THEN** 系统显示一个模态对话框，覆盖在当前窗口之上，并展示台账列表页面布局；在对话框关闭之前，用户不能与 `AttendedWeighingWindow` 进行交互（例如点击称重相关按钮），以保证对话框行为为真正的“模态”

### Requirement: Query area layout and controls
数据管理对话框的顶部区域 SHALL 提供与示例截图一致的查询条件布局，包括但不限于运单编号、发货单号、商品名称、状态、起止日期等输入或选择控件，仅用于展示样式与布局。

#### Scenario: Render query condition controls
- **WHEN** 数据管理对话框打开
- **THEN** 对话框顶部按设计要求渲染查询条件控件（文本框、下拉框、日期选择器等），并与截图中的排列顺序和对齐方式大体一致

### Requirement: Ledger table columns and row style
数据管理对话框中间区域 SHALL 使用表格控件展示台账记录，其列集合、顺序和对齐方式与示例截图大体一致，至少包含运单号、车牌号、类型、商品、状态、运单量、扣量、实际数量、实际重量、单位换算、进场时间等列，并采用条纹行、悬浮/选中高亮等样式以便人工验收。

#### Scenario: Render ledger table with representative columns
- **WHEN** 数据管理对话框打开
- **THEN** 表格控件展示上述关键列及列头文本，并保证列宽、文本对齐、单元格间距等足以支撑与截图进行样式对比

### Requirement: Pagination and footer controls
数据管理对话框底部区域 SHALL 渲染分页和操作按钮区域，包括首页/上一页/下一页/末页按钮、跳转页输入/选择控件、当前页/总页数/总条数信息展示，以及“确定”或关闭类操作按钮，其布局与示例截图大体一致。

#### Scenario: Render pagination and summary area
- **WHEN** 数据管理对话框打开
- **THEN** 底部显示分页按钮、跳页控件以及统计信息文本，并在右侧或合适位置显示确认/关闭类按钮，其整体布局足以根据截图进行视觉验收

### Requirement: Single test record for style verification
系统在渲染数据管理对话框时 SHALL 使用至少一条内置本地测试数据记录填充台账表格，以便对行背景、选中状态、对齐方式和数值格式（如 m³、吨、吨/立方米）进行样式验收，该测试数据不依赖任何远端接口且不会被持久化。

#### Scenario: Display built-in test record
- **WHEN** 数据管理对话框首次打开且尚未接入真实数据
- **THEN** 台账表格中至少显示一条预置测试记录，字段值具有代表性并包含常见的单位与数值格式，用于对照截图进行视觉检查

### Requirement: Avalonia implementation constraints for dialog view
数据管理对话框的 XAML 实现 SHALL 符合当前项目的 Avalonia 约定，以保证编译通过与绑定正确。

#### Scenario: DataGrid binding and properties
- **WHEN** 对话框内使用 `DataGrid` 绑定台账集合
- **THEN** 使用 `ItemsSource` 绑定数据源（不使用 WPF 风格的 `Items`）；不设置 `CanUserAddRows`（Avalonia.Controls.DataGrid 无此属性）；只读行为通过 `IsReadOnly="True"` 表达

#### Scenario: Compiled bindings and x:DataType
- **WHEN** 项目启用 `AvaloniaUseCompiledBindingsByDefault`
- **THEN** 对话框根元素（如 `Window`）上 SHALL 声明合适的 `xmlns`（如 `xmlns:local`）及 `x:DataType`（指向对话框类型或对应 ViewModel），使 `{Binding Records}`、`{Binding CurrentPage}` 等编译绑定可解析；表格控件（`DataGrid`）上 SHALL 不设置会覆盖其 `DataContext` 的 `x:DataType`，以免 `ItemsSource` 等绑定无法解析
