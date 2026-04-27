## 1. 数据模型

- [x] 1.1 在 `MaterialClient.Common/Models/` 下创建 `StandardExportRow.cs`，定义 15 个属性（PlateNumber、DeliveryType、MaterialName、OrderType、PlanQuantity、PlanWeight、OffsetCount、ActualQuantity、ActualWeight、UnitConversion、JoinTime、OutTime、ProviderName、OrderNo、Remark），时间字段为 string 类型（格式化后），数值字段为 decimal?，其余为 string

## 2. ViewModel

- [x] 2.1 创建 `ViewModels/StandardDataManagementDialogViewModel.cs`，继承 `ViewModelBase`，实现 `ITransientDependency`
- [x] 2.2 注入 `IRepository<Waybill>` 和 `ILogger`，实现分页查询方法 `LoadDataAsync()`，查询 `WeighingMode == Standard && IsDeleted == false` 的 Waybill，Include Material 和 Provider，按 JoinTime 降序排列，使用 Skip/Take 分页
- [x] 2.3 实现 Waybill → StandardExportRow 映射逻辑，处理 DeliveryType/OrderType 枚举到中文显示的转换，处理 Material/Provider 为 null 的情况，时间格式化为 "yyyy-MM-dd HH:mm:ss"
- [x] 2.4 实现筛选条件属性：PlateNumber、SelectedDeliveryType（全部/收料/发料）、MaterialName、SelectedOrderType（全部/首称中/已完成/已取消）、StartDate、EndDate
- [x] 2.5 实现 BuildFilter() 方法，根据筛选条件构建 LINQ 查询
- [x] 2.6 实现 QueryCommand（重置到第 1 页并查询）、PageChangeCommand（分页切换）、CloseCommand、ConfirmCommand
- [x] 2.7 创建测试数据方法 `CreateTestRow()`，返回一条有代表性的 StandardExportRow 记录，用于查询失败时的回退显示
- [x] 2.8 定义 `DeliveryTypeFilterOption` 和 `OrderTypeFilterOption` 静态选项集合

## 3. 视图文件

- [x] 3.1 创建 `Views/AttendedWeighing/StandardDataManagementDialogWindow.axaml`，声明 x:DataType 指向 StandardDataManagementDialogViewModel，设置 Width=1200 Height=500、SystemDecorations="None"、Background="White"
- [x] 3.2 实现标题栏区域（蓝色 #6498FE 背景，白色"台账管理"文字，关闭按钮绑定 CloseCommand）
- [x] 3.3 实现查询条件区域：车牌号 TextBox、类型 ComboBox（绑定 DeliveryTypeFilterOption）、商品名称 TextBox、状态 ComboBox（绑定 OrderTypeFilterOption）、进场日期起止 DateTimePicker、查询按钮（绑定 QueryCommand）
- [x] 3.4 实现 DataGrid 区域，定义 15 列 DataGridTextColumn，绑定 Records 集合，设置 IsReadOnly="True"、AutoGenerateColumns="False"、CanUserResizeColumns="True"
- [x] 3.5 实现底部区域：左侧 Ursa.Pagination 控件（绑定 CurrentPage/TotalCount/PageSize/PageChangeCommand），右侧仅显示"关闭"按钮（绑定 ConfirmCommand），不包含"导出"按钮

## 4. Code-behind

- [x] 4.1 创建 `Views/AttendedWeighing/StandardDataManagementDialogWindow.axaml.cs`，实现 ITransientDependency
- [x] 4.2 构造函数接收 StandardDataManagementDialogViewModel，设置 DataContext，订阅 CloseCommand 和 ConfirmCommand 执行 Close()
- [x] 4.3 在构造函数中调用 `viewModel.LoadDataCommand.Execute(null)` 加载初始数据
- [x] 4.4 在 OnClosed 中 Dispose 订阅

## 5. 路由集成

- [x] 5.1 修改 `AttendedWeighingWindow.axaml.cs` 中的 `OpenLedgerManagementDialogAsync()` 方法，根据当前 WeighingMode 判断：Standard 模式创建 StandardDataManagementDialogViewModel + StandardDataManagementDialogWindow，SolidWaste 模式保持现有逻辑不变
- [x] 5.2 确保 StandardDataManagementDialogViewModel 通过 IServiceProvider.GetRequiredService 正确注入
