## 1. 模型与扩展方法

- [x] 1.1 定义 SelectionItem 类（Id, Name），放在合适命名空间（如 MaterialClient.UI 或共享层）
- [x] 1.2 为 Provider 添加 ToSelectionItem() 与 SelectionItem 的 ToProviderId()（或等价）扩展方法
- [x] 1.3 为 Material 添加 ToSelectionItem() 与 SelectionItem 的 ToMaterialId() 扩展方法
- [x] 1.4 为 Street 添加 ToSelectionItem() 与 SelectionItem 的 ToStreetName()（或等价）扩展方法

## 2. 控件骨架与模板

- [x] 2.1 新增 TemplatedControl，定义 PART_TextBox 与 PART_Popup
- [x] 2.2 在默认模板中实现 Popup 内结构：DataGrid（单列"名称"、水平网格线、RowHeight=30、白底黑字列头）+ Ursa Pagination（分页信息 + 翻页控件）+ 可选空状态与"新增"区；Popup 外观与 GenericSelectionPopup 一致（Width=400、Height=250、BorderThickness=3、CornerRadius=4）；PlacementTarget 为控件自身
- [x] 2.3 暴露属性：LoadPageAsync、SelectedItem（TwoWay）、Watermark、PageSize、IsPopupOpen、AddNewCommand、CurrentPage、TotalCount、ShowResults、CurrentPageInfo、TotalCountInfo、ShowAddNew

## 3. 交互与数据加载

- [x] 3.1 实现点击/聚焦打开 Popup，_searchText 初始为当前选中项显示文本，以 selectedIds + page 1 + pageSize 调用 LoadPageAsync
- [x] 3.2 实现 TextBox 输入防抖（如 300ms）后以新 searchText、page 1 请求；若 popup 未打开则打开
- [x] 3.3 实现 Ursa Pagination 翻页：CurrentPage 通过 TwoWay 绑定驱动，属性变化时以当前 searchText 与新 page 调用 LoadPageAsync；使用 _suppressPageChangeLoad 避免编程设值（如 popup 打开时重置 page=1）导致的双重加载
- [x] 3.4 实现 DataGrid 选择（点击/DoubleTapped）更新 SelectedItem、关闭 Popup、焦点回 TextBox
- [x] 3.5 实现 Escape/点击外部关闭时强制重置 _searchText 与 TextBox 显示为已选项（无则空）
- [x] 3.6 实现 DataGrid 内的选择确认行为（SelectionChanged + DoubleTapped 事件处理）

## 4. 样式与视觉一致

- [x] 4.1 关闭状态样式与 SearchableSelectionBox 一致：Height=32、背景 #FFFFFF、边框 #E5E7EB、内边距、字体 12、前景 #333333、右侧下拉箭头 10×6 #666666
- [x] 4.2 Popup 打开状态样式与 GenericSelectionPopup 一致：Border（White、#E5E7EB、3px、4px 圆角、400×250）、DataGrid（Height=200、列头白底黑字通过 `<Border.Styles>` 局部覆盖全局蓝底白字）、分页栏（Height=50、左侧"当前页:X  共N条记录"、右侧 Ursa Pagination）
- [x] 4.3 复用或轻量调整 Hover/Focus/Error 等现有样式资源

## 5. 服务层配合

- [x] 5.1 在 GetPagedProvidersAsync（或等价）中增加参数 IReadOnlyList<int>? selectedIds，当任一项 Name 与 searchText 完全一致时忽略 searchText 过滤
- [x] 5.2 在 GetPagedMaterialsAsync（或等价）中增加 selectedIds 及相同过滤逻辑
- [x] 5.3 在镇街等其它分页接口（若有）中按需增加 selectedIds 及过滤逻辑

## 6. 可创建（Add New）

- [x] 6.1 无结果时显示空状态与"新增"入口，绑定 AddNewCommand 或等价；行为与现有 GenericSelectionPopup "新增"一致

## 7. 无头测试与迁移

- [x] 7.1 测试：点击打开（有选中项）时 TextBox 显示已选项名称，列表含已选项
- [x] 7.2 测试：点击打开（无选中项）时 searchText 为空、selectedIds 为 null
- [x] 7.3 测试：输入后不选择即关闭（Escape/点击外部）时 TextBox 与 _searchText 恢复为已选项
- [x] 7.4 测试：选择一项后 SelectedItem 更新、Popup 关闭
- [x] 7.5 测试：无结果时"新增"可触发 AddNewCommand（若实现）
- [x] 7.6 在 UI 无头测试工程（MaterialClient.UI.Test）中新增针对 `CreatablePageableSearchableSelectionBox` 的无头测试类，验证关闭态视觉（Height=32、背景/边框/字体与 SearchableSelectionBox 一致）及 Popup 模板部件（PART_DataGrid、PART_EmptyPanel）
- [x] 7.7 在 UI 无头测试中验证 `CreatablePageableSearchableSelectionBox` 的交互行为（点击/聚焦打开、输入搜索、防抖调用 LoadPageAsync、分页 CurrentPage 变更触发重载、Escape/点击外部重置、DataGrid 选择确认）及分页属性（CurrentPage、TotalCount、ShowResults、CurrentPageInfo、TotalCountInfo）
- [x] 7.8 在 `SolidWasteModeFormView` 中仅替换供应商选择为新控件，验证端到端

## 8. 焦点与状态管理修复（方案 B — 重构状态模型）

- [x] 8.1 移除 `OnGotFocus` 中 `IsPopupOpen = true` 的逻辑；控件可保留 `Focusable=true`，但 GotFocus 不再驱动 popup
- [x] 8.2 在 `OnApplyTemplate` 中通过 `AddHandler(PointerPressedEvent, handler, RoutingStrategies.Tunnel, handledEventsToo: true)` 注册 PART_RootBorder（或控件自身）的指针按下事件，保证 TextBox 消费 PointerPressed 后仍能触发 popup 打开
- [x] 8.3 在 `OnIsPopupOpenChanged` 中同步 TextBox 可编辑性：`IsPopupOpen=true` 时设 `PART_TextBox.IsReadOnly = false`；`IsPopupOpen=false` 时设 `PART_TextBox.IsReadOnly = true`，并恢复 TextBox 为已选项文本或 watermark
- [x] 8.4 移除 `OnDataGridSelectionChanged` 和 `OnDataGridDoubleTapped` 中的 `Dispatcher.Post(() => PART_TextBox.Focus())`，选择/关闭后不主动 focus TextBox
- [x] 8.5 确认 `OnRootPointerPressed` 只在 `!IsPopupOpen` 时设 `IsPopupOpen = true`，且使用 Tunnel 路由确保事件在 TextBox 处理之前被捕获
- [x] 8.6 验证初始渲染时 popup 不弹出：TextBox 初始为 `IsReadOnly=true`，显示 watermark 或已选项名称
- [x] 8.7 更新无头测试：新增测试验证初始渲染不弹出 popup、选择后 popup 不重新弹出、关闭→重新点击→popup 正常打开、任意时刻 IsPopupOpen 与 TextBox.IsReadOnly 互斥

## 9. TextBox 焦点系统退出与 LightDismiss 竞态修复

- [x] 9.1 在 `OnIsPopupOpenChanged(true)` 中设置 `PART_TextBox.Focusable = true` 和 `PART_TextBox.IsHitTestVisible = true`（在设 `IsReadOnly = false` 的同一处）
- [x] 9.2 在 `OnIsPopupOpenChanged(false)` 中设置 `PART_TextBox.Focusable = false` 和 `PART_TextBox.IsHitTestVisible = false`（在设 `IsReadOnly = true` 的同一处）；Avalonia 在 Focusable=false 时自动释放焦点
- [x] 9.3 在 `OnApplyTemplate` 中初始化 `PART_TextBox.Focusable = false` 和 `PART_TextBox.IsHitTestVisible = false`（与现有 `IsReadOnly = true` 初始化一致）
- [x] 9.4 `OnRootPointerPressed` 使用 `Dispatcher.UIThread.Post(() => { if (!IsPopupOpen) IsPopupOpen = true; })` 延迟打开，避免同一个点击事件被 LightDismiss 立即关闭
- [x] 9.5 更新无头测试：验证关闭态 TextBox.Focusable=false 和 TextBox.IsHitTestVisible=false；打开态三属性均恢复；多轮 open/close 循环中三属性始终与 IsPopupOpen 同步

## 10. API 重构：SelectedId + CreateNewAsync + 消除反馈环

- [x] 10.1 新增 `SelectedId` (int?, StyledProperty, TwoWay) 替代 `SelectedItem` (SelectionItem?) 作为公共选择 API；控件内部维护 `_selectedDisplayName` 用于 TextBox 展示
- [x] 10.2 在 `SelectedId` 变化时（来自 ViewModel 绑定），从 `CurrentPageItems` 中查找匹配项更新展示文本；若未找到则触发 `LoadPageAsync(selectedIds: [id])` 加载并匹配
- [x] 10.3 在 DataGrid 选择时设置 `SelectedId = item.Id`（替代 `SelectedItem = item`），内部更新展示文本
- [x] 10.4 新增 `CreateNewAsync` (Func<string, CancellationToken, Task<SelectionItem?>>?, StyledProperty) 替代 `AddNewCommand` (object?)；控件内部在"新增"按钮点击时调用，传入当前 `_searchText`
- [x] 10.5 实现控件内部的创建后编排：`CreateNewAsync` 返回非空时，设 `SelectedId = result.Id`，刷新 `LoadPageAsync(selectedIds: [result.Id])`，关闭 popup，更新展示文本
- [x] 10.6 移除 `SelectedItem` 公共属性（或标记 Obsolete）、移除 `AddNewCommand` 公共属性、移除 `_selectedItemSub` 订阅
- [x] 10.7 在 `OnIsPopupOpenChanged(false)` 中添加 `_debounceCts?.Cancel()` 取消悬挂的 debounce 计时器
- [x] 10.8 添加 `_suppressNextOpen` 冷却标志：`OnIsPopupOpenChanged(false)` 设 true + `Dispatcher.Post` 重置 false；`OnRootPointerPressed` 检查此标志
- [x] 10.9 更新 XAML 模板：PART_AddNewButton 的 Command 改为控件内部处理（不再绑定外部 AddNewCommand）
- [x] 10.10 更新 `SolidWasteModeFormView.axaml` 绑定：`SelectedItem` → `SelectedId="{Binding SelectedProviderId, Mode=TwoWay}"`，`AddNewCommand` → `CreateNewAsync="{Binding CreateProviderFunc}"`
- [x] 10.11 更新 `AttendedWeighingDetailViewModel`：移除 `SelectedProviderSelectionItem` 属性、移除双向 WhenAnyValue 响应链（line 87-114 的反馈环）、移除 `AddNewProviderCommand`；新增 `CreateProviderFunc` (Func) 属性；保留 `SelectedProvider` 单向同步到 `SelectedProviderId`
- [x] 10.12 更新无头测试：验证 SelectedId 绑定、CreateNewAsync 内部编排、关闭冷却保护、debounce 取消、选择不同项后 popup 不重弹
