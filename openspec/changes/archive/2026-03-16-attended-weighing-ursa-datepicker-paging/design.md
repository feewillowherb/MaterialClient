## Context

- 数据管理对话框（`DataManagementDialogWindow`）当前用 Avalonia 原生 `DatePicker` 绑定 `StartDate`/`EndDate`，与 `ExportFilterDialog`、`WeighingRecordListView` 使用的 `u:DateTimePicker` 不一致。
- 对话框通过 `BuildFilter()` 构建 `SolidWasteExportFilter`，调用 `ISolidWasteService.GetExportRowsAsync(filter)` 一次性取回全部记录并填入 `Records`，分页按钮无命令绑定，仅 UI 占位。
- Excel 导出在同一个对话框中用同一 `BuildFilter()` 调用 `IExcelExportService.ExportSolidWasteAsync(filter, outputPath)`，导出的是全量匹配数据。
- 项目已在使用 Ursa 的 `u:Pagination`（如 `MaterialsSelectionPopup`、`GenericSelectionPopup`）和 `u:DateTimePicker`，且通过 `Directory.Packages.props` 统一版本。

## Goals / Non-Goals

**Goals:**

- 日期选择统一为 Ursa 组件（仅日期时使用日期格式），与项目其他弹窗一致。
- 对话框支持真实分页：同一 `SolidWasteExportFilter` 下，服务层支持分页查询，页面只加载并展示当前页（如 10 条），分页控件可切换页、跳转、展示总条数/总页数。
- 导出与页面“所见即所得”：导出仍使用当前查询条件，导出结果为该条件下**全部**匹配记录（与当前“全量导出”语义一致）；页面仅按页展示同一数据集，不改变导出接口契约。
- 窗口高度固定为约一页 10 条表格行高度，避免过大或需滚动多页才满屏。

**Non-Goals:**

- 不修改 `SolidWasteExportFilter` 的字段定义；不改变 Excel 导出文件格式或列结构。
- 不新增独立的数据管理“规范”模块；变更限于对话框与 `SolidWasteService` 的分页扩展。

## Decisions

### 1. 日期控件：Ursa DateTimePicker（仅日期）

- **选择**：使用 `u:DateTimePicker`，`DisplayFormat`/`PanelFormat` 设为 `yyyy-MM-dd`，与现有 `ExportFilterDialog` 用法一致，仅不显示时间部分。
- **备选**：继续使用 Avalonia `DatePicker` — 与项目其他 Ursa 风格不统一，故不采用。

### 2. 分页数据来源：服务层分页接口

- **选择**：在 `ISolidWasteService` 上增加分页查询方法（如 `GetPagedExportRowsAsync(filter, pageIndex, pageSize)` 返回当前页数据 + 总条数），对话框仅请求当前页；总条数用于总页数与分页控件。
- **备选**：仍在客户端一次性拉全量再内存分页 — 数据量大时内存与首屏性能差，故不采用。

### 3. 导出与页面数据一致性

- **选择**：导出继续调用现有 `GetExportRowsAsync(filter)`（全量），不传分页参数；页面分页调用新分页接口。二者共用同一 `BuildFilter()`，因此“查询条件一致”；导出结果 = 该条件下全部记录，页面 = 同一条件下的分页视图，语义一致（所见即所得）。
- **备选**：导出“仅当前页” — 与用户对“导出”的常见预期（导出当前条件全部数据）不符，故不采用。

### 4. 分页 UI：Ursa Pagination

- **选择**：底部用 `u:Pagination` 替代当前占位按钮，绑定 `CurrentPage`、`TotalCount`、`PageSize` 及翻页/跳转命令，与 `MaterialsSelectionPopup`/`GenericSelectionPopup` 模式一致。
- **备选**：保留自定义首页/上一页/下一页/末页按钮 — 与项目内已采用的 Ursa 分页不统一，故不采用。

### 5. 窗口高度与每页条数

- **选择**：固定每页 10 条（`PageSize=10`）；窗口高度根据标题栏 + 查询区 + 约 10 行表格行高 + 底部分页/按钮区域计算，设为固定高度（或最小高度），不随数据量变化。
- **备选**：可配置每页条数 — 首版固定 10 条即可，若后续有需求再在 ViewModel 中暴露为可配置。

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| SolidWasteService 当前先查全量再内存过滤 Provider/GoodsName，分页若在 DB 层做需先得到“过滤后”总数 | 分页接口内部复用现有过滤逻辑，先得到过滤后的列表再 skip/take，或改为可排序的 IQueryable 上分页（需评估 EF 与 Waybill 扩展属性查询性能），首版可采用“过滤后列表再分页”以保证与现有导出逻辑一致。 |
| 窗口固定高度在不同 DPI/字体下可能略偏 | 使用与现有表格行高一致的数值，并预留少许边距；若后续反馈再微调数值。 |
| 分页与导出并发时 filter 被修改 | 导出时基于当前 ViewModel 的查询条件重新调用 `BuildFilter()` 传参，只读使用，不共享可变引用。 |

## Migration Plan

- 无数据迁移；仅界面与服务接口增量修改。部署后用户打开数据管理对话框即可使用新日期控件与分页；导出行为不变（仍为全量导出当前条件）。

## Open Questions

- 无。若后续需要“仅导出当前页”选项，可在 spec 中新增需求再扩展。
