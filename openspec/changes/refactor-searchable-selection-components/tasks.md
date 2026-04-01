## 1. DTO 与基础设施

- [x] 1.1 实现 `SelectionItem` DTO 类：包含 `int Id`、`string Name`、静态工厂方法（`FromProvider`、`FromMaterial`、`FromStreet`）、`IEquatable<SelectionItem>`（仅比较 Id）
- [x] 1.2 实现 `GetStableHashCode()` 字符串扩展方法（确定性稳定哈希，供 `FromStreet` 使用）

## 2. SearchableSelectionBox 控件重构

- [x] 2.1 重写 `SearchableSelectionBox.axaml`：UserControl 内嵌 Popup（Placement=Bottom），包含 Border（触发区）→ TextBlock/TextBox 切换 + 下拉箭头，Popup 内含 DataGrid + 无结果提示/新增按钮 + Ursa Pagination
- [x] 2.2 重写 `SearchableSelectionBox.axaml.cs`：定义 StyledProperty（`SelectedItem`、`LoadPageAsync`、`CreateNewAsync`、`Watermark`、`AllowCreateNew`、`PageSize`、`PopupWidth`），实现 Popup 打开/关闭管理、选中项恢复（`_isInitializing` 标志）、PointerPressed 确认选择并关闭
- [x] 2.3 实现响应式搜索：TextBox 输入 → 300ms 防抖 → 调用 `LoadPageAsync(search, page, pageSize, selectedIds)`
- [x] 2.4 实现分页：Ursa Pagination 页码变更 → 调用 `LoadPageAsync` 加载对应页
- [x] 2.5 实现新增逻辑：搜索文本非空且与当前页结果不一致时显示"新增"按钮 → 调用 `CreateNewAsync(name)` → 成功设为 SelectedItem 并关闭 Popup，失败保持打开
- [x] 2.6 实现取消逻辑：ESC 键关闭 Popup 且不更新 SelectedItem；Popup 使用 IsLightDismissEnabled 支持点击外部关闭

## 3. 父级 ViewModel 迁移

- [x] 3.1 重构 `AttendedWeighingDetailViewModel`：移除 3 组 `GenericSelectionPopupViewModel<T>` + `IsXxxPopupOpen` 属性（共 6 个），替换为每个选择器提供 `LoadPageAsync` 委托和 `CreateNewAsync` 委托
- [x] 3.2 为供应商选择器实现 `LoadPageAsync` 委托：包装 `IProviderService.GetPagedProvidersAsync`，结果映射为 `SelectionItem`
- [x] 3.3 为物料选择器实现 `LoadPageAsync` 委托：包装 `IMaterialService.GetPagedMaterialsAsync`，结果映射为 `SelectionItem`
- [x] 3.4 为镇街选择器实现 `LoadPageAsync` 委托：包装内存配置数据为 `PagedResultDto<SelectionItem>`
- [x] 3.5 为供应商选择器实现 `CreateNewAsync` 委托：调用创建逻辑，返回 `SelectionItem`
- [x] 3.6 为物料选择器实现 `CreateNewAsync` 委托：调用创建逻辑，返回 `SelectionItem`
- [x] 3.7 清理 ViewModel 中旧的 WhenAnyValue 订阅和 Popup 同步逻辑

## 4. 父级 View 迁移

- [x] 4.1 重构 `SolidWasteModeFormView.axaml`：将三处"SearchableSelectionBox + 独立 Popup"三件套替换为新的自包含 `SearchableSelectionBox`，移除外部 Popup 声明和 `IsXxxPopupOpen` 绑定
- [x] 4.2 验证新组件在父视图中的绑定正确（SelectedItem 双向绑定 + LoadPageAsync/CreateNewAsync 委托绑定 + Watermark）

## 5. 清理旧代码

- [x] 5.1 删除 `GenericSelectionPopup.axaml` 和 `GenericSelectionPopup.axaml.cs`
- [x] 5.2 删除 `GenericSelectionPopupViewModel<T>`（含 `IGenericSelectionItem`、`IGenericSelectionPopupViewModel`、`IGenericSelectionPopupBindings` 接口）
- [x] 5.3 评估 `MaterialsSelectionPopup` 及其 ViewModel 是否仍在使用；若废弃则一并删除（结论：仍被 StandardModeFormView 使用，保留）
