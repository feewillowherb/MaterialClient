## Why

当前 DataManagementDialogWindow 仅服务于固废模式，展示 17 列固废专用字段（毛重/皮重/联单编号/上传状态等）。标准模式使用完全不同的数据列集（运单数量、扣量、单位换算等），缺少对应的数据管理界面，导致标准模式用户无法查看台账数据。

## What Changes

- 新增标准模式专用的 DataManagementDialogWindow（独立 AXAML 文件 + code-behind + ViewModel），展示 15 列标准模式专用字段
- 新增 `StandardExportRow` DTO，映射 Waybill 实体字段到标准模式展示列
- 新增标准模式分页查询服务方法（基于现有 Repository 模式）
- 标准模式版本不包含 CSV/Excel 导出按钮
- 在 `AttendedWeighingWindow` 中根据当前 `WeighingMode` 决定打开哪种台账对话框

## Capabilities

### New Capabilities
- `standard-data-management-dialog`: 标准模式台账管理对话框的完整 UI 布局、数据列定义、查询筛选、分页交互和 ViewModel 逻辑
- `standard-waybill-paged-query`: 标准模式运单分页查询服务，支持按车牌号、类型、商品、状态、日期范围筛选

### Modified Capabilities
- `attended-weighing`: `AttendedWeighingWindow` 的"台账管理"入口需根据 WeighingMode 路由到不同的对话框

## Impact

| 文件路径 | 变更类型 | 变更原因 | 影响范围 |
|---------|---------|---------|---------|
| `Views/AttendedWeighing/StandardDataManagementDialogWindow.axaml` | 新增 | 标准模式台账对话框视图 | AttendedWeighing 模块 |
| `Views/AttendedWeighing/StandardDataManagementDialogWindow.axaml.cs` | 新增 | code-behind 订阅 ViewModel 命令 | AttendedWeighing 模块 |
| `ViewModels/StandardDataManagementDialogViewModel.cs` | 新增 | 标准模式台账 ViewModel | ViewModel 层 |
| `Common/Models/StandardExportRow.cs` | 新增 | 标准模式导出行 DTO | Models 层 |
| `Views/AttendedWeighing/AttendedWeighingWindow.axaml.cs` | 修改 | 台账管理入口路由逻辑 | AttendedWeighing 模块 |
