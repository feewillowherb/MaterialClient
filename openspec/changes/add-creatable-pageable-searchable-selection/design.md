## Context

- 当前供应商/材料/镇街等选择由 **SearchableSelectionBox**（展示 + 触发）+ 父视图中的 **Popup** + **GenericSelectionPopupView** 拼装而成，Popup 与 Placement 在父视图声明，数据与 VM 通过 `IGenericSelectionPopupBindings` 等耦合。
- 需求：单一控件内聚「可搜索、可分页、可创建」能力，关闭状态下与现有 SearchableSelectionBox 视觉一致（高度 32、白底、边框 #E5E7EB、字体 12、前景 #333333、右侧下拉箭头等），Popup 打开时与 GenericSelectionPopup 视觉一致（DataGrid + Ursa Pagination），与 ViewModel 通过标准属性/委托对接，不依赖具体 VM 类型。
- 约束：不强制一次性替换所有老界面；服务层仅在分页接口上增加 `selectedIds` 及"已选项名称与 searchText 一致时忽略过滤"的逻辑，不改变核心契约。

## Goals / Non-Goals

**Goals:**

- 实现一个 TemplatedControl，内嵌 PART_TextBox + PART_Popup（DataGrid + Ursa Pagination + 可选"新增"），通过 LoadPageAsync、SelectedItem、Watermark、PageSize、CurrentPage、TotalCount 等与调用方对接。Popup 打开时的视觉结构与 GenericSelectionPopup 完全一致。
- 统一 SelectedItem 为 SelectionItem（Id + Name），业务实体通过 FromX/ToX 扩展方法与 SelectionItem 互转，UI 层不直接依赖领域实体。
- 关闭状态下与 SearchableSelectionBox 外观一致，复用或轻量调整现有 Hover/Focus/Error 等样式。
- Popup 打开时与 GenericSelectionPopup 视觉一致：Width=400、Height=250、BorderThickness=3、DataGrid 单列"名称"（白底黑字列头）、分页信息文本 + Ursa Pagination 组件。
- 支持点击打开、输入防抖搜索、分页、选择/新增后关闭、Escape/点击外部重置等交互；数据契约为 (searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<T>>。

**Non-Goals:**

- 一次性替换所有使用 SearchableSelectionBox + GenericSelectionPopup 的界面；旧组合暂不删除，先标记 deprecated 再后续清理。
- 修改现有服务层核心契约；仅增加可选参数与过滤逻辑。

## Decisions

- **单一控件 vs 继续拼装**：采用单一 TemplatedControl，Popup 在控件模板内、PlacementTarget 为控件自身，宽度与触发器对齐。理由：减少父视图 XAML、避免每个使用点重复声明 Popup/Placement，行为与样式集中在一处。
- **Popup 内部使用 DataGrid + Ursa Pagination（与 GenericSelectionPopup 一致）**：Popup 内容采用 DataGrid（单列"名称"、水平网格线、RowHeight=30、白底黑字列头）+ Ursa `Pagination` 组件 + 分页信息文本（"当前页:X  共N条记录"），整体布局与现有 GenericSelectionPopup 完全一致（Width=400、Height=250、BorderThickness=3、CornerRadius=4）。理由：供应商与材料等选择弹窗需保持统一视觉，避免同一界面中出现两种不同风格的选择弹窗；使用 DataGrid 和 Ursa Pagination 复用已有样式和组件库，降低维护成本。控件通过 `CurrentPage`、`TotalCount`、`ShowResults`、`CurrentPageInfo`、`TotalCountInfo` 等 StyledProperty 与模板中的 Pagination 组件实现 TwoWay 绑定，内部使用 `_suppressPageChangeLoad` 标志避免翻页与编程设值的双重加载。局部样式 `<Border.Styles>` 覆盖全局 DataGridColumnHeader 蓝底白字为白底黑字，与 GenericSelectionPopup 的本地样式覆盖一致。
- **SelectionItem 与扩展方法**：UI 与调用方只交换 SelectionItem；Provider/Material/Street 等通过 ToSelectionItem() / ToProviderId() 等扩展方法互转。理由：控件不依赖具体领域类型，便于在不同实体上复用同一控件；ViewModel 仅需少量胶水代码。
- **加载契约 selectedIds**：分页加载接受 selectedIds，当其中任一项的 Name 与 searchText 完全一致时，服务层忽略 searchText 过滤。理由：点击打开时 TextBox 显示已选项名称并作为 searchText 传入，若不忽略会导致"用已选项名搜索"结果为空。
- **关闭时强制重置**：Escape 或点击外部关闭时，_searchText 与 TextBox 显示恢复为当前已选项显示文本（无则空），不保留用户输入。理由：与"允许不选择即关闭"的预期一致，避免未提交的输入残留在 UI 上。

## Risks / Trade-offs

- **迁移期双轨**：新旧两种用法并存时，需在文档与代码注释中明确"新界面优先用新控件"，避免继续堆积旧组合。缓解：在 GenericSelectionPopup 相关类型上标记 Obsolete/Deprecated，并在 proposal 与 tasks 中写明清迁节奏。
- **服务层契约扩展**：分页接口增加 selectedIds 可能影响现有调用方。缓解：参数设计为可选（如 IReadOnlyList<int>? selectedIds），现有调用不传即可；忽略 searchText 的逻辑仅在"任一项 Name 与 searchText 完全一致"时触发，行为可测。
- **样式一致性**：新控件关闭态需与 SearchableSelectionBox 像素级一致，Popup 打开态需与 GenericSelectionPopup 布局一致。缓解：关闭态复用同一组样式资源（Border、字体、颜色、箭头）；Popup 内使用相同的 DataGrid + Pagination 结构，局部样式覆盖 DataGridColumnHeader 保证列头白底黑字。

## Migration Plan

1. **实现阶段**：在 MaterialClient.UI 中新增控件及默认模板（DataGrid + Ursa Pagination）、SelectionItem 与扩展方法；服务层分页接口增加 selectedIds 与过滤逻辑；为新控件编写单元/集成测试。
2. **首个接入点**：在 `SolidWasteModeFormView` 中，仅将供应商选择由现有 SearchableSelectionBox + GenericSelectionPopup 组合替换为新控件，验证端到端行为与样式。
3. **进一步推广（后续变更处理）**：是否在更多界面中替换其它选择控件，不在本次变更范围内；如需推广，将通过新的变更与任务单独设计与评审。
4. **回滚**：若新控件在 `SolidWasteModeFormView` 中出现严重问题，可回退到仍使用旧组合的版本；新控件为增量替换，不删除旧代码直至后续废弃计划执行。

## Open Questions

- 无；提案与需求已明确，实现可按 tasks 推进。
