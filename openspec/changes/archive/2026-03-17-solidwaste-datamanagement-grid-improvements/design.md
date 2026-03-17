## Context

当前系统在固废过磅数据查询上已经有应用服务和前端页面，但存在以下技术层面问题：
- 后端 `SolidWasteService` 返回的分页结果未采用 ABP 约定的标准分页 DTO（如 `PagedResultDto<T>`），分页字段命名和结构与其他模块不一致，增加了前端适配成本。
- `DataManagementDialogWindow` 中用于筛选的日期控件布局较为紧凑，控件宽度不足，日期字符串容易被截断，影响筛选条件的可读性。
- `DataManagementDialogWindow` 中用于展示固废数据的 `DataGrid`/列表列宽为固定或体验较差，用户无法根据自身屏幕和关注字段灵活调整列宽。

本次变更在后端统一分页返回规范的同时，提升前端列表与筛选的交互体验。

## Goals / Non-Goals

**Goals:**
- 将固废数据分页查询接口的返回结构统一为 ABP 标准分页格式（例如使用 `PagedResultDto<T>` 或项目内通用分页 DTO），减少前端差异化处理。
- 在 `DataManagementDialogWindow` 中调整与固废数据相关的日期筛选控件布局（列定义、宽度等），确保日期文本在常见分辨率下均能完整展示。
- 使 `DataManagementDialogWindow` 中的数据列支持用户拖拽调整宽度，并设置更合理的初始宽度配置。

**Non-Goals:**
- 不引入新的业务字段或复杂过滤逻辑，现有查询条件范围保持不变（除布局与显示方式优化外）。
- 不在本次变更中处理与固废数据导出、打印等周边功能的深度改造，只在必要时适配新的分页结构。
- 不改变权限模型、路由结构等与本次需求无直接关系的系统行为。

## Decisions

- **分页返回采用 ABP 标准 DTO，TotalCount 统一使用 long：**
  - 在 `SolidWasteService` 中，将当前自定义分页返回类型替换为 ABP 推荐的标准分页 DTO（如 `PagedResultDto<SolidWasteExportRow>`），或项目已约定的包装类型。
  - 要求分页结果中的 `TotalCount` 字段在服务契约和实现中统一为 `long` 类型，与 ABP 默认约定保持一致，支持更大记录总数。
  - 保持查询方法的入参不变，仅调整返回类型与构造方式，内部查询仍通过现有仓储或应用服务实现。
  - 所有消费该分页结果的前端或 ViewModel 在内部如需使用 `int`（例如配合分页控件），应通过显式转换从 `long` 派生本地 `int` 计数，并在设计上确认记录总数不会超过 `int.MaxValue`。

- **日期筛选控件宽度与布局调整：**
  - 在 `DataManagementDialogWindow.axaml` 中，针对筛选区域的日期控件（如起始日期、结束日期或单一日期选择框），为其所在列设置固定或最小宽度（例如通过 `ColumnDefinition Width="Auto"` + 控件内部 `MinWidth`，或使用星号布局结合 `MinWidth`）。
  - 根据现有 UI 布局，优先保持整体结构不变，仅在控件层级增加 `MinWidth` 或 `Width`，并适当为控件设置 `HorizontalAlignment="Left"` 以避免被压缩。

- **列表列宽可调整性：**
  - 在 `DataManagementDialogWindow.axaml` 的 `DataGrid` 定义中，确保 `CanUserResizeColumns="True"`（若未配置则显式设置），并为关键列提供合理的 `Width`/`MinWidth`（如 `Width="*"`, `MinWidth="80"` 或 `Width="Auto"` + `MinWidth`）。
  - 避免使用完全固定宽度（如统一 `Width="80"`）限制用户拖拽后的效果，改为以 `MinWidth` 控制最小可视范围。
  - 如项目已有统一 DataGrid 样式或行为（例如通过 `Style` 或 `AttachedProperty`），尽量复用而不是单独定义一套行为。

## Risks / Trade-offs

- **分页结构变更带来兼容性风险：**
  - 风险：若有其他前端页面或服务复用固废分页接口的旧结构，在未同步改造前可能出现运行时错误或数据展示异常。
  - 权衡：统一为 ABP 标准结构有利于后续维护与通用组件复用，短期需要检查并同步更新所有调用方。

- **日期控件宽度调整导致布局挤压：**
  - 风险：在低分辨率或窄窗口情况下，增加日期控件宽度可能挤压其他筛选项或按钮。
  - 权衡：通过合理设置 `MinWidth` 而非绝对宽度，并利用栅格布局在必要时让控件换行或滚动，减轻该问题。

- **列宽可调整可能破坏既定布局：**
  - 风险：用户拖拽列宽后，部分字段可能被压缩过小或导致水平滚动条过长。
  - 权衡：通过设置 `MinWidth` 限制过度压缩，同时接受一定的布局灵活性带来的变化，提升可用性。

- **ViewModel 技术栈与风格统一：**
  - 设计上要求与主界面 `AttendedWeighingViewModel` 保持一致，`DataManagementDialogWindow` 所使用的 ViewModel 采用 ReactiveUI 风格（例如继承统一的 `ViewModelBase` 或使用 `[Reactive]` 源生成属性），而不是在 Code-behind 中手写 `INotifyPropertyChanged`。
  - 建议将 `DataManagementDialogViewModel` 抽取到 `MaterialClient.ViewModels` 命名空间，使用 ReactiveUI 源生成属性管理分页字段（如 `CurrentPage`、`TotalCount`、`TotalPages` 等），并通过命令与对话框交互，从而在架构上与其他 ViewModel 保持一致性、便于测试与复用。

