## Why

当前供应商/材料/镇街等选择使用「SearchableSelectionBox + 父视图 Popup + GenericSelectionPopup」拼装，结构分散、复用与维护成本高。需要统一为**单一可创建、可分页、可搜索的选择控件**，在保持与现有 SearchableSelectionBox 关闭状态一致外观的前提下，内聚搜索、分页与“新增”能力，并与 ViewModel 通过标准属性/委托对接，不依赖具体 VM 类型。

## What Changes

- 新增一个 **TemplatedControl**：内嵌 TextBox（唯一输入/展示面）+ Popup（列表、分页、可选“新增”区），通过 `LoadPageAsync`、`SelectedItem`、`DisplayMemberPath`、`GetItemId`、`Watermark`、`PageSize` 等与调用方对接。
- 引入 **SelectionItem** 模型（Id + Name）及与 Provider/Material/Street 等的 **FromX / ToX 扩展方法**，UI 层仅依赖 SelectionItem，业务实体通过扩展方法互转。
- 数据契约：分页加载签约为 `(searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<T>>`；服务层在 `selectedIds` 中任一项的 Name 与 `searchText` 完全一致时忽略 `searchText` 过滤。
- 服务层分页接口（如 GetPagedProvidersAsync）增加可选参数 `IReadOnlyList<int>? selectedIds` 及上述“已选项不误过滤”逻辑。
- 新界面/重构界面优先使用新控件，渐进替换现有 SearchableSelectionBox + GenericSelectionPopup 组合；不强制一次性替换所有老界面。

## Capabilities

### New Capabilities

- `creatable-pageable-searchable-selection`: 可创建、可分页、可搜索的单一选择控件：交互（点击打开、输入搜索、防抖、分页、选择/新增后关闭、Escape/点击外部重置）、与数据源契约（LoadPageAsync、selectedIds）、SelectionItem 与扩展方法、关闭状态下与 SearchableSelectionBox 一致的视觉与样式。

### Modified Capabilities

- （无：仅新增能力与可选服务层参数，不改变现有 spec 级需求。）

## Impact

- **MaterialClient.UI**：新增控件及默认模板、SelectionItem 与扩展方法；本次变更仅在 `SolidWasteModeFormView` 中替换供应商选择控件（由 SearchableSelectionBox + GenericSelectionPopup 组合改为新控件），是否在其它界面推广另行通过后续变更评估。
- **服务/应用层**：分页接口增加 `selectedIds` 参数及“已选项名称与 searchText 一致时忽略过滤”的逻辑。
- **测试**：需覆盖点击打开（有/无选中）、输入后不选择即关闭、选择一项、无结果时“新增”等场景。
