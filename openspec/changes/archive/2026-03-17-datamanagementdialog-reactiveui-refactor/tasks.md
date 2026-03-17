## 1. 抽取并实现 ReactiveUI ViewModel

- [x] 1.1 在 `MaterialClient.ViewModels` 命名空间下创建 `DataManagementDialogViewModel` 类，继承 `ViewModelBase`，并引入 ReactiveUI/Fody 所需的引用
- [x] 1.2 在 ViewModel 中定义固废台账筛选属性（`StartDate`、`EndDate`、`PlateNumber`、`GoodsName`、`ProviderName`）和分页属性（`CurrentPage`、`PageSize` 常量、`TotalCount`、`TotalPages`），使用 `[Reactive]` 自动生成变更通知
- [x] 1.3 在 ViewModel 中实现 `Records` 集合（`ObservableCollection<SolidWasteExportRow>`）用于承载当前页数据
- [x] 1.4 在 ViewModel 中实现 `LoadDataCommand`（基于 `ReactiveCommand.CreateFromTask`），封装从当前筛选状态构造 `SolidWasteExportFilter`、调用 `ISolidWasteService.GetPagedExportRowsAsync` 并更新 `Records`、`TotalCount`、`TotalPages` 的逻辑
- [x] 1.5 在 ViewModel 中实现 `PageChangeCommand`，接收页码参数并在合法范围内更新 `CurrentPage` 后调用 `LoadDataAsync`，以支持 `u:Pagination` 控件交互

## 2. 精简 DataManagementDialogWindow 视图逻辑

- [x] 2.1 在 `DataManagementDialogWindow.axaml.cs` 中移除旧的内部 `DataManagementDialogViewModel` 定义以及直接调用 `ISolidWasteService` 的分页逻辑
- [x] 2.2 调整 `DataManagementDialogWindow` 构造函数，通过构造函数参数获得 `ISolidWasteService` 实例，创建新的 `DataManagementDialogViewModel` 并将其设置为 `DataContext`
- [x] 2.3 在窗口初始化完成后，通过执行 ViewModel 的 `LoadDataCommand` 进行首次数据加载，而不是在 View 中手工调用服务
- [x] 2.4 更新查询按钮点击事件 `OnQueryClick`，仅修改 ViewModel 的 `CurrentPage` 并执行 `LoadDataCommand`，不再在 View 中构造筛选条件

## 3. 绑定与行为验证

- [x] 3.1 检查 `DataManagementDialogWindow.axaml` 中筛选输入控件和分页控件的绑定，确保它们分别绑定到新的 ViewModel 属性（如 `StartDate`、`EndDate`、`PlateNumber`、`CurrentPage`、`TotalCount`、`PageSize`、`PageChangeCommand`）
- [x] 3.2 运行应用并打开固废台账管理对话框，验证在正常情况下数据能够按照筛选条件正确加载，分页信息与记录数显示正确，分页按钮工作正常
- [x] 3.3 在服务不可用或发生异常的场景下（例如断开后端或模拟异常），验证 ViewModel 内的异常处理逻辑能够避免应用崩溃，并按设计回退到测试数据或空列表
- [x] 3.4 回归验证导出按钮行为，确保导出仍然使用 ViewModel 中的筛选状态构造 `SolidWasteExportFilter`，且通知弹窗显示正常

