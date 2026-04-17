## Context

`AttendedWeighingDetailView.axaml` 当前使用 `Panel` 同时承载 `StandardModeFormView` 和 `SolidWasteModeFormView`，通过 `IsVisible` 绑定切换可见性：

```xml
<!-- 当前结构（问题代码） -->
<Panel Grid.Row="1" Grid.RowSpan="2">
    <aw:StandardModeFormView IsVisible="{Binding !IsSolidWasteMode}" />
    <aw:SolidWasteModeFormView IsVisible="{Binding IsSolidWasteMode}" />
</Panel>
```

问题：两个子视图始终存在于可视化树中。`StandardModeFormView` 声明了 `x:DataType="vm:StandardWeighingDetailViewModel"`，其编译绑定会强转 DataContext。当 SolidWaste 模式激活时，DataContext 为 `SolidWasteWeighingDetailViewModel`，触发 `InvalidCastException`。

继承关系：
```
AttendedWeighingDetailViewModelBase (abstract)
├── StandardWeighingDetailViewModel
│     └── IsMaterialPopupOpen, MaterialsSelectionPopupViewModel, OpenMaterialSelectionCommand
└── SolidWasteWeighingDetailViewModel
      └── SolidWasteOrderNumber, SelectedStreet, SolidWasteTypes, ...
```

## Goals / Non-Goals

**Goals:**
- 消除 SolidWaste 模式下的 `InvalidCastException`
- 保持两个子视图各自的 `x:DataType` 强类型绑定不变
- 仅实例化与当前模式匹配的子视图，减少不必要的绑定开销

**Non-Goals:**
- 不修改 ViewModel 层代码
- 不修改 `StandardModeFormView.axaml` 或 `SolidWasteModeFormView.axaml` 的内部结构
- 不改变用户可见行为

## Decisions

### Decision 1: 使用 ContentControl + DataTemplate 替代 Panel + IsVisible

**选择**：`ContentControl` + `DataTemplate` 类型选择

**替代方案**：
| 方案 | 优点 | 缺点 |
|------|------|------|
| A. `DataTemplate` 类型选择 | Avalonia 惯用模式；编译绑定类型安全；仅实例化一个视图 | 视图切换时销毁/重建（当前无影响） |
| B. 子视图 `x:DataType` 改为基类 | 无需改结构 | `IsMaterialPopupOpen` 等子类属性无法使用编译绑定；需退化为弱绑定 |
| C. `{CompileBinding x:False}` | 最小改动 | 丧失编译绑定类型安全；运行时绑定性能较低 |
| D. 将子类属性上移到基类 | 绑定兼容 | 违反单一职责；SolidWaste 模式不需要材料弹窗属性 |

**理由**：方案 A 是 Avalonia 处理多态视图的标准模式。`DataTemplate` 的 `DataType` 属性在运行时匹配实际对象类型，仅创建匹配的视图实例，从根本上避免类型转换。

### Decision 2: ContentControl 的 Content 绑定方式

**选择**：`Content="{Binding}"` — 将整个 DataContext（即 `AttendedWeighingDetailViewModelBase` 子类实例）作为 Content 传递

```xml
<ContentControl Grid.Row="1" Grid.RowSpan="2" Content="{Binding}">
    <ContentControl.DataTemplates>
        <DataTemplate DataType="vm:StandardWeighingDetailViewModel">
            <aw:StandardModeFormView />
        </DataTemplate>
        <DataTemplate DataType="vm:SolidWasteWeighingDetailViewModel">
            <aw:SolidWasteModeFormView />
        </DataTemplate>
    </ContentControl.DataTemplates>
</ContentControl>
```

**理由**：`Content="{Binding}"` 等价于绑定当前 DataContext。DataTemplate 根据 Content 的运行时类型自动选择，生成的子视图的 DataContext 即为 Content 对象本身，类型与子视图的 `x:DataType` 声明一致。

## Risks / Trade-offs

- **视图状态不保留** → 当 `DetailViewModel` 被重新赋值时（`OpenDetail` 调用），视图会被重建。但当前设计中每次打开详情都会创建新的 ViewModel 实例（`ITransientDependency`），因此视图重建与 ViewModel 生命周期一致，无额外影响。
- **Popup 位置计算** → `StandardModeFormView` 中的 `MaterialSelectionPopup` 使用 `x:Name` 引用，在 DataTemplate 内部创建后名称引用仍然有效（DataTemplate 创建的视图树是完整的），无影响。
