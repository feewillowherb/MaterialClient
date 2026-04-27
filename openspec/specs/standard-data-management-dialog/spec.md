# standard-data-management-dialog

## Purpose

标准模式台账管理对话框，提供标准模式运单数据的查询、浏览和管理功能。

## Requirements

### Requirement: 标准模式台账管理对话框布局
系统在标准模式（WeighingMode.Standard）下，当用户在 AttendedWeighingWindow 中点击"台账管理"时，SHALL 弹出标准模式专用的数据管理对话框。对话框由 `MaterialClient/Views/AttendedWeighing/StandardDataManagementDialogWindow.axaml` 提供，标题为"台账管理"，尺寸与固废模式对话框一致（Width=1200, Height=500），采用相同的无边框样式（SystemDecorations="None"）和蓝色标题栏。

#### Scenario: 从有人值守称重窗口打开标准模式台账对话框
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击"台账管理"按钮
- **AND WHEN** 当前 `WeighingMode` 为 Standard
- **THEN** 系统 SHALL 显示标准模式专用的模态对话框 `StandardDataManagementDialogWindow`
- **AND** 对话框 SHALL 覆盖在当前窗口之上
- **AND** 对话框关闭前用户不能与 `AttendedWeighingWindow` 交互

### Requirement: 标准模式 DataGrid 列定义
标准模式台账对话框的 DataGrid SHALL 展示以下 15 列，按顺序排列：

| 列序 | Header | Binding 属性 | MinWidth |
|------|--------|-------------|----------|
| 1 | 车牌号 | PlateNumber | 80 |
| 2 | 类型 | DeliveryType | 80 |
| 3 | 商品 | MaterialName | 100 |
| 4 | 状态 | OrderType | 80 |
| 5 | 运单数量 | PlanQuantity | 80 |
| 6 | 运单重量 | PlanWeight | 80 |
| 7 | 扣量 | OffsetCount | 80 |
| 8 | 实际数量 | ActualQuantity | 80 |
| 9 | 实际重量 | ActualWeight | 80 |
| 10 | 单位换算 | UnitConversion | 80 |
| 11 | 进场时间 | JoinTime | 140 |
| 12 | 出场时间 | OutTime | 140 |
| 13 | 供应商 | ProviderName | 120 |
| 14 | 发货单号 | OrderNo | 120 |
| 15 | 备注 | Remark | 100 |

#### Scenario: 渲染标准模式台账表格
- **WHEN** 标准模式台账对话框打开
- **THEN** DataGrid SHALL 展示上述 15 列
- **AND** 列头文本 SHALL 与上表一致
- **AND** DataGrid SHALL 设置 IsReadOnly="True"、AutoGenerateColumns="False"
- **AND** 列 SHALL 支持用户拖拽调整宽度（CanUserResizeColumns="True"）

### Requirement: 标准模式查询条件区域
标准模式台账对话框顶部 SHALL 提供查询条件区域，包含以下筛选控件：

| 控件 | 类型 | 绑定属性 | 说明 |
|------|------|---------|------|
| 车牌号 | TextBox | PlateNumber | 文本筛选 |
| 类型 | ComboBox | SelectedDeliveryType | 选项：全部/收料/发料 |
| 商品名称 | TextBox | MaterialName | 文本筛选 |
| 状态 | ComboBox | SelectedOrderType | 选项：全部/首称中/已完成/已取消 |
| 进场日期起 | DateTimePicker | StartDate | 格式 yyyy-MM-dd |
| 进场日期止 | DateTimePicker | EndDate | 格式 yyyy-MM-dd |
| 查询按钮 | Button | QueryCommand | primary-button 样式 |

#### Scenario: 渲染查询条件控件
- **WHEN** 标准模式台账对话框打开
- **THEN** 对话框顶部 SHALL 按上述定义渲染查询条件控件
- **AND** 布局与固废模式查询区域保持一致的间距和对齐风格

#### Scenario: 执行查询
- **WHEN** 用户点击"查询"按钮
- **THEN** 系统 SHALL 将当前页重置为第 1 页
- **AND** 使用当前筛选条件重新加载数据

### Requirement: 标准模式不包含导出功能
标准模式台账对话框 SHALL NOT 包含 CSV/Excel 导出按钮。底部操作区域仅包含"关闭"按钮。

#### Scenario: 底部操作区域仅显示关闭按钮
- **WHEN** 标准模式台账对话框打开
- **THEN** 底部操作区域 SHALL 仅显示"关闭"按钮
- **AND** SHALL NOT 显示"导出"按钮

### Requirement: 标准模式分页控件
标准模式台账对话框底部 SHALL 包含 Ursa.Pagination 分页控件，与固废模式保持一致的绑定方式。

#### Scenario: 分页数据加载
- **WHEN** 对话框打开或用户切换页码
- **THEN** 系统 SHALL 调用分页查询加载当前页数据
- **AND** 显示当前页码、总页数、总条数

#### Scenario: 空数据回退到测试数据
- **WHEN** 分页查询失败或无数据
- **THEN** 系统 SHALL 显示一条预置测试数据记录
- **AND** 将总页数设为 1、总条数设为 1

### Requirement: 标准 DataGrid 绑定约束
标准模式对话框的 XAML 实现 SHALL 符合项目 Avalonia 约定，与 `attended-weighing-data-management-dialog-layout` spec 中定义的约束一致。

#### Scenario: 使用编译绑定
- **WHEN** 标准模式对话框使用数据绑定
- **THEN** 根 Window 元素 SHALL 声明 `x:DataType` 指向 `StandardDataManagementDialogViewModel`
- **AND** DataGrid 上 SHALL 不设置覆盖 DataContext 的 `x:DataType`
