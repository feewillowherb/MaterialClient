## 1. ViewModel 分页命令修复

- [x] 1.1 在 `DataManagementDialogViewModel` 中将 `PageChangeCommand` 从 `ReactiveCommand<int>` 调整为无参数的 `ICommand`，与 Ursa 分页用法保持一致（参考 `GenericSelectionPopupViewModel`）。
- [x] 1.2 确保分页命令在执行时基于当前 `CurrentPage` 和过滤条件调用 `LoadDataAsync` 或等效逻辑，重新加载 `Records`、`TotalCount` 和 `TotalPages`。

## 2. 分页状态与服务行为对齐

- [x] 2.1 在分页加载逻辑中保留并验证对 `TotalCount` 和 `TotalPages` 的计算与规范化，确保 `CurrentPage` 始终落在 `[1, TotalPages]` 区间内。
- [x] 2.2 验证在「查询」后第一页加载、点击上一页/下一页、跳转到指定页等场景下，Ursa 分页控件的当前页指示与页脚文案（现页数、总条数、总页数）保持一致。

