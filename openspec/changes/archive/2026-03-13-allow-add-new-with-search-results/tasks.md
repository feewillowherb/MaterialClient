## 1. ViewModel 状态与刷新

- [x] 1.1 在 `GenericSelectionPopupViewModel` 中新增私有状态：表示“当前搜索文本是否已在当前页结果中存在”（如 `_searchTextExistsInCurrentPage`），并在 `SetItemsAsync` 或数据加载完成后根据 `SearchText.Trim()` 与 `PagedItems` 中各条 `DisplayText` 做忽略大小写的完全匹配并更新该状态
- [x] 1.2 将 `ShowAddNewButton` 改为 `_allowAddNew && !string.IsNullOrWhiteSpace(SearchText) && !_searchTextExistsInCurrentPage`，并在所有会更新 `PagedItems` 或 `SearchText` 的路径上（如 `LoadDataAsync` 末尾、`SetItemsAsync` 内、以及 SearchText 触发刷新后的完成处）更新该状态并调用 `RaisePropertyChanged(nameof(ShowAddNewButton))`

## 2. 行为验证

- [x] 2.1 验证 ClientSide 模式：有过滤结果且输入与某条 DisplayText 完全一致时不显示「新增」；与任一条都不一致时显示「新增」
- [x] 2.2 验证 ServerSide 模式：当前页有结果时，按当前页是否存在与输入完全一致的 DisplayText 决定是否显示「新增」

## 3. UI 文案与布局

- [x] 3.1 仅在无结果时显示「未找到匹配结果」文案：有结果且显示「新增」时只显示按钮、不显示该文案，避免“有搜索结果却出现未找到匹配结果”的矛盾
- [x] 3.2 布局方案 A：有结果时「新增」置于列表下方独立一行，不遮挡列表内容（主 Grid 三行：列表区 / 新增按钮行 / 分页）
