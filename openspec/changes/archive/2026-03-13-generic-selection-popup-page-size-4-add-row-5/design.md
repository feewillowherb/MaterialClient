## Context

- **现状**：`GenericSelectionPopup` 使用 `GenericSelectionPopupViewModel`，默认 `PageSize = 10`；布局为三行：Row 0 = 列表或空状态、Row 1 = 有结果且可新增时的「新增」按钮（独立一行）、Row 2 = 分页。DataGrid 高度 200、行高 30，视觉上约 6～7 行，与分页条数不一致。
- **约束**：需保持 `IGenericSelectionPopupBindings` 与现有调用方兼容；ClientSide/ServerSide 两种分页模式均适用。
- **相关方**：使用该弹窗的 AttendedWeighingDetailViewModel（镇街/材料/供应商等）、MaterialsSelectionPopup 等。

## Goals / Non-Goals

**Goals:**

- 分页大小固定为 4，即每页最多 4 条数据。
- 列表区域固定为「4 行数据 + 第 5 行为新增按钮」（当可新增且应显示时）；有结果时新增按钮与列表在同一视觉块内，占第 5 行，不再使用当前 Row 1 的独立「新增」行。
- 无结果时保持「未找到匹配结果」+ 新增按钮的居中空状态不变。

**Non-Goals:**

- 不改变搜索、过滤、新增逻辑；不改变 ShowAddNewButton / ShowNoResultsMessage 等计算规则。
- 不增加新接口或破坏现有 ViewModel 接口。

## Decisions

### 1. 分页大小 4 的设定位置

- **选择**：在 **GenericSelectionPopupViewModel** 中将默认 `DefaultPageSize` 从 10 改为 4；现有调用方未传 `pageSize` 的继续使用默认值，即自动变为 4。若有调用方显式传入 `pageSize`，则尊重传入值（本次不要求所有调用方都改为 4，但默认 4 可覆盖绝大多数使用该通用弹窗的场景）。
- **理由**：通用选择弹窗的 UI 设计为 5 行（4 数据 + 1 按钮），默认与 UI 一致更合理；改动一处即可生效，避免遗漏调用点。
- **备选**：仅在创建 VM 的每个调用处传入 `pageSize: 4` — 调用点分散，易漏；不采用。

### 2. 列表区域布局：4 行 DataGrid + 第 5 行新增按钮

- **选择**：在 **GenericSelectionPopup.axaml** 中，将 Row 0 的「列表区域」改为内部子布局：上方为 DataGrid，固定总高度需能容纳列头 + 4 行数据（见决策 3）；下方为第 5 行，当 `ShowAddNewButtonBelowList` 为 true 时显示「新增」按钮（仍复用现有绑定 `ShowAddNewButtonBelowList`、`AddNewButtonText`、`AddNewItemCommand`），无结果时 Row 0 仍为居中「未找到匹配结果」+ 新增。移除当前 **Row 1**（原「有结果时新增按钮独立一行」）的 Border/Button，即新增按钮不再单独占一行在列表外，而是与列表同属 Row 0 块内的第 5 行。
- **理由**：满足「第 5 行留给新增按钮」且与 4 条数据行形成固定 5 行布局；与现有 ViewModel 属性兼容，仅布局调整。
- **备选**：保留 Row 1 但把 Row 0 的 DataGrid 高度改为 120 — 仍有两行结构，不符合「第 5 行」在同一块内的表述；不采用。

### 3. DataGrid 高度与行高

- **选择**：DataGrid 行高保持 30；**DataGrid 总高度（Height）须包含列头行**，且列头高度由主题决定（当前 Fluent 主题下 DataGridColumnHeader 实际为 **36**，非 30）。故总高度设为 **156**（列头 36 + 4×30 数据），使 4 条数据行区域恰好 120px、无纵向溢出，从而不出现滚动条、不触发滚轮；第 5 行（新增按钮行）高度可与行高一致或略大（如 36），由布局决定。内层 Grid 第一行高度与 DataGrid 一致为 156。
- **理由**：若按列头 30 设总高 150，则数据行区仅 150−36=114，缺 6px 导致内部出现纵向滚动并响应滚轮。156 可保证数据行区≥120，布局刚好容纳内容。
- **不采用**：通过禁用垂直滚动（如 VerticalScrollBarVisibility="Disabled"）来避免滚轮响应；目标为**布局刚好容纳内容、不产生纵向溢出**，从而不触发滚动条与滚轮，而非禁用滚动。

## Risks / Trade-offs

- **[默认 4 影响其他未传 pageSize 的弹窗]** 若存在依赖「每页 10 条」的调用方，改为默认 4 后每页条数会减少。**缓解**：通用弹窗当前主要用于物料/供应商/镇街等，均为少量选择，4 条一页可接受；若有特殊场景需 10 条，可在该调用处显式传入 `pageSize: 10`。
- **[MaterialsSelectionPopupViewModel 等子类]** MaterialsSelectionPopupViewModel 有自有 `PageSize` 与 `DefaultPageSize`，若其 UI 也使用 GenericSelectionPopup 且希望 4+1 布局，需在子类或创建处统一为 4。**缓解**：设计阶段确认 MaterialsSelectionPopup 是否使用同一 GenericSelectionPopup 视图；若共用，则子类默认也应改为 4 或由构造函数传入 4。

## Migration Plan

- 仅前端布局与默认参数变更：更新 `GenericSelectionPopupViewModel.DefaultPageSize`、`GenericSelectionPopup.axaml` 布局；若有子类或独立弹窗（如 MaterialsSelectionPopup）使用不同默认，则单独改为 4 或传入 4。无数据迁移、无部署步骤。发布即生效；回滚则还原相关提交即可。

## Open Questions

- 无。若后续有弹窗需要「每页 10 条」且不采用 5 行布局，可在该处显式传 `pageSize: 10` 并在视图上允许更多行（或单独视图变体）。
