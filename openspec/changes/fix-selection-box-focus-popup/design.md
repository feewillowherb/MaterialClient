## Context

`CreatablePageableSearchableSelectionBox` 是一个自定义 Avalonia `TemplatedControl`，用于带分页搜索和新建功能的单选控件。该控件在 `SolidWasteModeFormView` 中用作供应商选择。

当前存在三个缺陷：

1. **页面加载焦点**：控件在构造函数中设置了 `Focusable = true`，导致页面加载时自动成为第一个焦点元素。
2. **选择不同项后弹窗重开**：用户在 Popup 中选择一个不同于当前选中项的条目后，Popup 关闭约 300ms 后又自动重新打开。选择相同项不触发此问题。
3. **Popup 位置居中**：Popup 模板使用 `Placement="Bottom"`，在 Avalonia 11 中此模式将 Popup 居中对齐到目标控件底部，而非左对齐。

### 根因分析（通过诊断日志 + Avalonia DevTools MCP 确认）

**弹窗重开的真正根因**：**Avalonia TextBox.TextChanged 事件是异步触发的**，导致 `_suppressTextChanged` 标志模式完全失效。

#### 诊断日志时序证据

```
17:18:02.554  AcceptSelection: Id=1, Name='大萨达撒'
17:18:02.554  AcceptSelection: setting IsPopupOpen=false
17:18:02.587  OnPopupClosed
17:18:02.589  OnIsPopupOpenChanged(False)
               └─ SyncTextBoxToDisplayText() 在 _suppressTextChanged=true 期间设置 Text
               └─ _suppressTextChanged=false 同步恢复
               └─ _debounceCts?.Cancel(); _debounceCts=null
17:18:02.589  AcceptSelection 返回, IsPopupOpen=False ✓

17:18:02.590  OnTextBoxTextChanged: suppressed=False ← TextChanged 异步触发!
               └─ 创建新 debounce (300ms)

17:18:02.789  _suppressNextOpen=false (200ms 计时器到期)

17:18:02.890  debounce callback: _suppressNextOpen=False
               └─ IsPopupOpen = true → Popup 重开 ✗
```

#### 根因机制

1. `SyncTextBoxToDisplayText()` 使用同步标志 `_suppressTextChanged = true` → `Text = ...` → `_suppressTextChanged = false`
2. Avalonia 的 `TextBox.TextChanged` 事件**不是在 `Text = ...` 赋值时同步触发**，而是在下一个调度帧异步分发
3. 当 `TextChanged` handler 运行时，`_suppressTextChanged` 已经恢复为 `false`，handler 不被拦截
4. Handler 创建新的 debounce CTS（绕过了之前在 `OnIsPopupOpenChanged(false)` 中的 cancel，因为那是取消旧 CTS）
5. 300ms debounce 到期时，`_suppressNextOpen`（200ms）已经过期 → Popup 被重开

选择相同项不触发是因为显示名称不变，TextBox.Text 不变，TextChanged 不触发。

**页面加载时同样存在此 Bug**：初始化时 `SyncTextBoxToDisplayText()` 设置 watermark 也触发异步 TextChanged → debounce → Popup 自动打开。

#### 为何方案 D（决策 4、5）不够

- **决策 4（cancel after sync）**：取消的是旧 CTS，新 CTS 在异步 TextChanged 中创建，在 cancel 之后
- **决策 5（suppress 检查）**：`SuppressOpenDelayMs(200ms) < DebounceMs(300ms)`，debounce 回调触发时 suppress 已过期

#### 根本设计缺陷

debounce 回调中包含 `if (!IsPopupOpen) IsPopupOpen = true;`，即"如果 Popup 关了就打开"。但 Popup 关闭时 TextBox 是 `IsReadOnly=true` + `Focusable=false`，用户**不可能**在 Popup 关闭时手动输入。因此这行代码唯一的触发来源是内部 Sync 操作泄漏的异步 TextChanged——它是 100% 的 bug 代码，0% 的正常用途。方案 E 尝试修补异步时序使 `_suppressTextChanged` 正确工作；方案 F 直接消除问题根源：**让 debounce 回调永远不打开 Popup**。

## Goals / Non-Goals

**Goals:**
- 页面加载时焦点不落在 `CreatablePageableSearchableSelectionBox` 上
- 选择条目后（无论是否重复）Popup 关闭且不重新打开
- 选择后焦点离开该控件
- Popup 弹出时左对齐到控件左边缘

**Non-Goals:**
- 不修改 `SearchableSelectionBox` 的焦点行为（该控件有独立的 GotFocus 处理）
- 不改变控件的核心 API（SelectedId、LoadPageAsync 等）

## Decisions

### 决策 1：移除控件的 `Focusable = true`（已实现）

**选择**：在构造函数中将 `Focusable` 改为 `false`。

**理由**：该控件不需要自身接收焦点。当 Popup 打开时，内部的 `PART_TextBox` 会被设为 `Focusable = true` 来接收键盘输入。控件级别的 `Focusable = true` 只会导致它成为 Tab 导航的目标，引起页面加载时的意外聚焦。

### 决策 2：选择后主动转移焦点（已实现）

**选择**：在 `AcceptSelection` 和 `OnAddNewButtonClick` 中，关闭 Popup 后通过 `ClearFocusFromControl()` 将焦点转移到 TopLevel。

**理由**：Avalonia 中设置 `Focusable = false` 不会清除已有焦点。MCP 诊断确认 `PART_TextBox` 在 `Focusable=false` 后仍保持 `IsFocused=True`。需要主动转移。

### 决策 3：增强 `_suppressNextOpen` 延迟（已实现）

**选择**：将 `_suppressNextOpen` 的重置从单次 `Post` 改为 `DispatcherTimer.RunOnce`（200ms）。

**理由**：增加时间窗口以覆盖焦点回落事件。但 MCP 诊断表明这不是重开的主因。

### 决策 4：关闭 Popup 后取消新产生的 debounce（方案 D-A，已实现但不充分）

**选择**：在 `OnIsPopupOpenChanged(false)` 中，在 `SyncTextBoxToDisplayText()` **之后**再次调用 `_debounceCts?.Cancel()`。

**不充分原因**：异步 TextChanged 在 cancel 之后才触发，创建的是全新 CTS，旧 cancel 无法覆盖。保留此代码作为防御层，但不是根本修复。

### 决策 5：debounce 回调中增加 suppress 检查（方案 D-B，已实现但不充分）

**选择**：在 debounce 回调中增加 `!_suppressNextOpen` 检查。

**不充分原因**：`SuppressOpenDelayMs(200ms) < DebounceMs(300ms)`，debounce 回调触发时 suppress 已过期。保留此检查作为防御层。

### 决策 7：debounce 回调不得打开 Popup（方案 F，根本修复，替代方案 E）

**选择**：将 debounce 回调中的 `if (!IsPopupOpen && !_suppressNextOpen) IsPopupOpen = true;` 替换为 `if (!IsPopupOpen) return;`。即 Popup 关闭时 debounce 回调直接跳过，不执行搜索也不打开 Popup。

**理由**：
1. Popup 关闭时 TextBox 是 `IsReadOnly=true`，用户无法手动输入，debounce 唯一可能的来源是内部 Sync 泄漏的异步 TextChanged
2. 即使 debounce 被意外创建，回调也不会造成任何副作用——直接 return
3. 无任何时序依赖，不依赖 `_suppressTextChanged`、`_suppressNextOpen` 或 `DispatcherTimer` 的正确性
4. 同时修复选择后 Popup 重开和页面加载时 Popup 自动打开两个 bug

**与方案 E 的对比**：方案 E 试图让 `_suppressTextChanged` 在异步 TextChanged 到达时仍为 `true`（通过 `Dispatcher.Post` 延迟重置）。这依赖 Avalonia 调度帧的顺序，是平台特定的时序假设。方案 F 在逻辑层面消除问题，与平台调度行为无关。

**对现有防御层的影响**：决策 4（cancel after sync）和决策 5（suppress 检查）可保留为防御层但不再是关键路径。`SuppressOpenDelayMs` 无需增加到 400ms，保持 200ms 即可。

### 决策 6：修复 Popup 位置为左对齐

**选择**：将控件模板中 Popup 的 `Placement="Bottom"` 改为 `Placement="BottomEdgeAlignedLeft"`。

**理由**：Avalonia 11 中 `PlacementMode.Bottom` 将 Popup 居中对齐到目标底部中心。`BottomEdgeAlignedLeft` 将 Popup 左边缘与目标左边缘对齐，这是选择控件的标准 UX。

## Risks / Trade-offs

- **[风险] 移除 Focusable 可能影响键盘导航** → 缓解：控件的交互入口是鼠标点击，Tab 键跳过此控件后到达下一个表单字段，体验反而更好。
- **[风险] debounce 回调跳过 Popup 关闭状态可能遗漏搜索** → 缓解：Popup 关闭时 TextBox 是 `IsReadOnly=true`，用户无法输入，因此 Popup 关闭时不存在需要执行的搜索。Popup 打开时 debounce 行为完全不变。
- **[风险] `BottomEdgeAlignedLeft` 在窗口边缘可能导致 Popup 超出屏幕** → 缓解：Avalonia 的 Popup 系统自带窗口边缘约束，会自动调整位置。
