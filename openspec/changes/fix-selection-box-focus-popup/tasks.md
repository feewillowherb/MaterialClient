## 1. 移除控件的自动聚焦能力

- [x] 1.1 在 `CreatablePageableSearchableSelectionBox` 构造函数中将 `Focusable = true` 改为 `Focusable = false`

## 2. 选择后焦点转移

- [x] 2.1 在 `AcceptSelection` 方法中，`IsPopupOpen = false` 之后，通过 `Dispatcher.UIThread.Post` 将焦点转移到 `TopLevel`（或父容器），确保焦点离开控件
- [x] 2.2 在 `OnAddNewButtonClick` 方法中，创建成功并关闭 Popup 后，同样转移焦点

## 3. 增强 _suppressNextOpen 保护机制

- [x] 3.1 将 `OnIsPopupOpenChanged(false)` 中 `_suppressNextOpen` 的重置从单次 `Dispatcher.UIThread.Post` 改为带约 200ms 延迟的 `DispatcherTimer.RunOnce`，确保焦点回落完成后才解除抑制

## 4. 修复 debounce 导致的 Popup 重开（方案 D，已实现但不充分）

- [x] 4.1 在 `OnIsPopupOpenChanged(false)` 中，`SyncTextBoxToDisplayText()` 之后再次调用 `_debounceCts?.Cancel()` 并置为 `null`（保留为防御层）
- [x] 4.2 在 `OnTextBoxTextChanged` 的 debounce 回调中，`if (!IsPopupOpen)` 条件增加 `&& !_suppressNextOpen` 检查（保留为防御层）

## 5. 修复 Popup 位置为左对齐

- [x] 5.1 在 `App.axaml` 控件模板中将 `Popup` 的 `Placement="Bottom"` 改为 `Placement="BottomEdgeAlignedLeft"`

## 6. 测试更新

- [x] 6.1 更新 `CreatablePageableSearchableSelectionBoxClosedStateTests` 中 `Control_ShouldBeFocusable` 测试为 `Control_ShouldNotBeFocusable`
- [x] 6.2 新增测试：选择不同项后 debounce 不会重开 Popup（模拟 TextChanged 后验证 IsPopupOpen 保持 false）

## 8. debounce 回调不得打开 Popup（方案 F，根本修复，替代方案 E）

- [x] 8.1 在 `OnTextBoxTextChanged` 的 debounce 回调中，将 `if (!IsPopupOpen && !_suppressNextOpen) IsPopupOpen = true;` 替换为 `if (!IsPopupOpen) return;`，使 Popup 关闭时回调直接跳过

## 9. 清理诊断日志

- [ ] 9.1 移除 `CreatablePageableSearchableSelectionBox.axaml.cs` 中所有 `DiagLog(...)` 调用和 `DiagLog` 方法，以及 `using System.Diagnostics;` 和 `using System.IO;`（调试用途已完成）
- [ ] 9.2 删除仓库根目录的 `popup-diag.log` 文件

## 10. MCP 验证（重新验证）

- [ ] 10.1 运行应用，通过 Avalonia DevTools MCP 验证：选择不同项后 `IsPopupOpen` 保持 `False`
- [ ] 10.2 通过诊断日志确认：选择后无 `OnTextBoxTextChanged: creating debounce` 记录（在清理诊断日志之前执行）
