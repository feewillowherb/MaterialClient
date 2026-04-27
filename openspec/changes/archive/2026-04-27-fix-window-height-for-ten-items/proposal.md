## Why

三个管理窗口（台账管理、材料管理、供应商管理）当前使用 `SizeToContent="Height"` 使窗口高度随内容动态增长。当列表项较多时窗口会变得过高，当项目较少时高度不一致，用户体验不佳。需要将窗口高度固定为可在一页内完整显示十个项目列表的值，保持界面一致性并减少滚动操作。

## What Changes

- 移除 `DataManagementDialogWindow.axaml` 上的 `SizeToContent="Height"`，设置固定 `Height` 值
- 移除 `MaterialManagementWindow.axaml` 上的 `SizeToContent="Height"`，设置固定 `Height` 值
- 移除 `ProviderManagementWindow.axaml` 上的 `SizeToContent="Height"`，设置固定 `Height` 值
- 三个窗口的 DataGrid 均保留现有滚动行为，确保超过十个项目时可通过滚动查看

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

（此变更为纯 UI 布局调整，不涉及现有 spec 层面的行为变更，仅需修改 XAML 属性值）

## Impact

- **受影响文件**：
  - `MaterialClient/Views/AttendedWeighing/DataManagementDialogWindow.axaml`
  - `MaterialClient/Views/AttendedWeighing/MaterialManagementWindow.axaml`
  - `MaterialClient/Views/AttendedWeighing/ProviderManagementWindow.axaml`
- **变更范围**：仅涉及 XAML Window 元素的属性调整（移除 `SizeToContent="Height"`，添加固定 `Height`），不影响 ViewModel、业务逻辑或 API
- **依赖**：无新依赖引入
