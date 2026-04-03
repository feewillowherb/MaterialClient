# MaterialClient 视觉契约指南

**读者**：熟悉 HTML/CSS 的传统 BS（浏览器端）前端开发者，需要把页面思路迁移到本仓库的桌面端实现。  
**目标**：以 **HTML 为语义基线**、**渲染图为视觉基线**，与 AI/协作者约定「输入—输出」，从而稳定生成或评审 `MaterialClient` 页面（Avalonia XAML + ReactiveUI）。

> 实现细节与日常开发顺序见：`docs/ui-development-flow.md`。

---

## 1. 先建立心智模型：不是浏览器，而是桌面 MVVM

| BS 前端 | MaterialClient |
|--------|----------------|
| 浏览器渲染 DOM | Avalonia 可视化树（`*.axaml` 声明 UI） |
| JS 改 DOM / React 状态 | ViewModel（`ReactiveObject`）+ 绑定 |
| CSS 选择器 / 类名 | `Style` / `StyleInclude` / 控件 `Classes` / 资源画刷 |
| `fetch` / Axios | 注入的服务、异步方法、`ReactiveCommand` |
| 路由（SPA） | 多 `Window`、或主窗口内切换 `UserControl` / `ViewLocator` |

**契约含义**：HTML 描述的是 **结构与语义**（区块、表单字段、列表），截图描述的是 **像素级视觉**（间距、字号、颜色、对齐）。二者一起构成「视觉契约」；单独 HTML 或单独截图都不足以唯一确定实现，但合在一起足够让 AI 按本仓库惯例产出初稿。

---

## 2. HTML 标签 → Avalonia 控件映射（常用）

以下映射面向「从页面结构反推控件类型」，便于你把习惯中的标签名换成桌面控件名。

| HTML / 常见模式 | MaterialClient 中优先选用 | 备注 |
|------------------|---------------------------|------|
| `<div>` 纵向堆叠 | `StackPanel`（`Orientation="Vertical"`，`Spacing`） | 类似 flex column + gap |
| `<div>` 横向或工具栏 | `StackPanel`（`Horizontal`）或 `DockPanel` | 顶栏/底栏可用 `DockPanel` |
| `<div>` 二维分区 | `Grid` + `RowDefinitions` / `ColumnDefinitions` | 最接近 CSS Grid |
| `<span>` / 纯文本 | `TextBlock` | 只读文本 |
| `<label>` + `<input type="text">` | `Label` + `TextBox` | 两列布局常用外层 `Grid` |
| `<select>` | `ComboBox` | 选项常绑定到集合 + `ItemTemplate` |
| `<button>` | `Button`，主操作可加 `Classes="primary-button"` 等（见 `App.axaml`） | 命令：`Command="{Binding ...}"` |
| `<table>` / 数据表 | `DataGrid` | 项目已引用 Fluent DataGrid 主题 |
| `<img>` 静态资源 | `Image`，`Source="/Assets/xxx.png"` | 资源放在 `MaterialClient/Assets/` |
| `<img>` 运行期路径 | `Image` + 转换器绑定 | 见 `NullOrEmptyImageConverter` 等 |
| 卡片 / 面板 | `Border`（`CornerRadius`、`Padding`、`Background`、`BorderBrush`） | 对应「卡片」视觉 |
| 模态框 | 独立 `Window` 或 `Flyout` / 自定义 Popup | 与 Web `dialog` 类似但生命周期在窗口层 |

**搜索 + 分页下拉、材料弹窗** 等本仓库已有模式，不要在契约里发明新名词：在 HTML 草图中用注释标明「`SearchableSelectionBox`」或「材料选择弹窗」，便于直接对齐 `docs/ui-development-flow.md` 第 4 节。

---

## 3. CSS 概念 → Avalonia 样式与资源

BS 开发者习惯用 CSS 控制外观；在 MaterialClient 中对应关系大致如下。

| CSS / 设计概念 | Avalonia 侧 |
|----------------|------------|
| `color`, `background` | `Foreground`、`Background`、`BorderBrush`；或引用 `App.axaml` 中 `SolidColorBrush` 资源（如 `PrimaryBlue`、`TextPrimary`） |
| `padding`, `margin` | 控件的 `Padding`、`Margin`；容器常用统一 `Spacing`（`StackPanel`） |
| `border-radius` | `Border.CornerRadius` |
| `font-size` | `FontSize`（`TextBlock`、`TextBox` 等） |
| `width: 100%` | `HorizontalAlignment="Stretch"` 或 `*` 列 / `MinWidth` |
| `flex: 1` | `Grid` 中 `*` 行高 / 列宽，或 `VerticalAlignment="Stretch"` |
| 主题 / 暗色 | `Application.RequestedThemeVariant`；本仓库默认 `Light` |

**契约建议**：在 HTML 旁用 **内联注释或独立设计说明** 写出主色、圆角、间距阶梯（例如 8/16/24）。若与全局资源不一致，截图仍是最终视觉依据。

---

## 4. 「视觉契约」交付物（给 AI 或人工实现）

为了一次性生成可用的 `Window` / `UserControl` + ViewModel 骨架，建议按下面清单提供材料。

### 4.1 必带

1. **HTML 基线（单文件即可）**
   - 语义清晰：区块用注释标出「标题区 / 表单区 / 列表区 / 底部按钮区」。
   - 每个表单字段：`name` 或 `data-field` 与业务含义一致（如 `plateNumber`、`provider`），便于映射到 ViewModel 属性名。
   - 列表/表格：表头与列含义写清；若需双击行为，用 HTML 注释说明。

2. **渲染图（PNG/JPG）**
   - 与 HTML 描述同一状态（例如「编辑中」「空列表」）。
   - 分辨率足够辨认边距与字号；若有响应式多状态，可多张图并标注断点说明（桌面端通常单一布局即可）。

### 4.2 强烈建议

3. **交互说明（短列表即可）**
   - 哪些字段只读、哪些可编辑；主按钮与次要按钮；是否需要确认弹窗、搜索分页。
4. **与现有页面对齐的说明**
   - 例如：「供应商选择与有人值守页一致」→ 实现时复用 `SearchableSelectionBox` 模式。

### 4.3 AI / 实现者输出约定

- **View**：`*.axaml`，含 `x:DataType`、主要 `{Binding}`。
- **ViewModel**：属性、`ReactiveCommand`、必要的 `Load`/选择逻辑占位。
- **资源**：静态图放入 `Assets/` 并在 XAML 用 `/Assets/...` 引用。
- **不凭空引入**：未在契约中出现的业务接口；占位服务接口需在注释或 OpenSpec 中说明。

---

## 5. 从 HTML+截图到 MaterialClient 的典型推导步骤（AI 可照此执行）

1. **定边界**：整页 = `Window` 还是嵌入 `UserControl`？是否需要新 `Window` 或仅主窗口内一块区域？
2. **拆布局**：用 `Grid`/`StackPanel`/`Border` 复现截图中的区域比例；先对齐间距与对齐方式。
3. **控件落地**：按第 2 节映射表替换标签；列表用 `DataGrid`，表单用标签+输入两列 `Grid`。
4. **绑定与命令**：为每个 `data-field` 命名 ViewModel 属性；按钮绑定 `Command`。
5. **视觉校准**：对照截图调整 `FontSize`、`Margin`、画刷；优先复用 `App.axaml` 已有画刷与全局 `TextBox`/`ComboBox` 样式。
6. **仓库模式复用**：搜索分页、材料弹窗、确认输入等严格按 `docs/ui-development-flow.md` 已有模式接入，避免重复造轮子。

---

## 6. BS 与 MaterialClient 的常见错位（契约里应提前声明）

1. **没有「像素完美 CSS」**：Avalonia 字体度量与浏览器略有差异，以截图为准做微调即可。
2. **双向绑定与更新线程**：VM 更新 UI 集合须在 UI 线程；异步加载后回写需注意（见 `ui-development-flow` 第 4 节）。
3. **DataTemplate 内命令**：嵌套模板里绑定父级命令时，可能需要 `$parent` 等技巧，契约中若结构极深应标注「命令触达路径」。
4. **弹窗时序**：打开即加载、恢复选中项等需与现有 `Interaction`/Popup 模式一致，见 `docs/popup-selection-analysis.md`（若流程复杂）。

---

## 7. 最小 HTML 契约示例（片段）

下面仅作「字段命名 + 结构」示范；真实项目请补全整页与截图。

```html
<!-- MaterialClient visual contract: AttendedWeighing / example -->
<main data-view="StandardModeForm">
  <section data-region="form-card">
    <!-- field: PlateNumber -> VM: PlateNumber -->
    <div class="field" data-field="plateNumber">
      <label>车牌号</label>
      <input type="text" />
    </div>
    <!-- field: Provider -> VM: SearchableSelectionBox + LoadPageAsync -->
    <div class="field" data-field="provider" data-widget="SearchableSelectionBox">
      <label>供应商</label>
      <!-- ... -->
    </div>
  </section>
</main>
```

---

## 8. 相关文档

| 文档 | 用途 |
|------|------|
| `docs/ui-development-flow.md` | Avalonia + ReactiveUI 开发流程、控件模式、图片与 Converter |
| `MaterialClient/App.axaml` | 全局画刷、控件默认样式、`Classes` 约定 |
| `openspec/project.md` | 项目与 OpenSpec 协作方式（若变更需入 OpenSpec） |

---

**版本说明**：本文聚焦「HTML/截图 → MaterialClient」的**契约与映射**；具体类名与目录以仓库当前结构为准，若框架升级请同步更新第 2、3 节映射表。
