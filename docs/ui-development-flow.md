# MaterialClient UI 开发流程（Avalonia + ReactiveUI）

**用途**：记录本仓库当前的 UI 开发“怎么做、按什么顺序做”，面向接手维护同事与新增功能开发者。  
**适用范围**：MaterialClient 桌面端（Avalonia UI + ReactiveUI，含弹窗/选择器/对话框）。

---

## 1. UI 栈与基本约定

1. UI 框架：`Avalonia`（XAML 文件为 `*.axaml`，代码后置为 `*.axaml.cs`）。
2. 状态与绑定：`ReactiveUI`（ViewModel 继承 `ReactiveObject`，使用 Source Generators 生成属性/命令）。
3. DI：ViewModel 通常实现 `ITransientDependency`，构造函数通过 `IServiceProvider` 或直接注入服务获取依赖。
4. 命令：优先在 ViewModel 暴露 `ReactiveCommand`，View 通过 `Command="{Binding xxxCommand}"` 调用。
5. 线程规则：
   1) 所有 UI 可见状态（如 `ObservableCollection` / UI 属性集合）更新必须在 UI 线程完成。  
   2) 需要时用 `Dispatcher.UIThread.InvokeAsync` 或 Rx 的 UI 调度器（例如 `RxApp.MainThreadScheduler`）承接 UI 更新。

参考文件：
- `MaterialClient/ViewModels/ViewModelBase.cs`
- `MaterialClient/Views/Controls/SearchableSelectionBox.axaml(.cs)`
- `MaterialClient/Views/Controls/MaterialsSelectionPopup.axaml(.cs)`
- `MaterialClient/Views/Controls/AttendedWeighingDetailView.axaml.cs`

---

## 2. 如何选择要写的 UI 类型

1. `Window`：需要独立窗口生命周期、可 `Show()` / `ShowDialog()` 的页面（例如系统设置、预览、确认输入）。
2. `UserControl`：作为主页面的一块区域/表单/复合控件，嵌入 Window 内（例如 Standard/SolidWaste 表单区域、明细控件）。
3. `Controls/*`：可复用的“部件”，通常包含事件处理与强绑定样式（例如 `SearchableSelectionBox`）。
4. `Views/Dialogs/*`：对话框类 UI（例如 `ConfirmTextDialog`），通常 ViewModel 通过 `Interaction` 调用它。

---

## 3. 标准开发流程（按“新页面/新弹窗”执行）

### 3.1 先对齐需求输入（OpenSpec 侧）

1. 在 OpenSpec 里明确本次变更的目标（功能入口、用户操作、状态/数据流边界）。
2. 若在 Agent 模式下已经产出探索文档，建议在 `openspec/changes/<change-id>/proposal.md` 中显式引用，以便后续实现阶段追溯依据。

（文档关联：`openspec/changes/README.md`、`openspec/project.md`、以及本仓库 UI 相关分析文档如 `docs/popup-selection-analysis.md`。）

### 3.2 设计 ViewModel（先把“状态”和“事件”说清）

1. 列出需要的 UI 状态属性（通常用 `[Reactive]` 字段生成属性）。
2. 列出页面交互命令（通常用 `[ReactiveCommand]` 生成 `xxxCommand`）。
3. 列出异步加载点：
   1) 打开弹窗/进入页面后加载（初始化加载）
   2) 用户输入变化后加载（例如搜索防抖 + 分页）
   3) 选择变化后联动加载（例如选择供应商后刷新材料单位）
4. Rx 订阅策略：
   1) 需要长期订阅时，把订阅放入 `CompositeDisposable` 并在 `Dispose`/`OnDetachedFromVisualTree` 释放（例如 `AttendedWeighingViewModel`）。
   2) 短订阅也尽量使用 DisposeWith 管理生命周期。

参考写法：
- `MaterialClient/ViewModels/ViewModelBase.cs`
- `MaterialClient/ViewModels/MaterialsSelectionPopupViewModel.cs`
- `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs`
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

### 3.3 编写 View（XAML 绑定优先，代码后置尽量少）

1. XAML：
   1) 使用 `x:Class` + `x:DataType` 做强类型绑定（例如 `StandardModeFormView.axaml`）。
   2) 属性通过 `{Binding xxx}` 绑定，命令通过 `{Binding xxxCommand}` 绑定。
   3) DataGrid/列表类：使用 `ItemsSource` 绑定集合、使用 `SelectedItem` 绑定当前选择；双击/选择事件通过 View 触发 ViewModel 命令，以保持 MVVM 边界清晰。
2. 代码后置（`*.axaml.cs`）：
   1) 只做“视图层专属”的事件桥接（例如双击触发命令、注册 Interaction 处理器、窗口拖拽等）
   2) 不要把业务规则写进代码后置

参考写法：
- `MaterialClient/Views/Controls/SearchableSelectionBox.axaml(.cs)`（打开/关闭、点击项确认选择）
- `MaterialClient/Views/Controls/MaterialsSelectionPopup.axaml(.cs)`（双击调用命令）
- `MaterialClient/Views/Controls/AttendedWeighingDetailView.axaml.cs`（Interaction 注册处理器）

### 3.4 处理线程与异步更新（防止 UI 跨线程问题）

1. 异步加载结果回写集合/属性时，务必在 UI 线程更新。
2. 使用 `Dispatcher.UIThread.InvokeAsync` 包裹对集合的清空/填充逻辑（例如材料分页加载）。
3. 与 Rx 结合时，确保链路末端在 UI 调度器上执行（避免在后台线程触发 UI 绑定更新）。

参考写法：
- `MaterialClient/ViewModels/MaterialsSelectionPopupViewModel.cs`（分页加载后 UI 线程更新 `PagedMaterials`）
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`（Rx 链路末端使用 UI 调度/订阅）

### 3.5 弹窗/选择器/对话框三类场景的落地方式

1. 选择器（Search + Pagination + 选择）优先复用 `SearchableSelectionBox`
2. 选择弹窗（通常带 DataGrid + 双击/选择确认）复用对应 Popup 控件与 ViewModel
3. 输入确认对话框（需要输入字符串/确认取消）优先用 `Interaction` + `ConfirmTextDialog`

---

## 4. 当前仓库最常用的 3 个 UI 模式

### 4.1 `SearchableSelectionBox`：选择下拉 + 搜索 + 分页

实现分工：
1. View（`SearchableSelectionBox.axaml/.cs`）负责：
   1) 控制弹出/关闭（`IsDropdownOpen` 双向绑定）
   2) 处理用户点击确认选择（把条目的 `SelectedItem` 写回）
   3) 在打开时触发加载，搜索输入做节流
2. Detail ViewModel（例如 `AttendedWeighingDetailViewModel`）负责：
   1) 提供 `LoadPageAsync` 委托（告诉控件怎么分页查数据）
   2) 提供 `CreateNewAsync` 委托（“新增”需要时）
   3) 订阅选择变化并把选择映射成业务字段（例如 `SelectedProvider` / `SelectedMaterial` 等）

XAML 绑定方式要点（以 SolidWaste 表单为例）：
1. `SelectedItem`：TwoWay 绑定到 Detail VM 的 `SelectedXxxItem`
2. `LoadPageAsync`：绑定到 Detail VM 的 `XxxLoadPageAsync` 委托
3. `CreateNewAsync`：绑定到 Detail VM 的 `XxxCreateNewAsync` 委托（有些选择器关闭创建功能）

---

### 4.2 `MaterialsSelectionPopup`：弹窗内 DataGrid + 双击确认

实现分工：
1. Popup ViewModel（例如 `MaterialsSelectionPopupViewModel`）负责：
   1) 分页/搜索加载（防抖节流 + 异步加载）
   2) 暴露 `SelectedMaterial`（TwoWay 绑定）
   3) 暴露 `SelectMaterialCommand`（双击时触发选择）
2. Popup View（`MaterialsSelectionPopup.axaml/.cs`）负责：
   1) DataGrid 绑定 `ItemsSource` 与 `SelectedItem`
   2) 在双击事件里从 `DataContext` 获取 ViewModel，然后执行 `SelectMaterialCommand.Execute(...)`

XAML 绑定方式要点：
1. `ItemsSource="{Binding PagedMaterials}"`
2. `SelectedItem="{Binding SelectedMaterial, Mode=TwoWay}"`

---

### 4.3 输入确认：`Interaction<ConfirmTextRequest, string?>`

实现分工：
1. ViewModel（例如 `AttendedWeighingDetailViewModel`）：
   1) 声明 `Interaction<ConfirmTextRequest, string?> ConfirmTextInteraction`
   2) 业务上需要确认输入时直接发起 Interaction，并根据返回结果继续
2. View（例如 `AttendedWeighingDetailView.axaml.cs`）：
   1) 在 View 检测到 `DataContext` 可用时调用 `RegisterHandler`
   2) 从 `TopLevel.GetTopLevel(this)` 获取窗口 owner
   3) 显示 `ConfirmTextDialog`，等待 `ShowDialog<string?>`
   4) 把结果通过 `ctx.SetOutput(result)` 回传给 ViewModel

---

## 5. 常见坑与自检清单（在当前实现语境下）

1. UI 跨线程更新导致异常或“偶发不刷新”，自检：所有集合清空/填充、以及 UI 可见属性更新是否都在 UI 线程执行？
2. 长生命周期订阅未释放，自检：任何 `Subscribe(...)` 是否放入 `DisposeWith(...)` 或在控件/VM 生命周期释放？
3. 弹窗“闪一下”或“时序不稳定”（Selection + Refresh 的组合），自检：对 `SelectedItem` 的赋值次数是否在异步加载与恢复选择之间产生了多次触发？相关参考：`docs/popup-selection-analysis.md`。
4. DataTemplate 内命令绑定失效，自检：DataTemplate 内用到的命令，是否需要通过 `$parent[UserControl].DataContext` 找到正确的 ViewModel？
5. 弹窗关闭时机不一致，自检：关闭由 `IsPopupOpen`/`IsDropdownOpen` 驱动，还是由 SelectedItem 变化驱动？两者要统一口径，并避免状态竞争。

---

## 6. 交付物清单（建议写进变更说明）

1. `ViewModel`：状态属性、命令、关键订阅、异步加载入口。
2. `View`：XAML 绑定、必要的 code-behind 桥接事件、弹窗/选择器嵌入方式。
3. 相关文档引用：
   1) 若有 `docs/` 下的探索结论或分析，需在 `proposal.md` 中引用。
   2) 若涉及选择器/弹窗时序问题，建议引用 `docs/popup-selection-analysis.md`。
4. 手工验证点：
   1) 打开弹窗是否能加载与恢复选择。
   2) 搜索/分页是否正确节流与刷新。
   3) 确认输入/取消输入是否返回正确值并触发对应业务路径。

---

## 7. 如何使用图片资源

### 7.1 UI 静态资源（Logo/图标/背景）

1. 把图片放到 `MaterialClient/Assets/` 目录下（例如 `Indexbanner.png`、`fd-ico.ico`、`Car_Default.png`）。
2. 本项目已在 `MaterialClient.csproj` 中配置 `AvaloniaResource Include="Assets\**"`，用于打包为 Avalonia 资源。
3. 在 XAML 中引用使用 `/Assets/<fileName>` 的形式，例如：
   - 窗口图标：`Icon="/Assets/fd-ico.ico"`（见 `Views/*Window.axaml`）
   - 背景图：`<ImageBrush Source="/Assets/Indexbanner.png" ... />`（见 `Views/AttendedWeighing/AttendedWeighingWindow.axaml`）
   - 图片控件：`<Image Source="/Assets/xxx.png" ... />`（见 `Views/AttendedWeighing/AttendedWeighingWindow.axaml`）

### 7.2 运行期图片（现场照片/附件/缩略图/占位图）

运行期照片不建议当作 `Assets` 静态资源，而是通过 ViewModel 提供路径/位图加载。

本仓库常用方式是：在 XAML 中绑定到“路径或 Bitmap”，并用 Converter 统一处理 null/空值/默认图。

1. 当你绑定的是“图片路径字符串”：
   - `NullOrEmptyImageConverter`：当路径是 `"/Assets/..."` 或 `avares://MaterialClient/Assets/...` 时使用 `AssetLoader` 打开；当路径是本地文件则加载绝对路径（见 `MaterialClient/Converters/NullOrEmptyImageConverter.cs`）。
2. 当你绑定的是“车辆照片路径为空时的占位图”：
   - `CarNullOrEmptyImageConverter`：返回 `Car_Default.png`（见 `MaterialClient/Converters/CarNullOrEmptyImageConverter.cs`）。
3. 当你绑定的是“缩略图 Bitmap”：
   - `NullableBitmapToImageConverter`：Bitmap 为 null 时返回默认占位 Bitmap（见 `MaterialClient/Converters/NullableBitmapToImageConverter.cs`）。
4. 参考实现：照片网格
   - `PhotoGridView.axaml` 中通过 `EntryPhoto1Thumbnail` 等 Bitmap 绑定到 `Image.Source`，并使用 `NullableBitmapToImageConverter`（见 `Views/Controls/PhotoGridView.axaml`）。

### 7.3 与资源 URI 的区别（避免路径不一致）

- XAML 静态引用通常用：`/Assets/<name>`。
- Converter/代码中若需要 `AssetLoader.Open(...)`，常用：`avares://MaterialClient/Assets/<name>`（如上述 Converter 内的默认图路径）。

---

## 8. 根据已有截图生成 UI 的操作流程

当你拿到“一张目标界面截图（来自产品/旧版本/现场标注）”时，按下面顺序还原会更稳：

1. 明确范围与交互边界
   - 这张图对应 `Window` 还是 `UserControl` 或 Popup？
   - 哪些区域是纯展示，哪些区域需要输入/选择/列表交互。
2. 分解布局容器（先布局不急着绑定）
   - 复用项目常见骨架：顶部标题栏、卡片式 `Border`、Grid 行列划分、统一边距与圆角。
   - 标注每个区域的“相对位置/尺寸/间距”，先保证结构正确。
3. 从控件识别到 ViewModel 需求
   - 文本/输入框 => 对应 ViewModel 的属性（如 `PlateNumber`、`Remark`）。
   - 下拉 => 对应属性 + `SelectedItem/SelectedValue`。
   - 搜索 + 分页 => 优先考虑 `SearchableSelectionBox`（并在 Detail VM 提供 `LoadPageAsync/CreateNewAsync`）。
   - 列表/明细 => `DataGrid`（`ItemsSource` + `SelectedItem` + 双击/选择命令）。
   - 确认输入对话框 => `Interaction` + `ConfirmTextDialog`。
4. 先把“可见层”跑通（最小闭环）
   - 先完成 XAML 的静态渲染：控件位置、字体大小、颜色、背景图/卡片样式。
   - 再逐步加绑定：先只读展示，再逐步加 TwoWay 输入/SelectedItem。
5. 接入命令与异步逻辑（只在需要处加）
   - 搜索/分页：按项目模式做节流（例如 `Throttle` + UI 线程更新集合）。
   - 弹窗/选择器：关注“打开 -> 加载 -> 恢复选择 -> 回填”的时序稳定性（参考 `docs/popup-selection-analysis.md`）。
6. 图片还原与校验
   - 背景/Logo/图标：用 `/Assets/...` 引用静态资源。
   - 缩略图/占位图：复用 Converter（例如照片网格的 `NullableBitmapToImageConverter`）。
   - 大图查看：走 `ImageViewerWindow` 的模式（缩略图点击走命令，再弹查看窗口）。
7. 逐项对比截图验收
   - 视觉：字体/颜色/间距是否一致。
   - 交互：弹窗能否正确开关、选择能否回填、分页/搜索是否刷新。
   - 空态：无数据时是否显示“未找到/暂无”等提示（如 `SearchableSelectionBox` 的 `NoResultsPanel`）。
8. 文档化输出（可选但推荐）
   - 如果在“截图还原”过程中做了取舍或遇到时序问题，建议把探索结论写到 `docs/<your-topic>.md`，然后在对应 OpenSpec `proposal.md` 里引用，方便后续维护。

