# 变更提案：有搜索结果时仍可新增

## Why

当前使用 `SearchableSelectionBox` / `GenericSelectionPopupViewModel` 时，一旦关键字搜索产生结果（`TotalCount > 0`），「新增」按钮会被隐藏。用户输入的是前缀或模糊匹配到的并非完全相同的文本时，无法将当前输入作为新项添加，只能从现有结果中选择，导致无法新增与搜索结果“相近但不同”的项。

希望在**有搜索结果时也允许新增**，仅当当前输入与某条结果的显示文本**完全一致**时才不显示「新增」，避免重复添加。

## What Changes

- 调整「新增」按钮的显示逻辑：由“仅当无搜索结果时显示”改为“只要当前输入非空且不在当前结果中完全匹配则显示”。
- 匹配规则：当前搜索文本（Trim 后）与结果列表中任一项的显示文本（DisplayText）**完全一致**（建议忽略大小写）时，视为已存在，不显示「新增」；否则显示「新增」。
- 客户端过滤（ClientSide）与服务端分页（ServerSide）两种模式均需支持上述逻辑；服务端模式下“当前结果”指当前页的 `PagedItems`。

## Capabilities

### New Capabilities

- `generic-selection-popup`: 可复用的通用选择弹窗（搜索、分页、可选新增）。规范其行为：搜索有结果时仍可根据“当前输入是否已存在于当前结果”决定是否显示并允许执行「新增」。

### Modified Capabilities

- （无：本次不修改既有 spec 的需求表述，仅新增能力规范。）

## Impact

- **受影响代码**：`MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs`（`ShowAddNewButton` 计算逻辑，以及为支持“当前输入是否已存在”所需的派生状态或查询）。
- **受影响 UI**：`MaterialClient/Views/Controls/GenericSelectionPopup.axaml` 仅通过绑定 `ShowAddNewButton` 显示「新增」区域，无需改 XAML，除非后续需展示“已存在”提示。
- **API/依赖**：无新增对外 API；`IGenericSelectionPopupBindings.ShowAddNewButton` 的语义从“无结果且可新增时显示”变为“可新增且当前输入不在当前结果中完全匹配时显示”。
