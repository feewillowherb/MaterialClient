## 1. ViewModel 属性与命令

- [x] 1.1 在 `AttendedWeighingViewModel` 中添加 `IsSolidWasteMode` 布尔属性，初始化时从 `ISettingsService` 读取 `SystemSettings.DefaultWeighingMode` 设置值
- [x] 1.2 在 `AttendedWeighingViewModel` 中添加 `ExportCommand`（ReactiveCommand），绑定导出按钮的点击事件
- [x] 1.3 在 `ExportCommand` 的处理方法中添加 `// TODO: 支持标准模式导出` 注释

## 2. 导出过滤条件对话框

- [x] 2.1 创建 `ExportFilterDialogViewModel`，包含 `StartDate`、`EndDate`（DateTime?）、`PlateNumber`（string?）属性，以及 `ExportCommand` 和 `CancelCommand`（GoodsName 和 ProviderName 不在对话框中展示，构建 Filter 时始终为 null）
- [x] 2.2 创建 `ExportFilterDialog.axaml`（Window），布局包含日期范围（`u:DateTimePicker`）、车牌号（`TextBox`）输入字段，以及"导出"（`primary-button`）/"取消"（`secondary-button`）按钮
- [x] 2.3 确保对话框 UI 样式与 `WeighingRecordListView` 过滤区域一致：标签 `FontSize="13"` `Foreground="#666"`，控件间距和布局对齐

## 3. 导出执行流程

- [x] 3.1 在 `ExportCommand` 处理中，创建 `ExportFilterDialog` 实例并通过 `ShowDialog(parentWin)` 模态展示
- [x] 3.2 ~~对话框确认后，使用 `TopLevel.StorageProvider.SaveFilePickerAsync` 弹出文件保存对话框（默认扩展名 `.xlsx`）~~ → 改为从对话框获取保存路径并拼接自动生成的文件名
- [x] 3.3 用户选择路径后，将 ViewModel 属性映射为 `SolidWasteExportFilter`，调用 `ISolidWasteExcelExportService.ExportAsync` 执行导出
- [x] 3.4 根据 `ExportResult.Success` 显示成功/失败通知（使用现有的 `WindowNotificationManager`）

## 4. XAML 集成

- [x] 4.1 在 `AttendedWeighingWindow.axaml` 的"数据同步"按钮之后添加"导出"按钮，绑定 `ExportCommand`，`IsVisible="{Binding IsSolidWasteMode}"`，样式与其他菜单按钮一致（`transparent-button`、`Foreground="White"`、`Padding="8,4"`）

## 5. 保存路径字段与校验

- [x] 5.1 在 `ExportFilterDialogViewModel` 中添加 `SavePath`（string?）属性和 `SavePathError`（string?）校验提示属性
- [x] 5.2 在 `ExportFilterDialogViewModel` 中添加 `BrowseFolderCommand`，通过 `StorageProvider.OpenFolderPickerAsync` 选择目录
- [x] 5.3 在 `ExportCommand` 中添加路径非空校验：为空时设置 `SavePathError = "请选择保存位置"` 并中止导出
- [x] 5.4 更新 `ExportFilterDialog.axaml`：新增「保存位置」行（TextBox 显示路径 + [浏览] 按钮），路径为空时 TextBox 显示红色边框和提示

## 6. SystemSettings 路径持久化

- [x] 6.1 在 `SystemSettings` 中新增 `ExportDefaultPath` 属性（string，默认空）
- [x] 6.2 对话框初始化时读取 `SystemSettings.ExportDefaultPath`，有值则填入；为空则默认桌面路径
- [x] 6.3 更新 `AttendedWeighingViewModel.ExportSolidWaste`：移除 `SaveFilePickerAsync`，从对话框 ViewModel 获取保存路径并拼接自动生成文件名
- [x] 6.4 导出成功后将路径写入 `SystemSettings.ExportDefaultPath` 并调用 `SaveSettingsAsync`

## 7. 导出服务重构

- [x] 7.1 将 `ISolidWasteExcelExportService` 接口定义移入 `SolidWasteExcelExportService.cs`，删除 `ISolidWasteExcelExportService.cs`
- [x] 7.2 更新 `MapToExportRow`：上传结果 = `!IsPendingSync` → `"1"` / `"0"`
- [x] 7.3 更新 `MapToExportRow`：上传状态 = `!IsPendingSync` → `"上传成功"` / `"未上传"`
- [x] 7.4 更新 `MapToExportRow`：上传时间 = `LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss")` ?? `string.Empty`
