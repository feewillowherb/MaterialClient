# GenericSelectionPopup「新增」二次确认（可编辑 Name）设计方案

**日期**：2026-03-18  
**状态**：Draft  
**影响范围**：`GenericSelectionPopup` / `GenericSelectionPopupViewModel<T>` /（可选）新增 Dialog/Window  

---

## 1. 背景与现状

当前 `GenericSelectionPopup` 的“新增”按钮直接绑定 `AddNewItemCommand`，由 `GenericSelectionPopupViewModel<T>.AddNewItemAsync()` 取 `SearchText.Trim()` 作为 name，随后调用外部注入的 `_createNewItemFunc(name)` 创建新条目并插入列表、选中。

关键现状点：

- “新增”入口在 `GenericSelectionPopup.axaml`（无结果居中态 + 列表下方按钮），都绑定 `AddNewItemCommand`。
- “新增”逻辑在 `GenericSelectionPopupViewModel<T>`，目前没有二次确认、也无法在新增前再次编辑 name。

---

## 2. 目标与非目标

### 2.1 目标

- 点击“新增”后，弹出**确认界面**。
- 确认界面中包含一个可编辑 `TextBox`，默认填入当前搜索词 `SearchText.Trim()`。
- 提供“确认/取消”两按钮：
  - **取消**：不创建，不改变当前列表/选择。
  - **确认**：使用用户最终输入的 name 创建并选中新项。
- 尽量保持 `GenericSelectionPopupViewModel<T>` 的可复用性（不同实体 T 的创建仍由调用方提供 `_createNewItemFunc`）。

### 2.2 非目标

- 不处理“重名策略/服务端校验提示”的统一规范（由各 `_createNewItemFunc` 内部决定返回 null / 抛异常 / toast）。
- 不在本变更中重构 `SearchableSelectionBox` 或替换为新组件（仅增强“新增”路径）。

---

## 3. 方案 A：新增确认 Dialog Window（推荐，通用性最好）

### 3.1 核心思路

保持 `GenericSelectionPopupViewModel<T>` 不直接依赖 UI 框架的窗口创建细节，通过新增一个**可选的确认委托**把“弹窗与 name 编辑”交给上层（View/VM 组合层）实现。

新增委托（建议签名）：

```csharp
// 输入：建议的 name（来自 SearchText.Trim()）
// 输出：用户确认后的 name；返回 null 表示取消
Func<string, Task<string?>>? confirmNewNameFunc
```

`AddNewItemAsync()` 流程改为：

1. `proposedName = SearchText.Trim()`，为空直接 return（保持原逻辑）。
2. 若 `confirmNewNameFunc != null`：
   - `finalName = await confirmNewNameFunc(proposedName)`
   - 若 `finalName == null` → 用户取消，return
   - `finalName = finalName.Trim()`，为空则 return（可视为输入无效）
3. `newItem = await _createNewItemFunc(finalName)`（仍由调用方提供）
4. 成功后沿用当前插入列表并选中的逻辑。

### 3.2 可复用 Dialog Window 设计（避免 Window 分散）

为避免“每个小交互都新建一个 Window”导致项目中出现大量分散的窗口类，建议为方案 A 引入**可复用对话框**的组织方式：

- **通用宿主**：`CommonDialogWindow : Window`
  - 统一样式（标题栏/边框/按钮区/大小/居中 owner）
  - 内容通过 `Content` 或模板承载（推荐承载 `UserControl` 内容）
- **内容视图**：`ConfirmTextView : UserControl`（或更简单的 `UserControl + ViewModel`）
  - 仅包含提示文本、`TextBox`、确认/取消按钮
  - 可扩展：输入校验、提示信息、最大长度等
- **对外入口（建议集中）**：`IDialogService`
  - 业务层/组合层只依赖接口，不直接 `new Window()`
  - 示例：`Task<string?> ConfirmTextAsync(Window owner, string title, string message, string initialValue)`
    - 返回 `null` 表示取消
    - 返回非空字符串表示确认后的输入

该设计下，“确认新增 name”只是 `ConfirmTextAsync(...)` 的一个用例，后续类似的“确认删除/确认覆盖/输入备注”等也可复用同一宿主和服务入口，从而避免 Window 类膨胀。

### 3.3 UI 形态（示例）

使用上面的通用对话框后，UI 可以落为“通用宿主 + 确认文本内容视图”，而不是为本功能单独新增专用 Window：

- 标题：确认新增
- 提示：请确认名称
- TextBox：TwoWay 绑定到 `Name`
- Buttons：取消 / 确认

实现风格可沿用项目现有 dialog 模式（例如 `ExportFilterDialog`：VM 提供 `ConfirmCommand/CancelCommand`，Window code-behind Subscribe 后 Close），只是把“宿主 Window”做成可复用的 `CommonDialogWindow`。

### 3.4 改动面（文件建议）

- `MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs`
  - 增加字段：`Func<string, Task<string?>>? _confirmNewNameFunc;`
  - 构造函数新增可选参数：`confirmNewNameFunc`
  - `AddNewItemAsync()` 在调用 `_createNewItemFunc` 前先确认并获得最终 name
- 新增可复用对话框基础设施（建议放 `MaterialClient/Views/Dialogs/` 或 `MaterialClient/Views/Common/Dialogs/`）
  - `CommonDialogWindow.axaml` / `.axaml.cs`（宿主 Window，可复用）
  - `ConfirmTextView.axaml` / `.axaml.cs`（或 `.axaml` + VM）
  - `IDialogService` + 实现（建议放 `MaterialClient/Services/` 或现有服务目录）
- 调用方（创建 `GenericSelectionPopupViewModel<T>` 的业务 VM）：
  - 传入 `confirmNewNameFunc`，内部通过 `IDialogService.ConfirmTextAsync(ownerWindow, ...)` 实现
  - ownerWindow 获取建议由调用方持有/注入（或从当前视图的 `TopLevel` 获取后传入服务）

### 3.5 优点与缺点

- **优点**
  - UI 与 VM 解耦：`GenericSelectionPopupViewModel<T>` 继续通用可复用
  - 可统一复用确认窗口：供应商/物料/街道等全部走同一个 Dialog
  - 更易做校验/错误提示（比如空字符串、非法字符）并可扩展为“高级字段”
- **缺点**
  - 相比“专用小窗口”，需要先落地通用对话框/服务入口（一次性基础设施投入）
  - 需要解决 owner window 获取（通常可从当前 `TopLevel`/`Window` 获取，或由调用方手头已有 Owner）

### 3.6 任务拆分（建议）

- [ ] 在 `GenericSelectionPopupViewModel<T>` 增加 `confirmNewNameFunc` 注入点，并调整 `AddNewItemAsync()` 流程
- [ ] 新增 `CommonDialogWindow` + `ConfirmTextView`（或等价的通用对话框实现）
- [ ] 新增 `IDialogService`（或等价服务）并提供 `ConfirmTextAsync(...)`
- [ ] 在至少一个实际使用点（例如供应商/物料）接入委托并完成联调
- [ ] 为取消/确认/空输入/异常创建失败返回 null 的路径补齐 UX（比如不关闭弹窗、或提示）

---

## 4. 方案 B：在现有 Popup 内嵌“确认面板”（不新增窗口，改动局部 UI）

### 4.1 核心思路

不弹新 Window，而是在 `GenericSelectionPopup` 内部叠加一个“确认面板”（Overlay）：

- 点击“新增”后：切到确认态（隐藏 DataGrid/分页，显示确认面板）
- 确认面板含 TextBox（默认 `SearchText.Trim()`）+ 确认/取消按钮
- 取消：退出确认态，回到列表/无结果态
- 确认：用编辑后的 name 走创建逻辑，然后退出确认态并选中新项

### 4.2 需要的 VM 增强

`IGenericSelectionPopupBindings` / `GenericSelectionPopupViewModel<T>` 增加用于 UI 状态的属性与命令，例如：

- `bool IsConfirmAddOpen`
- `string ConfirmAddName`
- `ICommand OpenConfirmAddCommand`（替代原按钮直接绑 `AddNewItemCommand`）
- `ICommand ConfirmAddCommand`（内部调用创建逻辑）
- `ICommand CancelConfirmAddCommand`

为了最小化影响，也可以保持 `AddNewItemCommand` 为“打开确认面板”，真正创建逻辑挪到 `ConfirmAddCommand`。

### 4.3 XAML 初稿（概念）

在 `GenericSelectionPopup.axaml` 的根 `Grid` 里加一层 overlay：

- overlay 可见性绑定 `IsConfirmAddOpen`
- overlay 内部放 `Border + StackPanel(TextBox + Buttons)`

并把原“新增”按钮命令从 `AddNewItemCommand` 改到 `OpenConfirmAddCommand`（或继续用同名命令但语义变更）。

### 4.4 优点与缺点

- **优点**
  - 不新增 Window，交互更“原地”，实现更轻量（看起来像在同一个弹层里继续操作）
  - 不需要处理 owner window
- **缺点**
  - `GenericSelectionPopupViewModel<T>` 不再纯粹“数据 + 命令”，会引入更多 UI 状态
  - 现有 `IGenericSelectionPopupBindings` 接口需要扩展（所有使用方需适配编译）
  - 更难在全局复用“确认新增”能力到其他地方（因为它被绑死在这个 Popup 里）

### 4.5 任务拆分（建议）

- [ ] 扩展 `IGenericSelectionPopupBindings` 增加确认态相关属性/命令
- [ ] 扩展 `GenericSelectionPopupViewModel<T>` 实现确认态逻辑与最终创建
- [ ] 更新 `GenericSelectionPopup.axaml`：新增 overlay 面板 + 绑定调整
- [ ] 回归测试：选择、翻页、无结果新增、已有结果时新增按钮、取消关闭后的搜索/显示是否一致

---

## 5. 推荐结论

优先推荐 **方案 A（新增确认 Dialog Window + confirmNewNameFunc 委托）**：

- 复用性最好、对现有 `GenericSelectionPopup` UI 改动最小
- 不会强行把“确认态”耦合进 `GenericSelectionPopupViewModel<T>` 的状态机
- 更符合当前你们已有的“Window + VM + Subscribe Close”的实现习惯（参考 `ExportFilterDialog`）

若你明确希望“确认界面也必须出现在当前 Popup 内、像一个二级面板”，则选 **方案 B**。

---

## 6. 验收点（两方案通用）

- 点击“新增”必定出现确认界面，且 name 可编辑
- 取消不创建、不改变当前选择
- 确认后创建成功：新项出现在列表中并被选中（保持现有 insert + select 的体验）
- 输入空白或全空格：不创建（可提示或静默）
- 创建失败（`_createNewItemFunc` 返回 null 或抛异常）：不选中、不插入，且不崩溃
