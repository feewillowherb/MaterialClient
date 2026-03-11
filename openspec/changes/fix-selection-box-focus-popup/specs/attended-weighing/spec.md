## MODIFIED Requirements

### 需求：CreatablePageableSearchableSelectionBox 焦点与弹窗行为

CreatablePageableSearchableSelectionBox 控件在 Popup 关闭后不得自动获取焦点，不得因内部 debounce 路径重新打开 Popup，且页面加载时不得自动聚焦。

#### 场景：页面加载时不自动聚焦到控件
- **当** SolidWasteModeFormView 页面加载完成
- **则** CreatablePageableSearchableSelectionBox（供应商选择）不得自动获取焦点
- **且** Popup 不得自动打开

#### 场景：选择不同条目后 Popup 正常关闭
- **当** 用户在 Popup 中点击一个与当前 SelectedId 不同的条目
- **则** 系统须将 SelectedId 更新为新选中项
- **且** Popup 须关闭且不重新打开（包括不因 TextChanged debounce 重开）
- **且** 焦点须离开 CreatablePageableSearchableSelectionBox 控件及其子元素

#### 场景：选择相同条目后 Popup 正常关闭
- **当** 用户在 Popup 中点击与当前 SelectedId 相同的条目
- **则** SelectedId 不变
- **且** Popup 须关闭且不重新打开
- **且** 焦点须离开 CreatablePageableSearchableSelectionBox 控件及其子元素

#### 场景：通过点击控件打开 Popup
- **当** Popup 处于关闭状态
- **且** 用户点击 CreatablePageableSearchableSelectionBox 控件区域
- **则** Popup 须打开
- **且** 内部 TextBox 须获取焦点以接收搜索输入

#### 场景：新增条目后 Popup 正常关闭
- **当** 用户通过 Popup 中的"新增"按钮成功创建新条目
- **则** SelectedId 须更新为新创建条目的 ID
- **且** Popup 须关闭且不重新打开
- **且** 焦点须离开 CreatablePageableSearchableSelectionBox 控件及其子元素

#### 场景：Popup 关闭时 debounce 须被取消
- **当** Popup 从打开变为关闭（无论是选择、新增、Escape 还是 light-dismiss）
- **则** 系统须取消所有待执行的搜索 debounce 回调

#### 场景：debounce 回调不得打开 Popup
- **当** debounce 回调触发时 Popup 处于关闭状态
- **则** 回调须直接跳过，不得执行搜索也不得将 `IsPopupOpen` 设为 `true`
- **理由** Popup 关闭时 TextBox 为 `IsReadOnly=true`，不存在用户输入的搜索，debounce 来源只可能是内部 Sync 泄漏的异步 TextChanged

### 需求：CreatablePageableSearchableSelectionBox Popup 位置

Popup 弹出时须左对齐到控件左边缘。

#### 场景：Popup 左对齐
- **当** 用户点击 CreatablePageableSearchableSelectionBox 打开 Popup
- **则** Popup 的左边缘须与控件的左边缘对齐
- **且** Popup 不得居中于控件下方
