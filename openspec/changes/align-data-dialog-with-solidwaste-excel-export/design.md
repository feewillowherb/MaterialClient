# 设计：数据管理对话框与固废 Excel 导出对齐（职责分离）

## Context

- **现状**：`DataManagementDialogWindow` 使用 `LedgerRecord` 与 16 列，与 `SolidWasteExcelExportService` 的 17 列不一致；且 `SolidWasteExcelExportService` 内同时包含 Waybill 查询、Provider/Material 映射与 Excel 写入，职责混合，对话框若复用“同一份数据”只能依赖导出服务或重复查询逻辑。
- **目标**：职责分离——**数据**与**写出**分离；固废列表数据由 **SolidWasteService** 作为唯一数据源；**Excel 导出由 ExcelExportService 提供具体业务的导出方法**（不暴露通用 WriteAsync），固废导出逻辑合并进 ExcelExportService。
- **约束**：对话框保持模态、Avalonia 绑定；对外仍保留 `ISolidWasteExcelExportService.ExportAsync(filter, outputPath)` 的调用方式（可由 ExcelExportService 实现该接口），不破坏现有调用方。**代码约定**：接口与实现放在同一文件中（如 `ISolidWasteService` 与 `SolidWasteService` 同文件，`IExcelExportService` 与 `ExcelExportService` 同文件）。

## Goals / Non-Goals

**Goals:**

- **SolidWasteService** 作为固废导出行数据的唯一数据源，提供按 `SolidWasteExportFilter` 返回 `SolidWasteExportRow` 列表。
- **ExcelExportService**：**不提供通用方法**（如 WriteAsync&lt;T&gt;），**仅提供具体业务的导出方法**（如固废运单导出）。通用“表头+行数据写入 .xlsx”的逻辑作为**内部实现**，供各业务导出方法复用；新增业务时在 ExcelExportService 中增加对应导出方法即可。
- **固废导出逻辑合并进 ExcelExportService**：删除独立的 SolidWasteExcelExportService 类；ExcelExportService 实现固废 17 列、汇总行、ExportResult 与错误处理，依赖 ISolidWasteService 取数，并实现 ISolidWasteExcelExportService（或在其接口上暴露 ExportAsync(filter, outputPath)），保证现有调用方无需改签名。
- 对话框表格列与 17 列一致，数据来自 **SolidWasteService**，导出按钮仍调用 **ISolidWasteExcelExportService**，所见即所得。

**Non-Goals:**

- 不在此变更中实现其他业务的 Excel 导出（仅建立“具体业务导出方法”形态与固废导出）。
- 不改变现有 `ISolidWasteExcelExportService` 的对外签名（filter + outputPath）。
- 不在此变更中实现服务端分页（可仍为一次性加载当前筛选结果）。

## Decisions

1. **SolidWasteService 职责与接口**
   - **决策**：新增 `ISolidWasteService`（或 `ISolidWasteDataService`），提供如 `Task<IReadOnlyList<SolidWasteExportRow>> GetExportRowsAsync(SolidWasteExportFilter filter)`。实现类内包含当前 `SolidWasteExcelExportService` 中的 Waybill 查询、Provider/Material 字典构建与 `MapToExportRow` 映射逻辑，不再在导出服务内保留这些逻辑。
   - **理由**：唯一数据源，对话框与导出门面均只依赖该服务获取“即将导出的行”，保证一致性与可测试性。
   - **备选**：继续在导出服务内查数据并暴露一个“仅返回行”的方法供对话框用——仍会把数据与写出耦合在一处，不采用。

2. **ExcelExportService 仅提供具体业务导出方法**
   - **决策**：**不**对外暴露通用“写 Excel”方法（如 `WriteAsync<T>(path, headers, rows, rowToValues, getSummaryRow)`）。**IExcelExportService**（或导出相关接口）仅暴露**具体业务的导出方法**，例如固废运单导出：`Task<ExportResult> ExportSolidWasteAsync(SolidWasteExportFilter filter, string outputPath)`。实现类 **ExcelExportService** 内部保留“表头+行数据写入 .xlsx”的通用逻辑作为 **private** 方法，供 `ExportSolidWasteAsync` 及未来其他业务导出方法复用。
   - **理由**：调用方只依赖业务语义（导出固废、导出标准称重等），无需拼表头与委托；扩展新业务时在 ExcelExportService 增加新方法即可，通用写表逻辑不泄露到接口层。
   - **备选**：保留通用 WriteAsync 并另设门面——已不采用，改为“仅业务方法、通用逻辑内聚”。

3. **固废导出逻辑合并进 ExcelExportService**
   - **决策**：删除独立的 **SolidWasteExcelExportService** 类。**ExcelExportService** 实现固废导出：依赖 **ISolidWasteService** 取数；内含固废 17 列表头、行→列值映射、汇总行规则；内部调用自身 private 的写表逻辑写出 .xlsx；返回 `ExportResult`，含错误处理与日志。为保持现有调用方不变，**ExcelExportService 实现 ISolidWasteExcelExportService**（即提供 `ExportAsync(filter, outputPath)`），或接口合并后保留等效方法名。
   - **理由**：单一导出实现类，对外仅暴露具体业务导出方法；无独立门面类，减少类型与注册。
   - **备选**：保留 SolidWasteExcelExportService 门面——已不采用，改为合并进 ExcelExportService。

4. **对话框数据源与导出**
   - **决策**：对话框（或其 ViewModel）注入 **ISolidWasteService** 与 **ISolidWasteExcelExportService**。查询/加载表格时：用界面上的筛选条件构建 `SolidWasteExportFilter`，调用 `ISolidWasteService.GetExportRowsAsync(filter)`，将结果绑定到 DataGrid。导出按钮：用当前筛选条件构建 filter，调用 `ISolidWasteExcelExportService.ExportAsync(filter, outputPath)`。表格列与 17 列一致，数据与导出均基于同一 SolidWasteService 数据源，所见即所得。
   - **理由**：对话框不直接依赖“导出服务”取数，只依赖“数据服务”取数；导出仍走业务接口，职责清晰。
   - **备选**：对话框只依赖 ISolidWasteExcelExportService 并假设其提供“GetRows”——混淆了数据与导出职责，不采用。

5. **导出按钮位置与测试数据**
   - **决策**：导出按钮仍在对话框底部与“确定”同区；未接入真实数据时可用符合 `SolidWasteExportRow` 的本地测试数据做样式验收。与前一版设计一致。
   - **理由**：UX 与验收需求不变。

6. **服务注册采用 ABP 集成式 + 隐式 + AutoConstructor**
   - **决策**：SolidWasteService、通用 Excel 导出、固废 Excel 导出门面 **不** 使用扩展方法集中注册。各服务实现类实现 **ABP 依赖接口**（如 `ITransientDependency`），并标注 **AutoConstructor**（若有构造注入依赖），由 ABP 按约定扫描并隐式注册；**不** 提供 `AddSolidWasteExportServices` 等扩展方法，**不** 在 `MaterialClientModule` 或其它 Module 的 `ConfigureServices` 中显式注册上述服务。参考项目内惯例（如 `SoundDeviceService` 实现 `ISoundDeviceService, ISingletonDependency` + `[AutoConstructor]`）。
   - **理由**：与项目统一约定一致——服务通过 ABP 依赖接口 + AutoConstructor 由框架自动发现与注册，减少样板代码、避免 Module 或扩展方法堆积单服务注册。
   - **备选**：扩展方法集中注册（如 `AddSolidWasteExportServices()`）——已不采用，改为 ABP 隐式注册。
   - **实施要点**：  
     （1）**删除** `MaterialClient.Common/Services/SolidWasteExportServiceCollectionExtensions.cs`，不再保留扩展方法类。  
     （2）在 **MaterialClientModule.ConfigureServices** 中**移除**对 `AddSolidWasteExportServices()` 的调用（及仅用于该调用的命名空间引用）。  
     （3）**SolidWasteService**、**ExcelExportService** 两个实现类均实现 `Volo.Abp.DependencyInjection.ITransientDependency`；有构造注入依赖的类须标注 `[AutoConstructor]`。**ExcelExportService** 实现 **ISolidWasteExcelExportService**（或等效业务导出接口），不再存在独立的 SolidWasteExcelExportService 类。

## Risks / Trade-offs

- **[Risk]** ExcelExportService 若只支持固废一种业务，后续其他业务（如标准称重导出）需在同类中增加方法，可能使类变大。  
  **Mitigation**：优先满足固废导出；新增业务时在 ExcelExportService 增加对应导出方法，内部复用 private 写表逻辑；若方法数量过多可后续再按领域拆分接口或实现。

- **[Risk]** SolidWasteService 与现有导出服务的查询逻辑迁移时可能遗漏边界条件。  
  **Mitigation**：将现有 `SolidWasteExcelExportService` 内查询与映射逻辑整体迁入 SolidWasteService，固废导出逻辑迁入 ExcelExportService；通过现有导出相关测试或新单测覆盖 SolidWasteService 与 ExcelExportService。

- **[Risk]** 对话框需同时注入 SolidWasteService 与 ISolidWasteExcelExportService，依赖增多。  
  **Mitigation**：职责清晰，文档与命名明确“数据来自 SolidWasteService、导出走 ISolidWasteExcelExportService”。

## Migration Plan

- 新增 SolidWasteService；将原 `SolidWasteExcelExportService` 内查询与映射迁至 SolidWasteService。将固废导出逻辑（17 列表头、行→列值、汇总行、ExportResult、错误处理）迁入 **ExcelExportService**，**ExcelExportService** 实现 **ISolidWasteExcelExportService**（保留 `ExportAsync(filter, outputPath)`），依赖 ISolidWasteService 取数；通用“写表体”逻辑作为 ExcelExportService 内部 private 方法，不对外暴露。**删除** `SolidWasteExcelExportService` 类文件。
- 对话框改为依赖 SolidWasteService 加载表格、依赖 ISolidWasteExcelExportService 执行导出（由 ExcelExportService 实现）；列定义与查询区按 17 列与 SolidWasteExportFilter 调整。
- 若有测试或其它代码直接依赖 `SolidWasteExcelExportService` 类型，改为依赖 **ISolidWasteExcelExportService**（实现类为 ExcelExportService）。

## Open Questions

- 若希望调用方统一通过 IExcelExportService 注入：可在 IExcelExportService 上增加 `ExportSolidWasteAsync`，ExcelExportService 同时实现 IExcelExportService 与 ISolidWasteExcelExportService（ExportAsync 内部转调 ExportSolidWasteAsync），由实现时定夺即可。
