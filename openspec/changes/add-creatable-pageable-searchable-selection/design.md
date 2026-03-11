## Context

- 当前供应商/材料/镇街等选择由 **SearchableSelectionBox**（展示 + 触发）+ 父视图中的 **Popup** + **GenericSelectionPopupView** 拼装而成，Popup 与 Placement 在父视图声明，数据与 VM 通过 `IGenericSelectionPopupBindings` 等耦合。
- 需求：单一控件内聚「可搜索、可分页、可创建」能力，关闭状态下与现有 SearchableSelectionBox 视觉一致（高度 32、白底、边框 #E5E7EB、字体 12、前景 #333333、右侧下拉箭头等），Popup 打开时与 GenericSelectionPopup 视觉一致（DataGrid + Ursa Pagination），与 ViewModel 通过标准属性/委托对接，不依赖具体 VM 类型。
- 约束：不强制一次性替换所有老界面；服务层仅在分页接口上增加 `selectedIds` 及"已选项名称与 searchText 一致时忽略过滤"的逻辑，不改变核心契约。

## Goals / Non-Goals

**Goals:**

- 实现一个 TemplatedControl，内嵌 PART_TextBox + PART_Popup（DataGrid + Ursa Pagination + 可选"新增"），通过 LoadPageAsync、CreateNewAsync、SelectedId、Watermark、PageSize 等与调用方对接。Popup 打开时的视觉结构与 GenericSelectionPopup 完全一致。ViewModel 仅提供两个纯函数（LoadPageAsync + CreateNewAsync）和一个 int? 绑定（SelectedId），零 Command、零桥接代码、零反馈环。
- SelectionItem（Id + Name）作为控件内部数据载体及 LoadPageAsync / CreateNewAsync 的返回值契约，不作为控件公共属性暴露；控件公共 API 以 SelectedId (int?) 表达选择状态。
- 关闭状态下与 SearchableSelectionBox 外观一致，复用或轻量调整现有 Hover/Focus/Error 等样式。
- Popup 打开时与 GenericSelectionPopup 视觉一致：Width=400、Height=250、BorderThickness=3、DataGrid 单列"名称"（白底黑字列头）、分页信息文本 + Ursa Pagination 组件。
- Popup 状态与 TextBox 可编辑性严格同步：打开 ↔ 可编辑，关闭 ↔ 只读。不存在"TextBox 可编辑但 Popup 未打开"或"Popup 打开但 TextBox 只读"的中间态。
- 支持点击打开、输入防抖搜索、分页、选择/新增后关闭、Escape/点击外部重置等交互；数据契约为 (searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<T>>。

**Non-Goals:**

- 一次性替换所有使用 SearchableSelectionBox + GenericSelectionPopup 的界面；旧组合暂不删除，先标记 deprecated 再后续清理。
- 修改现有服务层核心契约；仅增加可选参数与过滤逻辑。

## Decisions

- **单一控件 vs 继续拼装**：采用单一 TemplatedControl，Popup 在控件模板内、PlacementTarget 为控件自身，宽度与触发器对齐。理由：减少父视图 XAML、避免每个使用点重复声明 Popup/Placement，行为与样式集中在一处。
- **Popup 内部使用 DataGrid + Ursa Pagination（与 GenericSelectionPopup 一致）**：Popup 内容采用 DataGrid（单列"名称"、水平网格线、RowHeight=30、白底黑字列头）+ Ursa `Pagination` 组件 + 分页信息文本（"当前页:X  共N条记录"），整体布局与现有 GenericSelectionPopup 完全一致（Width=400、Height=250、BorderThickness=3、CornerRadius=4）。理由：供应商与材料等选择弹窗需保持统一视觉，避免同一界面中出现两种不同风格的选择弹窗；使用 DataGrid 和 Ursa Pagination 复用已有样式和组件库，降低维护成本。控件通过 `CurrentPage`、`TotalCount`、`ShowResults`、`CurrentPageInfo`、`TotalCountInfo` 等 StyledProperty 与模板中的 Pagination 组件实现 TwoWay 绑定，内部使用 `_suppressPageChangeLoad` 标志避免翻页与编程设值的双重加载。局部样式 `<Border.Styles>` 覆盖全局 DataGridColumnHeader 蓝底白字为白底黑字，与 GenericSelectionPopup 的本地样式覆盖一致。
- **SelectionItem 与扩展方法**：UI 与调用方只交换 SelectionItem；Provider/Material/Street 等通过 ToSelectionItem() / ToProviderId() 等扩展方法互转。理由：控件不依赖具体领域类型，便于在不同实体上复用同一控件；ViewModel 仅需少量胶水代码。
- **加载契约 selectedIds**：分页加载接受 selectedIds，当其中任一项的 Name 与 searchText 完全一致时，服务层忽略 searchText 过滤。理由：点击打开时 TextBox 显示已选项名称并作为 searchText 传入，若不忽略会导致"用已选项名搜索"结果为空。
- **关闭时强制重置**：Escape 或点击外部关闭时，_searchText 与 TextBox 显示恢复为当前已选项显示文本（无则空），不保留用户输入。理由：与"允许不选择即关闭"的预期一致，避免未提交的输入残留在 UI 上。
- **Popup 仅由显式用户交互打开，不使用 OnGotFocus 触发器**：移除 `OnGotFocus` 作为 popup 打开触发器。Popup 打开仅由以下用户行为驱动：(1) 鼠标点击控件区域（通过 Tunnel 路由 + handledEventsToo 捕获，确保 TextBox 消费 PointerPressed 后仍能响应）；(2) 键盘导航聚焦后的按键交互。理由：原设计使用 `OnGotFocus → IsPopupOpen = true` 存在三个已知缺陷——a) 选择关闭后编程式 Focus(TextBox) 触发 GotFocus 导致 popup 立即重新弹出（死循环）；b) 初始渲染时焦点系统自动聚焦到控件导致 popup 在页面加载瞬间弹出；c) Escape 关闭 popup 但焦点留在 TextBox，再次点击 TextBox 不触发 GotFocus 导致 popup 未打开但 TextBox 可编辑（状态不同步）。`SearchableSelectionBox` 虽然也有相同的 `OnGotFocus` 模式但不出问题，是因为其 `IsPopupOpen` 仅控制内部面板可见性切换而非真正的 Popup，实际 Popup 由父视图的独立属性控制。本控件的 Popup 直接绑定 `IsPopupOpen`，因此不能使用 focus 作为触发器。
- **TextBox 可编辑性与 IsPopupOpen 严格绑定**：Popup 关闭时 TextBox 设为 `IsReadOnly=true`（或 `IsHitTestVisible=false`），Popup 打开时恢复可编辑。理由：保证"编辑状态"与"popup 打开"始终同步，不存在仅其中一个生效的中间态。关闭态下 TextBox 显示已选项名称或 watermark，不可交互编辑。
- **选择/关闭后不主动 Focus TextBox**：DataGrid 选择项后或 Escape 关闭后，不再调用 `Dispatcher.Post(() => PART_TextBox.Focus())`。让焦点自然停留或移走。理由：主动 focus 是死循环的直接诱因；不 focus 则 GotFocus 不会触发（即使保留 OnGotFocus 也安全）；用户若要再次打开只需点击控件即可。
- **关闭态 TextBox 完全退出焦点系统（Focusable + IsHitTestVisible 三态同步）**：`SearchableSelectionBox` 关闭态展示的是 TextBlock（天然不可聚焦），而本控件复用同一个 TextBox 显示/编辑。因此关闭态必须将 TextBox 降级为等效于 TextBlock 的纯展示状态：`Focusable=false`（退出 Tab 导航、阻止焦点系统自动聚焦）+ `IsHitTestVisible=false`（点击事件穿透到 Border，由 OnRootPointerPressed 处理打开）+ `IsReadOnly=true`（防止编辑）。打开态恢复为 `Focusable=true` + `IsHitTestVisible=true` + `IsReadOnly=false`。设 `Focusable=false` 时 Avalonia 会自动释放该元素的焦点，解决"点击外部后焦点仍停留在 TextBox"和"初始渲染时 TextBox 被焦点系统选中导致光标闪烁"两个问题。
- **OnRootPointerPressed 使用 Dispatcher.Post 延迟打开 Popup**：点击控件时，OnRootPointerPressed 通过 `Dispatcher.UIThread.Post(() => IsPopupOpen = true)` 延迟到当前输入事件处理完毕后再打开 Popup。理由：Popup 启用了 `IsLightDismissEnabled=true`，若在 Tunnel 阶段同步设置 `IsPopupOpen=true`，同一个点击事件在 Bubble 阶段会被 LightDismiss 识别为"外部点击"并立即关闭 Popup（同一次点击既打开又关闭）。延迟到下一个 Dispatcher 帧可避免此竞态。
- **公共 API 从 SelectedItem (SelectionItem?) 改为 SelectedId (int?)，SelectionItem 降为控件内部类型**：控件的公共选择状态由 `SelectedId` (int?, TwoWay) 表达，ViewModel 仅绑定此属性。`SelectionItem` (Id + Name) 仍用于控件内部的 DataGrid 展示和 `LoadPageAsync` / `CreateNewAsync` 的返回值契约，但不再作为控件的公共属性暴露给 ViewModel。理由：此前 `SelectedItem` (SelectionItem?) 作为 TwoWay 属性暴露给 ViewModel，ViewModel 被迫维护 `SelectedProvider ↔ SelectedProviderSelectionItem` 双向响应链（28 行桥接代码 + WhenAnyValue 反馈环）。当用户选择不同项时，ViewModel 响应链将新的 SelectionItem 实例回推至控件，触发 popup 关闭后又重新打开的 bug。改用 `SelectedId` (int?) 后：(1) ViewModel 直接绑定已有的 `SelectedProviderId`，零桥接代码；(2) 不存在对象引用回推，消除反馈环和 popup 重弹 bug；(3) SelectionItem 成为 LoadPageAsync 返回的数据载体，控件内部自行管理 Id → Name 的解析与展示。控件在 `SelectedId` 变化时，通过 `LoadPageAsync(selectedIds: [id])` 获取该项的 Name 用于展示；若 CurrentPageItems 已包含该 Id 则直接匹配，无需额外请求。
- **AddNewCommand (ICommand) 改为 CreateNewAsync (Func<string, CancellationToken, Task<SelectionItem?>>?)，创建后编排逻辑归控件所有**：原设计中 ViewModel 通过 `AddNewCommand` (ReactiveCommand) 执行创建，且创建完成后由 ViewModel 负责"选中新项 + 刷新列表 + 同步 SelectedProvider"等编排逻辑。新设计中，ViewModel 仅提供一个纯函数 `CreateNewAsync`，签名为 `(searchText, ct) → SelectionItem?`，只负责调用 Service 创建实体并返回结果。控件内部在用户点击"新增"按钮时调用此函数，传入当前搜索文本作为名称提示；创建成功后，控件自动设置 `SelectedId = newItem.Id`、刷新页面数据（LoadPageAsync with selectedIds）、更新展示文本。理由：(1) ViewModel 的 AddNewProviderCommand 中 8 行代码，仅前 2 行是业务知识（deliveryType、调哪个 Service），后续的"选中 + 刷新 + 同步"是通用编排，应封装在控件中；(2) 使 ViewModel 只需提供两个纯函数（LoadPageAsync + CreateNewAsync）即可完成全部配置，零 Command、零桥接、零反馈环；(3) 搜索文本作为名称提示传给 CreateNewAsync，比硬编码"新供应商"更合理——用户搜索"ABC公司"无结果后点新增，直接创建名为"ABC公司"的供应商。
- **Popup 关闭时取消悬挂的 debounce 计时器**：在 `OnIsPopupOpenChanged(false)` 中调用 `_debounceCts?.Cancel()`。理由：用户在 Popup 打开时输入搜索文本触发 debounce（300ms），若在 debounce 到期前通过选择项关闭 Popup，悬挂的 debounce 回调会在到期后执行 `if (!IsPopupOpen) IsPopupOpen = true`，导致 Popup 意外重新弹出。
- **Popup 关闭后添加冷却保护防止 OnRootPointerPressed 立即重开**：在 `OnIsPopupOpenChanged(false)` 中设置 `_suppressNextOpen = true`，并通过 `Dispatcher.UIThread.Post(() => _suppressNextOpen = false)` 在下一帧重置。`OnRootPointerPressed` 在 `_suppressNextOpen` 为 true 时跳过打开。理由：Popup 关闭时（尤其是从 DataGrid 选择后程序关闭），Avalonia 的指针系统可能在 Popup 覆盖层移除后重新评估指针位置，产生新的 PointerPressed 事件到达控件区域，触发 `OnRootPointerPressed` 立即重开 Popup。冷却保护确保关闭动作的同一事件周期内不会重新打开。

## Risks / Trade-offs

- **迁移期双轨**：新旧两种用法并存时，需在文档与代码注释中明确"新界面优先用新控件"，避免继续堆积旧组合。缓解：在 GenericSelectionPopup 相关类型上标记 Obsolete/Deprecated，并在 proposal 与 tasks 中写明清迁节奏。
- **服务层契约扩展**：分页接口增加 selectedIds 可能影响现有调用方。缓解：参数设计为可选（如 IReadOnlyList<int>? selectedIds），现有调用不传即可；忽略 searchText 的逻辑仅在"任一项 Name 与 searchText 完全一致"时触发，行为可测。
- **样式一致性**：新控件关闭态需与 SearchableSelectionBox 像素级一致，Popup 打开态需与 GenericSelectionPopup 布局一致。缓解：关闭态复用同一组样式资源（Border、字体、颜色、箭头）；Popup 内使用相同的 DataGrid + Pagination 结构，局部样式覆盖 DataGridColumnHeader 保证列头白底黑字。

## Migration Plan

1. **实现阶段**：在 MaterialClient.UI 中新增控件及默认模板（DataGrid + Ursa Pagination）、SelectionItem 与扩展方法；服务层分页接口增加 selectedIds 与过滤逻辑；为新控件编写单元/集成测试。
2. **首个接入点**：在 `SolidWasteModeFormView` 中，仅将供应商选择由现有 SearchableSelectionBox + GenericSelectionPopup 组合替换为新控件，验证端到端行为与样式。
3. **进一步推广（后续变更处理）**：是否在更多界面中替换其它选择控件，不在本次变更范围内；如需推广，将通过新的变更与任务单独设计与评审。
4. **回滚**：若新控件在 `SolidWasteModeFormView` 中出现严重问题，可回退到仍使用旧组合的版本；新控件为增量替换，不删除旧代码直至后续废弃计划执行。

## Open Questions

- 控件在 `SelectedId` 变化但 `CurrentPageItems` 中没有该 Id 时的首次展示：控件通过 `LoadPageAsync(selectedIds: [id])` 获取该项的 Name 用于展示。若 `LoadPageAsync` 尚未完成或返回空，TextBox 显示 Watermark 直到数据加载完毕。这是否足够，还是需要额外的 `ResolveDisplayTextAsync` 回调？当前判断无需——因为控件打开或 SelectedId 变化时都会触发 LoadPageAsync，selectedIds 参数确保后端将该项包含在结果中。
