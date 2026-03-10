## 1. 模型与扩展方法

- [ ] 1.1 定义 SelectionItem 类（Id, Name），放在合适命名空间（如 MaterialClient.UI 或共享层）
- [ ] 1.2 为 Provider 添加 ToSelectionItem() 与 SelectionItem 的 ToProviderId()（或等价）扩展方法
- [ ] 1.3 为 Material 添加 ToSelectionItem() 与 SelectionItem 的 ToMaterialId() 扩展方法
- [ ] 1.4 为 Street 添加 ToSelectionItem() 与 SelectionItem 的 ToStreetName()（或等价）扩展方法

## 2. 控件骨架与模板

- [ ] 2.1 新增 TemplatedControl，定义 PART_TextBox 与 PART_Popup
- [ ] 2.2 在默认模板中实现 Popup 内结构：列表（ListBox/ItemsControl）、分页区、可选空状态与“新增”区；PlacementTarget 为控件自身，宽度与触发器对齐
- [ ] 2.3 暴露属性：LoadPageAsync、SelectedItem（TwoWay）、DisplayMemberPath、GetItemId、Watermark、PageSize、IsPopupOpen（可选）、AddNewCommand（可选）

## 3. 交互与数据加载

- [ ] 3.1 实现点击/聚焦打开 Popup，_searchText 初始为当前选中项显示文本，以 selectedIds + page 1 + pageSize 调用 LoadPageAsync
- [ ] 3.2 实现 TextBox 输入防抖（如 300ms）后以新 searchText、page 1 请求；若 popup 未打开则打开
- [ ] 3.3 实现分页条/“加载更多”以当前 searchText 与新 page 调用 LoadPageAsync
- [ ] 3.4 实现列表选择（点击/Enter）更新 SelectedItem、关闭 Popup、焦点回 TextBox
- [ ] 3.5 实现 Escape/点击外部关闭时强制重置 _searchText 与 TextBox 显示为已选项（无则空）
- [ ] 3.6 实现 Popup 内 Arrow Up/Down 高亮移动、Enter 确认当前项

## 4. 样式与视觉一致

- [ ] 4.1 关闭状态样式与 SearchableSelectionBox 一致：Height=32、背景 #FFFFFF、边框 #E5E7EB、内边距、字体 12、前景 #333333、右侧下拉箭头 10×6 #666666、TextTrimming=CharacterEllipsis
- [ ] 4.2 复用或轻量调整 Hover/Focus/Error 等现有样式资源

## 5. 服务层配合

- [ ] 5.1 在 GetPagedProvidersAsync（或等价）中增加参数 IReadOnlyList<int>? selectedIds，当任一项 Name 与 searchText 完全一致时忽略 searchText 过滤
- [ ] 5.2 在 GetPagedMaterialsAsync（或等价）中增加 selectedIds 及相同过滤逻辑
- [ ] 5.3 在镇街等其它分页接口（若有）中按需增加 selectedIds 及过滤逻辑

## 6. 可创建（Add New）

- [ ] 6.1 无结果时显示空状态与“新增”入口，绑定 AddNewCommand 或等价；行为与现有 GenericSelectionPopup “新增”一致

## 7. 无头测试与迁移

- [ ] 7.1 测试：点击打开（有选中项）时 TextBox 显示已选项名称，列表含已选项
- [ ] 7.2 测试：点击打开（无选中项）时 searchText 为空、selectedIds 为 null
- [ ] 7.3 测试：输入后不选择即关闭（Escape/点击外部）时 TextBox 与 _searchText 恢复为已选项
- [ ] 7.4 测试：选择一项后 SelectedItem 更新、Popup 关闭
- [ ] 7.5 测试：无结果时“新增”可触发 AddNewCommand（若实现）
- [ ] 7.6 在 UI 无头测试工程（MaterialClient.UI.Test）中新增针对 `CreatablePageableSearchableSelectionBox` 的无头测试类，验证关闭态视觉（Height=32、背景/边框/字体/箭头尺寸与 SearchableSelectionBox 一致）
- [ ] 7.7 在 UI 无头测试中验证 `CreatablePageableSearchableSelectionBox` 的交互行为（点击/聚焦打开、输入搜索、防抖调用 LoadPageAsync、分页、Escape/点击外部重置、键盘上下/Enter 选择）
- [ ] 7.8 在 `SolidWasteModeFormView` 中仅替换供应商选择为新控件，验证端到端
