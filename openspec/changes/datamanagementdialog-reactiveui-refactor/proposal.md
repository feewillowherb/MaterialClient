## Why

当前 `DataManagementDialogWindow.axaml.cs` 中同时存在视图逻辑与业务逻辑：窗口直接持有 `ISolidWasteService`，在 Code-behind 中构造筛选条件、调用分页接口并处理异常。这种做法违背了项目既有的 ReactiveUI 与 MVVM 设计原则：View 只负责渲染和用户交互，业务状态与接口调用应集中在 ViewModel 中管理。随着固废台账管理功能的演进，这种混合会导致测试困难、复用性差，以及行为与其他界面风格不一致。

## What Changes

- 将固废台账管理窗口的分页加载与筛选逻辑从 `DataManagementDialogWindow.axaml.cs` 完整迁移到独立的 ReactiveUI 风格 ViewModel 中（`MaterialClient.ViewModels` 命名空间），实现业务代码与视图渲染的解耦。
- 统一使用 `ViewModelBase` + `[Reactive]` 属性管理 `CurrentPage`、`TotalCount`、`TotalPages` 等分页状态，并通过命令（如 `LoadDataCommand`、`PageChangeCommand`）驱动数据加载，而不是在 View 中直接调用服务。
- 窗口仅保留与 UI 交互紧密相关的代码（例如导出时弹窗选择目录、关闭对话框等），并通过依赖注入/构造函数获取 ViewModel 实例，将 `DataContext` 绑定到该 ViewModel。

## Capabilities

### New Capabilities
- `datamanagementdialog-reactiveui-refactor`: 保证固废台账管理窗口遵循项目统一的 ReactiveUI/MVVM 约定，业务接口调用和状态管理集中在 ViewModel 中，View 专注于渲染与交互。

### Modified Capabilities
- `attended-weighing-solidwaste-listing`: 对固废列表展示相关的 UI 交互层进行架构级改进，将原本散落在 View 中的业务逻辑收敛到 ViewModel，以提升一致性与可维护性。

## Impact

- 受影响代码：
  - `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml.cs`：删除或精简现有的业务调用、分页计算逻辑，仅保留视图相关操作，并改为通过 ViewModel 命令触发行为。
  - 新增 `MaterialClient/ViewModels/DataManagementDialogViewModel.cs`：实现固废台账管理的 ReactiveUI ViewModel，负责分页、筛选以及调用 `ISolidWasteService`。
- 受影响接口/依赖：
  - 对外服务契约（如 `ISolidWasteService.GetPagedExportRowsAsync`）本身不变，但其消费方式从 View 迁移到 ViewModel。
  - 需要确保依赖注入容器能够正确解析 `DataManagementDialogViewModel` 所需的服务（如 `ISolidWasteService`、`ILogger<DataManagementDialogViewModel>`）。
- 影响范围：
  - 固废台账管理对话框的内部结构和代码布局发生变化，但对最终用户的可见行为应保持不变或更一致（分页、筛选、导出等功能仍按原有方式工作）。
  - 为后续在该对话框中扩展逻辑（如新增过滤条件、增加状态指示等）提供更清晰的扩展点。

