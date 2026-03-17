## Context

有人值守界面（`AttendedWeighingWindow`）顶部蓝色菜单栏目前包含"数据管理"、"系统设置"、"项目信息"、"数据同步"四个按钮。系统已实现 `ISolidWasteExcelExportService` 用于固废运单 Excel 导出，但缺少 UI 入口。

当前系统通过 `SystemSettings.DefaultWeighingMode` 区分标准模式和固废模式，ViewModel 已注入 `ISettingsService` 可获取此配置。

现有对话框模式采用独立 Window + ViewModel 的方式（如 `AddLprDialog`、`SettingsWindow`），通过 `ShowDialog(parentWin)` 模态展示。

`WeighingRecordListView` 中的过滤区域使用 `card-border` 样式的 `Border`、`u:DateTimePicker`（Ursa 控件库）、`TextBox`、`primary-button`/`secondary-button` 按钮样式。

## Goals / Non-Goals

**Goals:**
- 在顶部菜单栏提供"导出"按钮入口，仅固废模式可见
- 提供过滤条件对话框，风格与 WeighingRecordListView 过滤区域一致
- 调用已有的 `ISolidWasteExcelExportService.ExportAsync` 完成导出
- 为标准模式导出预留 TODO 扩展点

**Non-Goals:**
- 不实现标准模式的导出功能（仅添加 TODO）
- 不添加导出进度条（当前数据量不需要）

## Decisions

### Decision 1：导出按钮的模式可见性控制

**方案**: ViewModel 暴露 `IsSolidWasteMode` 布尔属性，XAML 中通过 `IsVisible="{Binding IsSolidWasteMode}"` 控制按钮渲染。

**理由**: 与现有代码风格一致（如 `IsPrinterEnabled` 控制打印机状态面板的显示），简单直接。属性值在 ViewModel 初始化时从 `ISettingsService` 读取 `SystemSettings.DefaultWeighingMode` 获得。

**替代方案**: 使用 Converter 在 XAML 层做枚举到 bool 转换 —— 增加了耦合且现有模式已有 bool 属性的先例。

### Decision 2：对话框实现方式

**方案**: 创建独立的 `ExportFilterDialog`（Window）+ `ExportFilterDialogViewModel`，沿用项目中 `AddLprDialog` 的模态对话框模式。

**理由**:
- 与现有对话框实现方式一致（`AddLprDialog`、`AddCameraDialog`）
- ViewModel 持有过滤条件属性，对话框关闭后通过 ViewModel 读取用户输入
- `ShowDialog(parentWin)` 确保模态交互

### Decision 3：文件保存路径 — 对话框内置路径字段

**方案**: 在导出过滤条件对话框中内置「保存位置」字段 + [浏览] 按钮。不再使用系统 `SaveFilePickerAsync`。

- 「保存位置」字段显示目录路径，[浏览] 按钮通过 `StorageProvider.OpenFolderPickerAsync` 选择目录
- 文件名由系统自动生成：`固废运单_yyyyMMdd_HHmmss.xlsx`，拼接到目录路径后形成完整路径
- 首次使用默认路径：`Environment.GetFolderPath(Environment.SpecialFolder.Desktop)`
- 路径不能为空：点击"导出"时校验，为空则红框提示「请选择保存位置」

**理由**: 用户可直接看到并修改保存位置，不需要每次都与系统文件对话框交互，体验更流畅。

**替代方案**: 使用 `SaveFilePickerAsync` 弹出系统文件选择器 —— 每次都需要重复选择目录和输入文件名，且无法在对话框中直观展示当前路径。

### Decision 6：保存路径记忆

**方案**: 在 `SystemSettings` 中新增 `ExportDefaultPath` 属性（`string`，默认空）。导出成功后将当前目录路径写回 `SystemSettings` 并调用 `SaveSettingsAsync` 持久化。下次打开对话框时读取该值作为默认路径。

**记忆时机**: 仅在导出成功（`ExportResult.Success == true`）后记忆。导出失败或取消不更新路径。

**理由**: 
- 与现有 `SelectedPrinterName` 持久化模式一致
- 成功后记忆确保存储的路径是经过验证的有效路径
- 避免记住一个无效目录导致下次默认值有问题

### Decision 4：对话框过滤条件样式

**方案**: 复用 `WeighingRecordListView` 的 UI 约定：
- 标签：`FontSize="13"` `Foreground="#666"`
- 日期选择：`u:DateTimePicker`（Ursa 控件库），`DisplayFormat="yyyy-MM-dd HH:mm"` 
- 文本输入：`TextBox` `FontSize="13"`（仅车牌号）
- 按钮：`primary-button`（导出）+ `secondary-button`（取消）
- 货名（GoodsName）和发货单位（ProviderName）不在对话框中展示，构建 `SolidWasteExportFilter` 时始终为 null

**理由**: 保持产品内 UI 风格一致性。货名和发货单位暂无过滤需求，简化对话框交互。

### Decision 5：标准模式 TODO 预留

**方案**: 在 ViewModel 的导出命令处理方法中，围绕模式判断添加 `// TODO: 支持标准模式导出`，在 XAML 中的按钮 IsVisible 绑定处也添加注释说明。

### Decision 7：合并接口文件

**方案**: 将 `ISolidWasteExcelExportService` 接口定义移入 `SolidWasteExcelExportService.cs` 同一文件，删除独立的 `ISolidWasteExcelExportService.cs`。

**理由**: 接口仅一个方法、仅一处实现，独立文件增加无谓的导航开销。合并后接口仍保留（ABP DI 基于接口注册），仅物理位置变化。

### Decision 8：上传字段映射

**方案**: `MapToExportRow` 中的三个上传字段不再写死空值，改为从 `Waybill` 实体字段映射：

| Excel 列 | 映射规则 |
|----------|---------|
| 上传结果 | `!IsPendingSync` → `"1"`，否则 `"0"` |
| 上传状态 | `!IsPendingSync` → `"上传成功"`，否则 `"未上传"` |
| 上传时间 | `LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss")` |

**理由**: `Waybill.IsPendingSync` 和 `Waybill.LastSyncTime` 已有完整的同步状态语义，直接映射即可。

## Risks / Trade-offs

- **[风险] 用户输入无效目录路径** → 导出时捕获异常并提示失败，不记忆无效路径
- **[风险] 大量数据导出时 UI 阻塞** → 导出操作在异步方法中执行，不阻塞 UI 线程；当前数据量级不需要进度指示
- **[取舍] 对话框为独立 Window 而非内嵌面板** → 模态窗口更符合"填表-确认-执行"的交互模式，且与现有对话框风格一致
