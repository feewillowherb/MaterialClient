## 1. SolidWasteService（唯一数据源）

- [x] 1.1 新增 `ISolidWasteService` 接口（如 `GetExportRowsAsync(SolidWasteExportFilter filter)` 返回 `Task<IReadOnlyList<SolidWasteExportRow>>`），**与实现放在同一文件**（如 `MaterialClient.Common/Services/SolidWasteService.cs`，该文件内同时包含接口与 `SolidWasteService` 实现类）
- [x] 1.2 实现 `SolidWasteService`：将当前 `SolidWasteExcelExportService` 中的 Waybill 查询、Provider/Material 字典构建与 `MapToExportRow` 映射逻辑迁入本服务，无业务逻辑留在导出服务内
- [x] 1.3 在**集成扩展**内注册 SolidWasteService（见 §7）；不在此服务的 Module 中零散注册；并确保现有固废导出相关单测或集成测可覆盖本服务行为

## 2. 通用 Excel 导出

- [x] 2.1 新增通用 Excel 导出接口（如 `IExcelExportService`），接受输出路径、表头数组、行数据枚举及行→列值映射（或等效泛型/委托），不依赖 SolidWaste 等业务类型，**与实现放在同一文件**（如 `MaterialClient.Common/Services/ExcelExportService.cs`，该文件内同时包含接口与实现类），位于 `MaterialClient.Common/Services/`
- [x] 2.2 实现通用 Excel 导出：根据表头与行数据写入 .xlsx，可选支持汇总行或由调用方在写入后追加；与 ClosedXML 等写文件能力对接
- [x] 2.3 在**集成扩展**内注册通用 Excel 导出接口与实现（见 §7），供固废导出门面使用

## 3. 固废 Excel 导出门面重构

- [x] 3.1 将 `SolidWasteExcelExportService` 改为依赖 `ISolidWasteService` 与通用 Excel 导出接口；`ExportAsync(filter, outputPath)` 内部调用 `ISolidWasteService.GetExportRowsAsync(filter)` 取数，再调用通用导出写入 17 列表头与行数据
- [x] 3.2 在门面内保留或调用通用导出后追加固废汇总行（第 1 列为总数，第 6/7/8 列为毛重/皮重/净重之和），保留现有 `ExportResult` 与错误处理、日志
- [x] 3.3 保持 `ISolidWasteExcelExportService` 对外签名不变，现有调用方无需修改

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

## 7. 服务注册（集成方式）

- [x] 7.1 在 `MaterialClient.Common` 中新增服务注册扩展方法（如 `AddSolidWasteExportServices(this IServiceCollection services)`），在扩展内集中注册：`ISolidWasteService`/`SolidWasteService`、通用 Excel 导出接口与实现、`ISolidWasteExcelExportService`/`SolidWasteExcelExportService`；**不在** `MaterialClientCommonModule` 或其它 Module 的 `ConfigureServices` 中零散添加上述服务的注册
- [x] 7.2 在宿主或主模块（如 `MaterialClientModule.ConfigureServices`）中调用上述扩展方法，完成固废导出与数据源相关服务的集成注册
