## Why

当前实收皮重业务中，固废过磅数据的接口返回格式与项目中其他基于 ABP 的分页接口不一致，导致前端在展示固废数据列表时需要做额外适配，分页信息也不够清晰。同时，`DataManagementDialogWindow` 中与固废数据相关的筛选和表格交互存在一定的可用性问题：日期筛选控件宽度不足，无法完整显示日期文本，列表列宽也无法由用户自行调整，影响操作效率和用户体验。

## What Changes

- 将 `SolidWasteService` 中相关接口的返回结果调整为 ABP 标准的分页返回格式，统一分页字段结构，便于前后端对齐和复用。
- 更新固废数据管理对话框 `DataManagementDialogWindow` 的筛选区域中日期控件布局与宽度设置，使日期文本能够完整展示，不再被截断。
- 调整 `DataManagementDialogWindow` 中数据表格的列定义，使用户可以自行拖拽调整各列宽度，并在默认情况下提供更合理的初始列宽。

## Capabilities

### New Capabilities
- `solidwaste-datamanagement-grid-improvements`: 提供统一的固废分页数据接口契约，并改进固废数据管理对话框中的筛选日期显示与表格列宽可调体验。

### Modified Capabilities
- `attended-weighing-solidwaste-listing`: 调整固废列表展示相关的交互需求，包括日期筛选可读性和列表列宽可调整性。

## Impact

- 受影响代码：
  - 后端：`MaterialClient.Common/Services/SolidWasteService.cs` 中固废分页查询相关方法的返回类型与数据结构。
  - 前端 UI：`MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml` 中筛选区域日期控件布局以及数据表格列定义。
- 受影响接口：
  - 与固废分页查询相关的 API/应用服务方法，前端调用方需要适配 ABP 标准分页返回结构（如 `totalCount`、`items` 等）。
- 受影响系统/模块：
  - 实收皮重业务中的固废数据管理功能。
  - 可能影响依赖固废分页数据的其他导出、统计或报表功能，需要在设计阶段进一步确认兼容性。

