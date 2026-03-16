# 设计：数据管理对话框与固废 Excel 导出对齐（职责分离）

## Context

- **现状**：`DataManagementDialogWindow` 使用 `LedgerRecord` 与 16 列，与 `SolidWasteExcelExportService` 的 17 列不一致；且 `SolidWasteExcelExportService` 内同时包含 Waybill 查询、Provider/Material 映射与 Excel 写入，职责混合，对话框若复用“同一份数据”只能依赖导出服务或重复查询逻辑。
- **目标**：职责分离——**数据**与**写出**分离；固废列表数据由 **SolidWasteService** 作为唯一数据源；Excel 写出由**通用 Excel 导出接口**承担；业务按需暴露不同接口并注入不同数据源。
- **约束**：对话框保持模态、Avalonia 绑定；对外仍保留 `ISolidWasteExcelExportService.ExportAsync(filter, outputPath)` 的调用方式，不破坏现有调用方。**代码约定**：接口与实现放在同一文件中（如 `ISolidWasteService` 与 `SolidWasteService` 同文件，`IExcelExportService` 与实现类同文件）。

## Goals / Non-Goals

**Goals:**

- **SolidWasteService** 作为固废导出行数据的唯一数据源，提供按 `SolidWasteExportFilter` 返回 `SolidWasteExportRow` 列表。
- **通用 Excel 导出**：抽象出与业务无关的“表头 + 行数据写入 .xlsx”的能力，接受列定义与数据源，便于多业务复用。
- **SolidWasteExcelExportService** 改为门面：仅负责固废业务约定（列名、汇总行等），数据来自 SolidWasteService，写出来自通用 Excel 导出。
- 对话框表格列与 17 列一致，数据来自 **SolidWasteService**，导出按钮仍调用 **ISolidWasteExcelExportService**，所见即所得。

**Non-Goals:**

- 不在此变更中实现其他业务的 Excel 导出（仅建立通用接口与固废门面）。
- 不改变现有 `ISolidWasteExcelExportService` 的对外签名（filter + outputPath）。
- 不在此变更中实现服务端分页（可仍为一次性加载当前筛选结果）。

## Decisions

1. **SolidWasteService 职责与接口**
   - **决策**：新增 `ISolidWasteService`（或 `ISolidWasteDataService`），提供如 `Task<IReadOnlyList<SolidWasteExportRow>> GetExportRowsAsync(SolidWasteExportFilter filter)`。实现类内包含当前 `SolidWasteExcelExportService` 中的 Waybill 查询、Provider/Material 字典构建与 `MapToExportRow` 映射逻辑，不再在导出服务内保留这些逻辑。
   - **理由**：唯一数据源，对话框与导出门面均只依赖该服务获取“即将导出的行”，保证一致性与可测试性。
   - **备选**：继续在导出服务内查数据并暴露一个“仅返回行”的方法供对话框用——仍会把数据与写出耦合在一处，不采用。

2. **通用 Excel 导出接口形态**
   - **决策**：引入通用“写 Excel”能力，例如：  
     - 方式 A：`IExcelExportService`，方法如 `Task WriteAsync<T>(string outputPath, string[] headers, IEnumerable<T> rows, Func<T, object?[]?> rowToValues, ...)`，由调用方提供表头、行集合与行到列值的映射。  
     - 方式 B：更底层的 `IExcelWriter` 只负责“按列写入单元格”，业务层（含固废门面）自己遍历行、调用 writer 写每一行，并处理汇总行等。  
     建议采用方式 A 或等效的“表头 + 行枚举 + 行→列值”的泛型接口，以便固废门面只传 `SolidWasteExportRow` 与列映射，汇总行可作为可选回调或由门面在写入前/后单独写入。
   - **理由**：业务无关，可被固废、后续其他导出共用；不同业务暴露不同接口（如 `ISolidWasteExcelExportService`），内部注入 SolidWasteService + 通用 Excel 导出，实现职责分离。
   - **备选**：每个业务单独写 ClosedXML——重复且难以统一格式与行为，不采用。

3. **SolidWasteExcelExportService 重构为门面**
   - **决策**：`SolidWasteExcelExportService` 依赖 `ISolidWasteService` 与通用 Excel 导出（如 `IExcelExportService`）。`ExportAsync(filter, outputPath)` 内部：调用 `ISolidWasteService.GetExportRowsAsync(filter)` 得到行列表；将固废 17 列表头与行→列值映射传给通用导出；如需汇总行，在通用导出支持的前提下写入汇总行，或由门面在写入数据后追加一行。现有 `ExportResult`、错误处理与日志保留在门面内。
   - **理由**：对外 API 不变，调用方无感；数据来源与写出路径清晰，便于测试与维护。
   - **备选**：保留查询逻辑在导出服务内、仅抽一个“GetRows”给对话框用——数据源不唯一，不采用。

4. **对话框数据源与导出**
   - **决策**：对话框（或其 ViewModel）注入 **ISolidWasteService** 与 **ISolidWasteExcelExportService**。查询/加载表格时：用界面上的筛选条件构建 `SolidWasteExportFilter`，调用 `ISolidWasteService.GetExportRowsAsync(filter)`，将结果绑定到 DataGrid。导出按钮：用当前筛选条件构建 filter，调用 `ISolidWasteExcelExportService.ExportAsync(filter, outputPath)`。表格列与 17 列一致，数据与导出均基于同一 SolidWasteService 数据源，所见即所得。
   - **理由**：对话框不直接依赖“导出服务”取数，只依赖“数据服务”取数；导出仍走业务接口，职责清晰。
   - **备选**：对话框只依赖 ISolidWasteExcelExportService 并假设其提供“GetRows”——混淆了数据与导出职责，不采用。

5. **导出按钮位置与测试数据**
   - **决策**：导出按钮仍在对话框底部与“确定”同区；未接入真实数据时可用符合 `SolidWasteExportRow` 的本地测试数据做样式验收。与前一版设计一致。
   - **理由**：UX 与验收需求不变。

6. **服务注册采用集成方式**
   - **决策**：SolidWasteService、通用 Excel 导出、固废 Excel 导出门面的注册 **不** 在各自 Module 的 `ConfigureServices` 中零散添加。参考项目内其他“集成方式”，在 `MaterialClient.Common` 中提供 **扩展方法**（如 `IServiceCollection.AddSolidWasteExportServices()` 或等效命名），在该扩展内集中注册 `ISolidWasteService`、通用 Excel 导出接口/实现、`ISolidWasteExcelExportService`；宿主或主模块（如 `MaterialClientModule`）在配置服务时 **调用该扩展** 完成注册。
   - **理由**：与“其他项目使用集成方式”一致，便于功能边界清晰、测试时可按需挂接同一套注册，避免在 Module 内堆积单服务注册。
   - **备选**：在 `MaterialClientCommonModule.ConfigureServices` 里直接 `services.AddTransient<ISolidWasteService, SolidWasteService>` 等——不符合约定的集成方式，不采用。

## Risks / Trade-offs

- **[Risk]** 通用 Excel 导出接口若设计过窄，后续其他业务（如不同汇总行、多 Sheet）可能需扩展接口。  
  **Mitigation**：优先满足固废 17 列 + 单汇总行；接口可保留扩展点（如可选汇总行回调、可选 Sheet 名），待后续业务再迭代。

- **[Risk]** SolidWasteService 与现有导出服务的查询逻辑迁移时可能遗漏边界条件。  
  **Mitigation**：将现有 `SolidWasteExcelExportService` 内查询与映射逻辑整体迁入 SolidWasteService，导出服务改为纯门面；通过现有导出相关测试或新单测覆盖 SolidWasteService 与门面。

- **[Risk]** 对话框需同时注入 SolidWasteService 与 ISolidWasteExcelExportService，依赖增多。  
  **Mitigation**：职责清晰，文档与命名明确“数据来自 SolidWasteService、导出走 ISolidWasteExcelExportService”。

## Migration Plan

- 新增 SolidWasteService 及通用 Excel 导出接口/实现；将 `SolidWasteExcelExportService` 内查询与映射迁至 SolidWasteService，写入迁至通用导出或门面内对通用导出的调用；保留 `SolidWasteExcelExportService` 类名与 `ISolidWasteExcelExportService` 接口，保证现有调用方无需改签名。
- 对话框改为依赖 SolidWasteService 加载表格、依赖 ISolidWasteExcelExportService 执行导出；列定义与查询区按 17 列与 SolidWasteExportFilter 调整。
- 若有测试或其它代码直接依赖 `SolidWasteExcelExportService` 内部实现（如反射调用查询方法），需改为依赖 SolidWasteService 或门面公开行为。

## Open Questions

- 通用 Excel 导出接口的最终命名与所在程序集：是否统一放在 `MaterialClient.Common/Services/` 下，如 `IExcelExportService`、`ExcelExportService`，或单独 `IExcelWriter` + 扩展方法，由实现时定夺即可。
- 汇总行：由通用导出接口支持“追加一行”的回调/参数，还是由固废门面在调用通用导出写入数据后自行用 ClosedXML 追加一行，二选一即可，以实现简单、易测为准。
