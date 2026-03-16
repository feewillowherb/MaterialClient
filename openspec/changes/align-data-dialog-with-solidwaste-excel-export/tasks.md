## 1. SolidWasteService（唯一数据源）

- [x] 1.1 新增 `ISolidWasteService` 接口（如 `GetExportRowsAsync(SolidWasteExportFilter filter)` 返回 `Task<IReadOnlyList<SolidWasteExportRow>>`），**与实现放在同一文件**（如 `MaterialClient.Common/Services/SolidWasteService.cs`，该文件内同时包含接口与 `SolidWasteService` 实现类）
- [x] 1.2 实现 `SolidWasteService`：将当前 `SolidWasteExcelExportService` 中的 Waybill 查询、Provider/Material 字典构建与 `MapToExportRow` 映射逻辑迁入本服务，无业务逻辑留在导出服务内
- [x] 1.3 SolidWasteService 实现 **ITransientDependency** 并标注 **AutoConstructor**，由 ABP 隐式注册（见 §7）；并确保现有固废导出相关单测或集成测可覆盖本服务行为

## 2. ExcelExportService（仅具体业务导出方法 + 合并固废逻辑）

- [x] 2.1 **不**对外暴露通用 WriteAsync&lt;T&gt;；**IExcelExportService**（或导出相关接口）仅暴露具体业务导出方法。**ExcelExportService** 实现 **ISolidWasteExcelExportService**（保留 `ExportAsync(filter, outputPath)`），与实现放在同一文件（如 `MaterialClient.Common/Services/ExcelExportService.cs`），位于 `MaterialClient.Common/Services/`
- [x] 2.2 将“表头+行数据写入 .xlsx”的通用逻辑作为 ExcelExportService 的 **private** 方法实现（与 ClosedXML 对接，支持汇总行），供业务导出方法内部复用；**不**在接口上暴露
- [x] 2.3 将固废导出逻辑（17 列表头、行→列值映射、汇总行规则、`ExportResult`、错误处理与日志）自 **SolidWasteExcelExportService** 迁入 **ExcelExportService**；`ExportAsync(filter, outputPath)` 内部依赖 **ISolidWasteService** 取数，再调用上述 private 写表方法完成导出
- [x] 2.4 **删除** `MaterialClient.Common/Services/SolidWasteExcelExportService.cs` 文件；确认所有引用 **ISolidWasteExcelExportService** 的调用方无需改签名（实现类改为 ExcelExportService）
- [x] 2.5 ExcelExportService 实现 **ITransientDependency** 并标注 **AutoConstructor**（依赖 ISolidWasteService、ILogger 等），由 ABP 隐式注册（见 §7）

## 3. 固废导出接口与调用方一致性

- [x] 3.1 保持 `ISolidWasteExcelExportService` 对外签名不变（`ExportAsync(SolidWasteExportFilter filter, string outputPath)`），现有调用方（对话框、ViewModel、AttendedWeighingWindow）仍注入 **ISolidWasteExcelExportService**，仅实现类由 SolidWasteExcelExportService 变为 ExcelExportService

## 4. 数据管理对话框数据源与查询

- [x] 4.1 将对话框表格数据源从 `LedgerRecord` 改为与 `SolidWasteExportRow` 一致（如 `ObservableCollection<SolidWasteExportRow>`），并确保 DataContext/ViewModel 可绑定
- [x] 4.2 在对话框或 ViewModel 中注入 **ISolidWasteService**；查询/加载时根据界面上起止日期、车牌号、货名、发货单位构建 `SolidWasteExportFilter`，调用 `ISolidWasteService.GetExportRowsAsync(filter)` 并将结果绑定到表格
- [x] 4.3 将查询区控件与 `SolidWasteExportFilter` 对齐（起止日期、车牌号、货名、发货单位），并绑定到 ViewModel 或代码-behind 属性，供“查询”与“导出”使用

## 5. 表格列与导出按钮

- [x] 5.1 将 `DataManagementDialogWindow.axaml` 中 DataGrid 的列定义改为 17 列，列头及顺序与固废导出一致；Binding 指向 `SolidWasteExportRow` 对应属性，数值列按需格式化
- [x] 5.2 在对话框底部增加“导出”按钮；点击时用当前筛选条件构建 `SolidWasteExportFilter`，通过“另存为”或默认路径取得路径，调用 `ISolidWasteExcelExportService.ExportAsync`，根据 `ExportResult.Success` 显示成功或失败提示
- [x] 5.3 确保打开对话框时能注入 **ISolidWasteService** 与 **ISolidWasteExcelExportService**（通过构造函数或父 ViewModel 传入），并在加载数据与导出时正确使用

## 6. 测试数据与验收

- [x] 6.1 在未接入真实查询时，可使用符合 `SolidWasteExportRow` 的本地测试数据填充表格做 17 列样式验收；接入 SolidWasteService 后由查询结果替换

## 7. 服务注册（ABP 集成式 + 隐式 + AutoConstructor）

- [x] 7.1 **SolidWasteService**、**ExcelExportService** 均实现 **ITransientDependency** 并标注 **AutoConstructor**（有构造注入时），由 ABP 按约定扫描隐式注册；**不** 新增扩展方法、**不** 在 `MaterialClientModule` 或其它 Module 的 `ConfigureServices` 中显式注册上述服务（**不再存在** SolidWasteExcelExportService 独立类）
- [x] 7.2 **移除** `MaterialClient.Common/Services/SolidWasteExportServiceCollectionExtensions.cs` 文件；在 `MaterialClientModule.ConfigureServices` 中**移除**对 `AddSolidWasteExportServices()` 的调用（及仅用于该调用的 using）
