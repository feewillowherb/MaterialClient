## Why

在 `AttendedWeighingDetailView` 中，`StandardModeFormView` 和 `SolidWasteModeFormView` 通过 `IsVisible` 切换始终共存于可视化树中。`StandardModeFormView` 声明了 `x:DataType="vm:StandardWeighingDetailViewModel"`，其编译绑定（`IsMaterialPopupOpen`、`MaterialsSelectionPopupViewModel`、`OpenMaterialSelectionCommand`）会在运行时将 DataContext 强制转换为 `StandardWeighingDetailViewModel`。当 SolidWaste 模式激活时，DataContext 实际为 `SolidWasteWeighingDetailViewModel`（与 `StandardWeighingDetailViewModel` 是平级兄弟，均继承自 `AttendedWeighingDetailViewModelBase`），导致 `InvalidCastException`。

## What Changes

- 将 `AttendedWeighingDetailView.axaml` 中的 `Panel` + `IsVisible` 双视图共存模式替换为 `ContentControl` + `DataTemplate` 按类型选择模式，确保运行时仅实例化与当前 DataContext 类型匹配的子视图
- `StandardModeFormView` 和 `SolidWasteModeFormView` 的 `x:DataType` 保持各自的子类类型不变，因为 `DataTemplate` 机制保证 DataContext 类型与声明类型一致

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

- `detail-viewmodel-hierarchy`: 修改 "View DataType compatibility" 需求 — 不再要求所有视图使用基类 DataType，而是通过 `DataTemplate` 类型选择确保类型安全；新增 `DataTemplate` 视图选择机制需求

## Impact

- **XAML 文件**：`AttendedWeighingDetailView.axaml` — 替换 `Panel` 为 `ContentControl` + `DataTemplate`
- **ViewModel 文件**：无变更
- **行为变更**：切换模式时子视图会被销毁/重建（而非隐藏/显示），但当前设计中单次详情展示不会切换模式，因此无功能影响
- **性能**：仅实例化一个子视图而非两个，减少内存占用和绑定开销

### 代码变更表

| 文件路径 | 变更类型 | 变更原因 | 影响范围 |
|---------|---------|---------|---------|
| `Views/Controls/AttendedWeighingDetailView.axaml` | 修改 | 替换 Panel+IsVisible 为 ContentControl+DataTemplate | 详情页视图结构 |
| `openspec/specs/detail-viewmodel-hierarchy/spec.md` | 修改 | 更新 View DataType 兼容性需求，新增 DataTemplate 选择机制 | spec 文档 |
