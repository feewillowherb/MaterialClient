## ADDED Requirements

### Requirement: SelectionItem 统一 DTO

系统 SHALL 提供统一的 `SelectionItem` DTO，包含 `int Id` 和 `string Name` 属性，并提供以下静态工厂方法：
- `SelectionItem.FromProvider(ProviderDto provider)` — 从供应商 DTO 创建
- `SelectionItem.FromMaterial(Material material)` — 从物料实体创建
- `SelectionItem.FromStreet(string street)` — 从镇街字符串创建（Id 使用确定性稳定哈希生成）

系统 SHALL 在 `SelectionItem` 上实现 `IEquatable<SelectionItem>`，仅比较 `Id` 相等性。

#### Scenario: 从供应商创建 SelectionItem

- **WHEN** 调用 `SelectionItem.FromProvider(providerDto)`，其中 `providerDto.Id = 42`，`providerDto.ProviderName = "测试供应商"`
- **THEN** 返回的 `SelectionItem` 的 `Id` SHALL 为 42，`Name` SHALL 为 "测试供应商"

#### Scenario: 从物料创建 SelectionItem

- **WHEN** 调用 `SelectionItem.FromMaterial(material)`，其中 `material.Id = 10`，`material.Name = "水泥"`
- **THEN** 返回的 `SelectionItem` 的 `Id` SHALL 为 10，`Name` SHALL 为 "水泥"

#### Scenario: 从镇街字符串创建 SelectionItem

- **WHEN** 调用 `SelectionItem.FromStreet("某街道")`
- **THEN** 返回的 `SelectionItem` 的 `Name` SHALL 为 "某街道"，`Id` SHALL 为基于名称生成的确定性稳定哈希值

#### Scenario: SelectionItem 相等性仅比较 Id

- **WHEN** 两个 `SelectionItem` 的 `Id` 相同但 `Name` 不同
- **THEN** `Equals` SHALL 返回 `true`

### Requirement: SearchableSelectionBox 自包含 Popup

系统 SHALL 提供自包含的 `SearchableSelectionBox` UserControl，将 Popup 内嵌于控件内部，父视图无需声明独立的 Popup 元素。

控件 SHALL 通过以下 StyledProperty 暴露配置接口：
- `SelectedItem` (`SelectionItem?`, TwoWay) — 当前选中项
- `LoadPageAsync` (委托) — 分页加载函数，签名为 `Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>`
- `CreateNewAsync` (委托，可选) — 新增函数，签名为 `Func<string, Task<SelectionItem?>>`
- `Watermark` (`string?`, 默认 "请选择") — 无选中时的占位文本
- `AllowCreateNew` (`bool`, 默认 true) — 是否允许创建新项
- `PageSize` (`int`, 默认 4) — 每页条数
- `PopupWidth` (`double`, 默认 400) — Popup 宽度

#### Scenario: 控件声明无需外部 Popup

- **WHEN** 在父视图中声明 `<views:SearchableSelectionBox SelectedItem="{Binding X}" LoadPageAsync="{Binding LoadFunc}" />`
- **THEN** 控件 SHALL 自动管理内部 Popup 的打开/关闭，父视图无需声明 `<Popup>` 元素

#### Scenario: Popup 宽度可自定义

- **WHEN** 设置 `PopupWidth="500"`
- **THEN** 弹出 Popup 的宽度 SHALL 为 500 像素
- **WHEN** 未设置 PopupWidth
- **THEN** 弹出 Popup 的宽度 SHALL 默认为 400 像素

### Requirement: 响应式搜索与防抖

系统 SHALL 在 Popup 打开后，用户在 TextBox 中输入文本时，以 300ms 防抖延迟触发搜索，调用 `LoadPageAsync` 加载匹配结果。

#### Scenario: 输入触发搜索

- **WHEN** Popup 打开后用户在 TextBox 中输入 "供应"
- **THEN** 系统 SHALL 在用户停止输入 300ms 后调用 `LoadPageAsync("供应", 1, pageSize, selectedIds)`

#### Scenario: 清空搜索文本显示全部

- **WHEN** 用户清空 TextBox 中的搜索文本
- **THEN** 系统 SHALL 调用 `LoadPageAsync(null, 1, pageSize, selectedIds)` 加载未过滤的第一页

### Requirement: 分页支持

系统 SHALL 在 Popup 底部显示 Ursa Pagination 组件，支持翻页操作。翻页时 SHALL 调用 `LoadPageAsync` 加载对应页数据。

#### Scenario: 点击分页控件翻页

- **WHEN** 用户点击 Pagination 的第 2 页
- **THEN** 系统 SHALL 调用 `LoadPageAsync(searchText, 2, pageSize, selectedIds)` 加载第二页数据

#### Scenario: 搜索时重置到第一页

- **WHEN** 用户输入新的搜索文本触发搜索
- **THEN** 当前页 SHALL 重置为 1

### Requirement: 选择确认与取消

| 行为 | 结果 |
|------|------|
| 点击列表项 | 更新 SelectedItem 并关闭 Popup |
| 双击列表项 | 同单击（保持一致） |
| ESC 键 | 关闭 Popup，SelectedItem 不变 |
| 点击外部（LightDismiss） | 关闭 Popup，SelectedItem 不变 |

#### Scenario: 选择已有项并确认

- **WHEN** 用户在 Popup 中点击某列表项
- **THEN** `SelectedItem` SHALL 更新为该项，Popup SHALL 关闭，控件外观 SHALL 显示该项的 Name

#### Scenario: 再次选择当前已选中项

- **WHEN** 用户在 Popup 中点击当前已选中项（与 SelectedItem 相同 Id 的项）
- **THEN** Popup SHALL 关闭，`SelectedItem` SHALL 保持不变（Id 相同，属于确认行为）

#### Scenario: ESC 取消

- **WHEN** Popup 打开时用户按 ESC 键
- **THEN** Popup SHALL 关闭，`SelectedItem` SHALL 不变

#### Scenario: 点击外部取消

- **WHEN** Popup 打开时用户点击 Popup 外部区域
- **THEN** Popup SHALL 关闭（IsLightDismissEnabled），`SelectedItem` SHALL 不变

### Requirement: 新增功能

当 `AllowCreateNew` 为 true 且 `CreateNewAsync` 委托已提供时，系统 SHALL 在以下条件满足时显示"新增"按钮：
- 搜索文本非空（Trim 后）
- 搜索文本与当前页中任一项的 Name 不完全一致（忽略大小写）

新增流程：点击新增 → 调用 `CreateNewAsync(name)` → 成功后将返回的 SelectionItem 设为 SelectedItem → 关闭 Popup。

#### Scenario: 无结果时显示新增按钮

- **WHEN** 用户输入搜索文本后无任何匹配结果
- **THEN** 系统 SHALL 显示"未找到匹配结果"文本与"新增"按钮

#### Scenario: 有结果但输入与结果不一致时显示新增

- **WHEN** 用户输入搜索文本后有匹配结果，但当前页中没有任何项的 Name 与搜索文本（Trim 后）完全一致（忽略大小写）
- **THEN** 系统 SHALL 在列表下方独立行显示"新增"按钮

#### Scenario: 输入与某结果完全一致时不显示新增

- **WHEN** 当前页中某项的 Name 与搜索文本（Trim 后）完全一致（忽略大小写）
- **THEN** 系统 SHALL 不显示"新增"按钮

#### Scenario: 新增成功后自动选中并关闭

- **WHEN** 用户点击"新增"按钮且 `CreateNewAsync` 返回非 null 的 SelectionItem
- **THEN** `SelectedItem` SHALL 更新为新项，Popup SHALL 关闭

#### Scenario: 新增失败保持 Popup 打开

- **WHEN** 用户点击"新增"按钮且 `CreateNewAsync` 返回 null
- **THEN** Popup SHALL 保持打开

#### Scenario: 不允许新增时不显示按钮

- **WHEN** `AllowCreateNew` 为 false 或 `CreateNewAsync` 未提供
- **THEN** 系统 SHALL 不显示"新增"相关 UI

### Requirement: 打开时恢复当前选中项

当 Popup 打开时，系统 SHALL 通过 `LoadPageAsync` 的 `selectedIds` 参数传入当前 `SelectedItem` 的 Id，确保当前选中项出现在第一页结果中，并在加载完成后恢复其在 DataGrid 中的选中状态。

#### Scenario: 有选中项时打开 Popup

- **WHEN** `SelectedItem` 不为 null 且用户点击控件打开 Popup
- **THEN** 系统 SHALL 调用 `LoadPageAsync(searchText, 1, pageSize, [SelectedItem.Id])` 并在结果中恢复选中状态

#### Scenario: 无选中项时打开 Popup

- **WHEN** `SelectedItem` 为 null 且用户点击控件打开 Popup
- **THEN** 系统 SHALL 调用 `LoadPageAsync(searchText, 1, pageSize, null)` 且不恢复任何选中状态

## MODIFIED Requirements

（无已修改的需求 — 新能力 `searchable-selection` 完全替代原有 `generic-selection-popup` 能力）

## REMOVED Requirements

### Requirement: 有搜索结果时仍可新增
**Reason**: 被 `searchable-selection` 中的"新增功能"需求完整替代，行为一致
**Migration**: 新组件中此行为由 SearchableSelectionBox 内部处理，逻辑不变

### Requirement: 有结果时新增按钮不遮挡列表内容
**Reason**: 被 `searchable-selection` 中的"新增功能"需求覆盖，保持列表下方独立行布局
**Migration**: 新组件保持相同的布局方案（列表 + 独立行新增按钮 + 分页）
