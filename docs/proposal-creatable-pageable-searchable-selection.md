# 可创建、可分页、可搜索的选择组件实现提案

**日期**: 2026-02-27  
**状态**: 待评审

---

## 一、说明与范围

- **目标**：实现一个**可创建（Creatable）、可分页（Pageable）、可搜索（Searchable）**的单一选择组件，用于替代当前「SearchableSelectionBox + 父视图中的 Popup + GenericSelectionPopup」的拼装方式。
- **不引用**：本提案**不**基于、不引用现有 `PageableAutoCompleteBox` 控件；该控件的实现将回滚，本提案从需求与设计重新出发。
- **参考文档**：
  - [SearchableSelectionBox 与 AutoCompleteBox 对照分析](analysis-searchable-selection-vs-autocompletebox.md)：现有拼装方式的不足与改进方向。
  - [Popup 选择弹窗问题分析](popup-selection-analysis.md)：弹窗打开/关闭与选中恢复的时序问题。

---

## 二、需求整理

### 2.1 核心能力

| 能力 | 说明 |
|------|------|
| **可搜索（Searchable）** | 用户可在输入框中输入文本，作为搜索条件；支持防抖（如 300ms）后请求服务端/数据源。 |
| **可分页（Pageable）** | 下拉列表支持分页（服务端或客户端分页），具备分页条或“加载更多”；数据通过 `searchText + page + pageSize + selectedIds` 加载。 |
| **可创建（Creatable）** | 当无匹配结果时，可展示“未找到匹配结果”并提供“新增”入口（如按钮或命令），与现有 GenericSelectionPopup 的“新增”行为一致。 |

### 2.2 交互与行为

1. **单一输入面**：与 AutoCompleteBox 类似，**同一 TextBox** 既用于展示选中项文本，也用于输入搜索；避免“关闭时 TextBlock、打开时 TextBox”的两套 UI。
2. **点击即打开**：点击控件（或 TextBox）时立即打开下拉 popup，默认加载 `selectedIds + page 1 + pageSize`，搜索文本为当前选中项的显示文本（若有选中项）。
3. **输入即搜索**：在 TextBox 中输入时，防抖后以新搜索文本、第 1 页请求数据，并保持 popup 打开。
4. **允许不选择即关闭**：支持通过 **Escape** 或**点击外部**关闭 popup；关闭时**强制重置**，不保留用户输入：
   - `_searchText` 与 TextBox 显示恢复为当前**已选项**的显示文本（无选中则为空）。
5. **选择后关闭**：用户从列表选择一项或执行“新增”后，更新 `SelectedItem`，关闭 popup，焦点回到 TextBox。
6. **键盘支持**：在 popup 内 Arrow Up/Down 移动高亮，Enter 确认当前项，Escape 关闭并重置。

### 2.3 与数据源的契约

- **加载接口**：`Func<string? searchText, int page, int pageSize, IReadOnlyList<int>? selectedIds, CancellationToken, Task<PagedResultDto<T>>>`（或等价），由调用方提供。
- **已选项不误过滤**：当 `selectedIds` 对应项的**显示名称**与 `searchText` **完全一致**时，服务层应**忽略** `searchText` 过滤条件，避免“已选项名称”被当成搜索词导致结果为空。
  - `selectedIds` 通常数量为一个；可用 `Any()` 判断任一 selectedId 的 Name 与 `searchText` 完全一致即忽略过滤。
- **选中与展示**：控件暴露 `SelectedItem`、`DisplayMemberPath`（或 ValueMemberBinding）、可选 `GetItemId`（用于组成 `selectedIds`）。

---

## 三、组件设计概要

### 3.1 定位

- **单一控件**：一个 Control，内嵌「TextBox + Popup（列表 + 分页区 + 可选“新增”区）」。
- **不依赖具体 VM 类型**：通过 `LoadPageAsync`、`SelectedItem`、`DisplayMemberPath`、`GetItemId`、`Watermark`、`PageSize` 等标准属性与委托对接，可与现有 `GenericSelectionPopupViewModel` 或其它数据源适配，但不直接依赖 `IGenericSelectionPopupBindings`。

### 3.2 建议结构（Template）

```
[控件名] (TemplatedControl)
├── PART_TextBox (TextBox)          // 唯一输入/展示面
└── PART_Popup (Popup)
    └── Border（宽度与触发器对齐，MaxHeight 限制）
        ├── PART_ItemsList (ListBox 或 DataGrid)   // 当前页列表，键盘上下/Enter
        ├── 分页区 (PART_Pager)                     // 页码或“加载更多”
        └── （可选）空状态 + “新增”按钮/命令
```

- Popup 的 `PlacementTarget` 为控件自身；宽度与触发器对齐（如 MinWidth 绑定 TemplatedParent 的 Bounds.Width），避免在父视图中再声明 Popup 与 Placement。

### 3.3 关键属性（建议）

| 用途 | 属性/委托 | 说明 |
|------|-----------|------|
| 分页数据 | `LoadPageAsync` | `(searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<object>>`（或泛型），selectedIds 用于保证已选项出现在当前页。 |
| 选中项 | `SelectedItem` | 当前选中项，TwoWay。 |
| 显示文本 | `DisplayMemberPath` | 从选中项取显示字符串（如 "ProviderName"）。 |
| 项 ID | `GetItemId` | `object => int?`，用于组成 selectedIds。 |
| 占位 | `Watermark` | 无选中时的占位文本。 |
| 分页 | `PageSize` | 每页条数，默认 10。 |
| 弹窗状态 | `IsPopupOpen` | 可选，若需外部控制。 |
| 新增 | `AddNewCommand` / 事件 | 无结果时“新增”的入口，可选。 |

### 3.4 行为小结

- **打开**：点击/聚焦 → 打开 Popup，`_searchText` = 当前选中项显示文本（或无为空），以 `selectedIds + page 1 + pageSize` 加载。
- **输入**：防抖后以新 `_searchText`、page 1 加载；若 popup 未打开则打开。
- **分页**：分页条/“加载更多”以当前 `_searchText` 和新 page 加载。
- **选择**：列表点击/Enter → 更新 `SelectedItem`，关闭 Popup，焦点回 TextBox。
- **关闭（Escape / 点击外部）**：强制重置 `_searchText` 与 TextBox 显示为已选项，不保留用户输入；焦点回 TextBox。

---

## 四、服务层配合（与现有提案一致）

- 在 `GetPagedProvidersAsync`、`GetPagedMaterialsAsync` 等分页接口中：
  - 增加参数 `IReadOnlyList<int>? selectedIds`。
  - 若 `selectedIds` 中**任一**项的 Name 与 `searchText` **完全一致**，则**忽略** `searchText` 过滤条件。
- 这样在“点击打开时 TextBox 显示已选项名称并作为 searchText 传入”的场景下，不会因已选项名称而过滤掉结果。

---

## 五、与现有实现的关系

- **当前状态**：采用 SearchableSelectionBox + 父视图 Popup + GenericSelectionPopup 拼装；PageableAutoCompleteBox 实现将回滚，**本提案不依赖该控件**。
- **实现路径**：可新建一个控件（命名不沿用 PageableAutoCompleteBox），按本提案从零实现；或基于“触发器 + GenericSelectionPopup 内容”的既有逻辑，收口为单一 ControlTemplate，但**不引用** PageableAutoCompleteBox 的 axaml/cs。
- **迁移**：新控件就绪后，可在 SolidWasteModeFormView 等视图中替换现有“SearchableSelectionBox + Popup + GenericSelectionPopup”拼装，父视图不再维护 `IsXxxPopupOpen`、PlacementTarget、ApplyPopupOffset 等。

---

## 六、测试场景（与需求对应）

1. **点击打开（有选中项）**：TextBox 显示已选项名称，popup 加载时传入该文本为 searchText 与 selectedIds，列表含已选项。
2. **点击打开（无选中项）**：TextBox 为空，searchText 为空，selectedIds 为 null。
3. **输入后不选择即关闭（Escape/点击外部）**：TextBox 与内部 _searchText 恢复为已选项显示文本（或无则空）。
4. **输入后选择一项**：SelectedItem 更新，TextBox 显示新选中项，popup 关闭。
5. **无结果时“新增”**：可触发 AddNewCommand 或等价入口，行为与现有“新增”一致（若实现该能力）。

---

## 七、文档与参考

- [SearchableSelectionBox 与 AutoCompleteBox 对照分析](analysis-searchable-selection-vs-autocompletebox.md)
- [Popup 选择弹窗问题分析](popup-selection-analysis.md)
- Avalonia / Semi.Avalonia AutoCompleteBox：可参考“单 TextBox + Popup”的模板与焦点管理，本组件不直接复用其实现。
