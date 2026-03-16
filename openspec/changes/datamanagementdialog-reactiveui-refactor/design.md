## Context

固废台账管理对话框 `DataManagementDialogWindow` 当前已经使用 Avalonia + ReactiveUI，但在实现上存在以下问题：
- Code-behind (`DataManagementDialogWindow.axaml.cs`) 直接依赖 `ISolidWasteService` 并在 View 中构造筛选条件、调用分页接口、处理异常，导致 View 中混入业务逻辑。
- ViewModel 以内部类形式定义在同一个文件中，基于手写 `INotifyPropertyChanged`，与项目其他使用 `ViewModelBase` + `[Reactive]` 的 ViewModel 风格不一致。
- 分页和筛选逻辑难以单元测试，也不便于在其他上下文重用。

项目整体已经采用 ReactiveUI + MVVM 模式，并提供统一的 `ViewModelBase` 与源生成属性工具（ReactiveUI.Fody），本设计旨在将固废台账管理功能对齐到这一约定上。

## Goals / Non-Goals

**Goals:**
- 将固废台账分页加载、筛选、异常处理等业务逻辑从 View 完整迁移到独立的 ReactiveUI ViewModel 中，View 只负责渲染与基本交互。
- 使用 `ViewModelBase` 作为基类，并通过 `[Reactive]` 属性管理分页相关状态（`CurrentPage`、`TotalCount`、`TotalPages` 等），统一项目中分页 ViewModel 的实现风格。
- 通过命令（如 `LoadDataCommand`、`PageChangeCommand`）驱动数据加载，使得分页控件等 UI 元素只需绑定命令和状态即可。

**Non-Goals:**
- 不改变固废台账业务逻辑本身（查询条件、分页行为、导出规则等），仅对架构和职责分层进行整理。
- 不在本次变更中引入新的 UI 功能或复杂交互（例如新增过滤字段、增加多选导出等）。
- 不调整已有的服务契约（例如 `ISolidWasteService` 的方法签名）除非为对齐 ABP 分页契约所必须的 long/TotalCount 已在其他变更中处理。

## Decisions

- **ViewModel 抽取与命名空间统一：**
  - 在 `MaterialClient.ViewModels` 命名空间下创建独立的 `DataManagementDialogViewModel` 类，继承 `ViewModelBase`。
  - 使用 `ReactiveUI.Fody` 的 `[Reactive]` 属性来简化属性变更通知，并与现有 `AttendedWeighingViewModel`、`PhotoGridViewModel` 等保持一致风格。
  - ViewModel 通过构造函数注入 `ISolidWasteService` 与可选的 `ILogger<DataManagementDialogViewModel>`。

- **分页与筛选逻辑迁移到 ViewModel：**
  - 在 ViewModel 中实现：
    - 基本属性：`StartDate`、`EndDate`、`PlateNumber`、`GoodsName`、`ProviderName`。
    - 分页属性：`CurrentPage`、`PageSize`（常量）、`TotalCount`、`TotalPages`。
    - 数据集合：`ObservableCollection<SolidWasteExportRow> Records`。
  - 实现 `LoadDataCommand`（`ReactiveCommand.CreateFromTask`）用于：
    - 根据当前筛选属性构造 `SolidWasteExportFilter`。
    - 调用 `ISolidWasteService.GetPagedExportRowsAsync` 获取分页结果。
    - 填充 `Records`、计算 `TotalCount` 和 `TotalPages` 并矫正 `CurrentPage`。
  - 实现 `PageChangeCommand`（`ReactiveCommand.CreateFromTask<int>`）用于响应分页控件的页码变化；当接收到合法页码时更新 `CurrentPage` 并调用 `LoadDataAsync`。

- **视图与 ViewModel 的绑定方式：**
  - 在 `DataManagementDialogWindow` 构造函数中通过构造注入的 `ISolidWasteService` 创建 `DataManagementDialogViewModel` 实例，并设置为 `DataContext`。
  - 初次打开窗口时，通过执行 `LoadDataCommand` 触发一次数据加载，而不是在 View 中直接调用服务。
  - 查询按钮点击事件（`OnQueryClick`）只修改 ViewModel 的 `CurrentPage` 并执行 `LoadDataCommand`，不在 View 中重建过滤条件。
  - XAML 中的 `u:Pagination` 控件绑定 `CurrentPage`、`TotalCount`、`PageSize` 和 `PageChangeCommand`，实现分页 UI 与 ViewModel 命令/状态的解耦。

- **错误处理策略：**
  - 在 ViewModel 的 `LoadDataAsync` 中捕获服务调用异常并记录日志（使用 `Logger`），并回退到一条简单的测试数据或清空列表。
  - View 保留与 UI 通知密切相关的逻辑（例如导出失败时通过 `WindowNotificationManager` 弹出消息），并在导出逻辑中直接构造 `SolidWasteExportFilter` 使用 ViewModel 的筛选属性值。

## Risks / Trade-offs

- [Risk] 现有窗口逻辑被拆分到 ViewModel，可能在迁移初期引入绑定错误或命令未触发的情况。
  - Mitigation: 保持行为等价的前提下分步迁移；在完成后通过手工测试所有关键用户路径（查询、分页、导出）并在必要时添加 UI 层的回归测试。

- [Risk] 将更多依赖注入到 ViewModel 可能增加 DI 配置复杂度。
  - Mitigation: 避免在 ViewModel 中直接解析额外服务，保持仅依赖 `ISolidWasteService` 和可选 `ILogger`，并遵循现有 DI 注册模式。

- [Risk] View 中仍然保留导出逻辑（因为涉及文件对话框等 UI 交互），这部分与纯 MVVM 有轻微偏离。
  - Mitigation: 明确界定导出逻辑中的 UI 部分（选择目录、弹出通知）保留在 View，而数据过滤和服务调用策略在未来有需要时可以进一步抽象到 ViewModel 或应用服务。

