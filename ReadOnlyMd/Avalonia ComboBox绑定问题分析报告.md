# Avalonia ComboBox 绑定问题分析报告

## 问题概述

在 `AttendedWeighingDetailView` 视图中，DataGrid 内的 ComboBox（材料名称、单位）存在以下问题：
1. `SelectedMaterial` 和 `SelectedMaterialUnit` 有数据但不显示文本
2. 只有第一次选择时能正常渲染，切换选项后不更新
3. 切换列表项（`SelectListItemCommand`）后，物料信息无法正常渲染

## 问题分析

### 问题一：ComboBox 选中项不显示文本

**原因**：使用 `ItemTemplate` 而非 `DisplayMemberBinding`

```xml
<!-- ❌ 错误写法 -->
<ComboBox ItemsSource="{Binding Materials}"
          SelectedItem="{Binding SelectedMaterial}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

在 Avalonia UI 中：
- `ItemTemplate` **仅用于**定义下拉列表中每个项目的显示方式
- ComboBox 关闭状态下显示选中项时，**不会使用** `ItemTemplate`
- 选中项的显示依赖于 `DisplayMemberBinding` 或对象的 `ToString()` 方法

**解决方案**：使用 `DisplayMemberBinding` 替代 `ItemTemplate`

```xml
<!-- ✅ 正确写法 -->
<ComboBox ItemsSource="{Binding Materials}"
          SelectedItem="{Binding SelectedMaterial}"
          DisplayMemberBinding="{Binding Name}" />
```

`DisplayMemberBinding` 会同时应用于：
1. 下拉列表中的每个项目
2. 关闭状态下显示的选中项

---

### 问题二：切换选项后不更新显示

**原因**：清空集合时未重置 `SelectedItem`

```csharp
// ❌ 错误写法
private async Task LoadMaterialUnitsInternalAsync(int materialId)
{
    var units = await LoadMaterialUnitsFunc(materialId);
    MaterialUnits.Clear();  // 此时 SelectedMaterialUnit 还指向旧对象
    foreach (var unit in units)
    {
        MaterialUnits.Add(unit);
    }
}
```

当 `MaterialUnits.Clear()` 被调用时：
- `SelectedMaterialUnit` 还指向一个旧对象
- 该对象现在不在 `ItemsSource` 集合中
- ComboBox 无法正确匹配和显示选中项

**解决方案**：在清空集合前先将 `SelectedItem` 设为 `null`

```csharp
// ✅ 正确写法
private async Task LoadMaterialUnitsInternalAsync(int materialId)
{
    var units = await LoadMaterialUnitsFunc(materialId);
    SelectedMaterialUnit = null;  // 先重置选中项
    MaterialUnits.Clear();
    foreach (var unit in units)
    {
        MaterialUnits.Add(unit);
    }
}
```

---

### 问题三：DataContext 切换后绑定失效

**原因**：使用相对路径绑定 `$parent[DataGrid].DataContext`

```xml
<!-- ❌ 问题写法 -->
<ComboBox ItemsSource="{Binding $parent[DataGrid].DataContext.Materials}" />
```

在 DataGrid 内部使用 `$parent[Type]` 查找父元素的相对路径绑定时：
- 当外层 `DataContext` 被替换（如切换详情视图时创建新的 ViewModel）
- DataGrid 可能会缓存之前的绑定上下文
- 导致 ComboBox 的 `ItemsSource` 无法正确更新

**解决方案**：使用命名元素绑定

```xml
<!-- 1. 给 UserControl 添加名字 -->
<UserControl x:Name="DetailViewRoot"
             x:DataType="vm:AttendedWeighingDetailViewModel">

<!-- 2. 使用命名元素绑定 -->
<ComboBox ItemsSource="{Binding #DetailViewRoot.((vm:AttendedWeighingDetailViewModel)DataContext).Materials}" />
```

命名元素绑定的优势：
- 绑定路径明确，不依赖相对父元素查找
- 当 DataContext 变化时，绑定会正确更新
- 编译时类型检查更可靠

---

## 最终修改总结

### 1. AXAML 修改

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             x:Class="MaterialClient.Views.AttendedWeighingDetailView"
             x:DataType="vm:AttendedWeighingDetailViewModel"
             x:Name="DetailViewRoot">  <!-- 添加名字 -->

    <!-- 材料名称 ComboBox -->
    <ComboBox ItemsSource="{Binding #DetailViewRoot.((vm:AttendedWeighingDetailViewModel)DataContext).Materials}"
              SelectedItem="{Binding SelectedMaterial, Mode=TwoWay}"
              DisplayMemberBinding="{Binding Name}" />

    <!-- 单位 ComboBox -->
    <ComboBox ItemsSource="{Binding MaterialUnits}"
              SelectedItem="{Binding SelectedMaterialUnit, Mode=TwoWay}"
              DisplayMemberBinding="{Binding DisplayName}" />
</UserControl>
```

### 2. ViewModel 修改

```csharp
private async Task LoadMaterialUnitsInternalAsync(int materialId)
{
    if (LoadMaterialUnitsFunc != null)
    {
        try
        {
            var units = await LoadMaterialUnitsFunc(materialId);
            SelectedMaterialUnit = null;  // 先重置
            MaterialUnits.Clear();
            foreach (var unit in units)
            {
                MaterialUnits.Add(unit);
            }
        }
        catch
        {
            // 如果加载失败，保持空列表
        }
    }
}
```

---

## 最佳实践建议

1. **ComboBox 显示绑定**：优先使用 `DisplayMemberBinding`，而非 `ItemTemplate`
2. **集合更新**：清空 `ItemsSource` 集合前，先将 `SelectedItem` 设为 `null`
3. **DataContext 绑定**：在可能发生 DataContext 切换的场景，使用命名元素绑定而非相对路径绑定
4. **双向绑定**：显式指定 `Mode=TwoWay` 确保绑定正确工作
5. **类型转换**：使用 `((Type)Expression)` 语法明确指定绑定目标类型

---

## 相关文件

- `MaterialClient/Views/AttendedWeighingDetailView.axaml`
- `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs`
- `MaterialClient/Views/AttendedWeighingWindow.axaml`

## 日期

2025-12-15

