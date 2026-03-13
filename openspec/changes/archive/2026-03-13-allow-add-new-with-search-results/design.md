# 设计：有搜索结果时仍可新增

## Context

- **现状**：`GenericSelectionPopupViewModel.ShowAddNewButton` 当前为 `_allowAddNew && TotalCount == 0 && !string.IsNullOrWhiteSpace(SearchText)`，即仅在“无任何搜索结果”时显示「新增」。一旦搜索产生结果（含模糊/前缀匹配），按钮隐藏，用户无法把当前输入作为新项添加。
- **约束**：需同时支持 ClientSide（内存过滤）与 ServerSide（服务端分页）；不改变现有 `IGenericSelectionPopupBindings` 的接口形状，仅改变 `ShowAddNewButton` 的语义与计算方式。
- **相关方**：使用该弹窗的表单（如物料、供应商等可创建新项的选择框）。

## Goals / Non-Goals

**Goals:**

- 有搜索结果时，若当前搜索文本（Trim 后）与当前结果集中任一项的显示文本不完全一致，则显示「新增」并允许执行新增。
- 仅当当前输入与某条结果的 DisplayText 完全一致（建议忽略大小写）时，不显示「新增」，避免重复添加。
- ClientSide 与 ServerSide 两种模式均按上述规则工作；ServerSide 下“当前结果”指当前页的 `PagedItems`。

**Non-Goals:**

- 不改变搜索/过滤算法（如不引入新的模糊匹配库）。
- 不增加“已存在”的单独提示 UI（仅通过不显示「新增」表达）。

## Decisions

### 1. “当前结果”与“是否已存在”的数据源

- **选择**：以当前页的 `PagedItems` 为准，判断是否存在 DisplayText 与 SearchText.Trim() 完全一致的项。
- **理由**：ClientSide 下 PagedItems 即为当前过滤后的当前页；ServerSide 下我们只有当前页数据，无法在不额外请求的前提下判断全库是否包含该显示名，用当前页一致即可避免本页重复，且实现简单。
- **备选**：ClientSide 下用“全部过滤结果”判断——更严格，但需在 VM 内保留过滤后列表的引用或再算一遍，增加状态与复杂度；本次不采用。

### 2. 完全一致与大小写

- **选择**：使用 `StringComparison.OrdinalIgnoreCase` 比较 SearchText.Trim() 与各条 DisplayText，相等则视为已存在。
- **理由**：与现有 `FilterClientSide` 的 `OrdinalIgnoreCase` 一致，避免“已存在”因大小写不同而仍显示新增。
- **备选**：区分大小写——可能造成“ABC”与“abc”重复；不采用。

### 3. ShowAddNewButton 的实现方式

- **选择**：引入私有派生状态“当前输入是否已存在于当前页结果”（如 `SearchTextExistsInCurrentPage`），在 `LoadDataAsync` / `SetItemsAsync` 完成后根据当前 `SearchText` 与 `PagedItems` 计算并缓存；`ShowAddNewButton` 改为 `_allowAddNew && !string.IsNullOrWhiteSpace(SearchText) && !SearchTextExistsInCurrentPage`，并在 SearchText 或 PagedItems 变化时更新并触发 `RaisePropertyChanged(nameof(ShowAddNewButton))`。
- **理由**：逻辑集中、可测；无需改动 XAML，仅 ViewModel 行为变化。
- **备选**：在 `ShowAddNewButton` 的 getter 内每次实时遍历 PagedItems——简单但可能在高频刷新时重复计算；若后续有性能顾虑可再改为缓存。

### 4. 何时刷新“已存在”状态

- **选择**：在现有会更新 `PagedItems` 或 `SearchText` 的路径上刷新（如 `LoadDataAsync` 末尾、`SetItemsAsync` 内、以及 `SearchText` 的 WhenAnyValue 触发 RefreshAsync 后的完成处），并统一调用一个“更新 ShowAddNewButton 相关属性”的方法（内部设置 `SearchTextExistsInCurrentPage` 并 `RaisePropertyChanged(ShowAddNewButton)`）。
- **理由**：保证用户输入或翻页后，按钮显示与当前页数据一致。

### 5. 有结果时「新增」按钮布局（不遮挡列表）

- **选择**：采用**独立一行**（方案 A）。主布局为三行：Row 0 = 列表区域（仅 DataGrid 或仅空状态+新增）、Row 1 = 仅当「有结果且可新增」时显示的「新增」按钮行（Auto 高度）、Row 2 = 分页。无结果时 Row 0 显示「未找到匹配结果」+ [新增] 居中；有结果且可新增时 Row 0 仅显示 DataGrid，Row 1 显示 [新增]，列表与按钮分区、不重叠。
- **理由**：有结果时原布局中 DataGrid 与 ShowAddNewButton 的 StackPanel 同处一格且按钮居中，会压在列表上遮挡内容；将「有结果时的新增」单独占一行可从结构上避免遮挡。
- **备选**：与分页同一行放按钮——不增行数但分页行变挤；按钮贴列表底部——改动小但行数多时仍可能挡最后一行；不采用。

## Risks / Trade-offs

- **[ServerSide 仅看当前页]** 服务端模式下，若同名项不在当前页，仍会显示「新增」，可能产生跨页重复。**缓解**：接受为已知限制；若业务强需求“全库唯一显示名”，可在服务端或后续迭代中在创建前做唯一性校验。
- **[重复计算]** 若在 getter 内实时计算是否存在，在快速输入时可能多次遍历。**缓解**：采用“在数据/搜索变更完成后更新一次”的缓存方式，避免 getter 内遍历。

## Migration Plan

- ViewModel 逻辑 + GenericSelectionPopup.axaml 布局调整（三行布局、有结果时新增按钮独立一行）。无数据迁移、无部署步骤。发布后即生效；若需回滚，还原相关提交即可。

## Open Questions

- 无。若后续业务要求“全库唯一显示名”校验，可在创建新项的服务层或 API 中增加校验，不在此设计范围内。
