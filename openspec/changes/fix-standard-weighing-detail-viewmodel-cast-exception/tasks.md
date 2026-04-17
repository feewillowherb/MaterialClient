## 1. XAML 视图结构修改

- [x] 1.1 在 `AttendedWeighingDetailView.axaml` 中将 `Panel`（Grid.Row="1"）替换为 `ContentControl`，设置 `Content="{Binding}"`
- [x] 1.2 在 `ContentControl.DataTemplates` 中添加 `StandardWeighingDetailViewModel` 类型的 DataTemplate，内容为 `aw:StandardModeFormView`
- [x] 1.3 在 `ContentControl.DataTemplates` 中添加 `SolidWasteWeighingDetailViewModel` 类型的 DataTemplate，内容为 `aw:SolidWasteModeFormView`
- [x] 1.4 移除原来 `Panel` 内的两个子视图及其 `IsVisible` 绑定

## 2. Spec 文档更新

- [x] 2.1 更新 `openspec/specs/detail-viewmodel-hierarchy/spec.md` 中 "View DataType compatibility" 需求，反映 DataTemplate 选择机制
