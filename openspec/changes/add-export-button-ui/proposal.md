## Why

有人值守界面（AttendedWeighingWindow）已有固废运单 Excel 导出服务（`ISolidWasteExcelExportService`），但缺少用户触发导出的 UI 入口。用户需要在顶部菜单栏直接点击按钮、填写过滤条件后导出 Excel 文件。

## What Changes

- 在 `AttendedWeighingWindow.axaml` 顶部菜单栏"数据同步"按钮后方新增"导出"按钮
- 该按钮仅在固废模式（`WeighingMode.SolidWaste`）下渲染，标准模式不显示
- 在代码中添加 `TODO` 标记，为未来标准模式导出功能预留扩展点
- 点击"导出"按钮弹出一个过滤条件对话框（Dialog/Window），包含以下输入字段：
  - 日期范围（StartDate / EndDate）— 使用 `u:DateTimePicker`
  - 车牌号（PlateNumber）— TextBox
  - 货名（GoodsName）和发货单位（ProviderName）不在对话框中展示，始终以 null 传入导出服务
- 对话框新增「保存位置」字段 + [浏览] 按钮，用于选择导出目录（不再弹出系统文件选择器）
  - 保存路径不能为空，点击"导出"时校验，为空则红框提示
  - 文件名自动生成：`固废运单_yyyyMMdd_HHmmss.xlsx`
  - 首次使用默认路径为桌面（`Environment.GetFolderPath(Desktop)`）
- 导出成功后将保存路径持久化到 `SystemSettings.ExportDefaultPath`，下次打开对话框自动填充
- 对话框的过滤条件 UI 样式与 `WeighingRecordListView.axaml` 中的搜索区域保持一致（`card-border`、`FontSize="13"`、`Foreground="#666"` 标签、`primary-button`/`secondary-button` 按钮样式）
- 用户确认后调用 `ISolidWasteExcelExportService.ExportAsync` 执行导出
- 导出完成后显示成功/失败提示
- 将 `ISolidWasteExcelExportService` 接口合并到 `SolidWasteExcelExportService.cs` 中，删除独立的接口文件
- 补全导出的上传字段映射（上传结果、上传状态、上传时间），数据来源为 `Waybill.IsPendingSync` 和 `Waybill.LastSyncTime`

## Capabilities

### New Capabilities
- `export-button-ui`: 有人值守界面的导出按钮及过滤条件对话框

### Modified Capabilities

## Impact

- `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml` — 新增导出按钮
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` — 新增导出命令和对话框交互逻辑
- 新增 `ExportFilterDialog.axaml` + `ExportFilterDialogViewModel.cs` — 过滤条件对话框
- `MaterialClient.Common/Services/SolidWasteExcelExportService.cs` — 合并接口 + 补全上传字段映射
- 删除 `MaterialClient.Common/Services/ISolidWasteExcelExportService.cs`
- 依赖已有的 `SolidWasteExportFilter`
- 依赖 `SystemSettings.DefaultWeighingMode` 判断当前称重模式
- `MaterialClient.Common/Configuration/SystemSettings.cs` — 新增 `ExportDefaultPath` 属性
