## ADDED Requirements

### Requirement: DataManagementDialogWindow view has no business logic
`DataManagementDialogWindow` 视图层不得直接依赖业务服务或实现业务流程逻辑，所有与固废台账数据加载、分页与筛选相关的逻辑必须集中在对应的 ViewModel 中。

#### Scenario: View only handles rendering and basic interactions
- **WHEN** 开发者在 `DataManagementDialogWindow.axaml.cs` 中查看代码
- **THEN** 只能看到与界面渲染和基本交互相关的逻辑（如按钮点击关闭窗口、打开文件选择对话框、展示通知等），而看不到直接调用固废服务或手工计算分页的业务代码

### Requirement: DataManagementDialogWindow uses ReactiveUI ViewModel
固废台账管理对话框必须使用单独的 ReactiveUI ViewModel 类型（如 `DataManagementDialogViewModel`），该 ViewModel 负责管理固废台账的分页、筛选状态以及调用 `ISolidWasteService`。

#### Scenario: View binds to ReactiveUI ViewModel
- **WHEN** 窗口初始化并设置 `DataContext`
- **THEN** `DataContext` 绑定到一个继承自 `ViewModelBase` 的 ViewModel 实例，且分页控件、筛选输入框与导出逻辑均通过该 ViewModel 的属性和命令进行数据绑定或读取

### Requirement: Pagination is driven by commands and reactive properties
固废台账分页行为必须由 ViewModel 中的命令（例如 `LoadDataCommand`、`PageChangeCommand`）和带有变更通知的分页属性驱动，而非在 View 中手工构造过滤条件和直接调用分页接口。

#### Scenario: Pagination control interacts via command and properties
- **WHEN** 用户在对话框中点击“查询”按钮或通过分页控件切换页码
- **THEN** 视图只更新 ViewModel 的页码状态并执行相应命令，由 ViewModel 内部完成调用固废分页服务、更新记录集合和总条数，不在 View 中直接访问 `ISolidWasteService`

