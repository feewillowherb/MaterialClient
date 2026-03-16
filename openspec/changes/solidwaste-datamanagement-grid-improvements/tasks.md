## 1. 后端分页结构调整

- [x] 1.1 在 `MaterialClient.Common/Services/SolidWasteService.cs` 中定位固废分页查询相关方法，确认当前返回类型和字段结构
- [x] 1.2 将固废分页查询方法的返回值改为 ABP 标准分页 DTO（如 `PagedResultDto<T>` 或项目统一分页类型），并在方法内部构造统一结构（含 `totalCount` 与 `items`）
- [x] 1.3 搜索所有调用固废分页查询接口的前端/服务调用点，调整为读取标准分页字段（例如 `totalCount`、`items`）
- [x] 1.4 本地运行相关接口或集成测试，确认分页数据返回结构符合预期且无运行时异常

## 2. 日期筛选控件布局优化

- [x] 2.1 在 `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml` 中定位与固废数据管理相关的日期筛选控件所在区域
- [x] 2.2 调整日期控件所在的栅格列定义（如 `ColumnDefinitions`）与控件属性，为日期控件增加合适的 `MinWidth` 或 `Width`，保证默认窗口下日期文本完整显示
- [x] 2.3 在不同窗口宽度下手动验证对话框布局，确保日期控件未被严重压缩，且不会明显挤压其他筛选条件或按钮

## 3. 列宽可调整体验改进

- [x] 3.1 在 `DataManagementDialogWindow.axaml` 中定位用于展示固废数据的 `DataGrid` 或列表控件定义
- [x] 3.2 确认并设置 `CanUserResizeColumns=\"True\"`（如无则添加），为关键列配置合理的 `Width`/`MinWidth` 以支持用户拖拽调整列宽
- [x] 3.3 运行界面手动测试列宽拖拽，验证列宽可自由调整且不会缩小到不可读，同时整体布局仍然可用

## 4. TotalCount long 化与 DataManagementDialogViewModel 重构

- [x] 4.1 梳理项目中与固废台账管理相关的分页接口调用点（包括 `DataManagementDialogWindow.axaml.cs` 及其他使用固废分页结果的 ViewModel），确认哪些地方依赖 `TotalCount` 类型
- [x] 4.2 在后端服务层中，确保固废分页返回的 DTO（如 `PagedResultDto<SolidWasteExportRow>`）的 `TotalCount` 字段保持为 `long` 类型，并更新任何仍然使用自定义分页结果类型的实现
- [x] 4.3 在前端/客户端调用代码中（如 `DataManagementDialogWindow.axaml.cs`），统一通过显式转换从 `long` `TotalCount` 派生本地 `int` 计数，仅用于分页控件和简单显示，并在实现中记录不超过 `int.MaxValue` 的约束假设
- [x] 4.4 参考 `AttendedWeighingViewModel` 的实现风格，将 `DataManagementDialogViewModel` 从 Code-behind 内部类抽取到 `MaterialClient.ViewModels` 命名空间，继承统一的 ViewModel 基类（如 `ViewModelBase`）或基于 ReactiveUI `[Reactive]` 源生成属性定义分页相关属性（`CurrentPage`、`PageSize`、`TotalCount`、`TotalPages` 等）
- [x] 4.5 调整 `DataManagementDialogWindow.axaml` 的 `DataContext` 绑定与构造逻辑，使其通过依赖注入或工厂方式获取新的 ReactiveUI 风格的 `DataManagementDialogViewModel`，而非在窗口构造函数中手动 new 内部 VM 类型
- [x] 4.6 为新的 `DataManagementDialogViewModel` 增加分页与查询命令（例如 `LoadDataCommand` / `PageChangeCommand` 等），并在 XAML 中通过命令绑定触发分页与筛选逻辑，确保行为与现有实现一致或更优
- [x] 4.7 在完成重构后，运行固废台账管理相关的关键用户路径（查询、分页、导出等），验证 `TotalCount` 显示正确、分页行为正确、ReactiveUI 绑定无异常，并更新必要的单元测试或 UI 回归用例

