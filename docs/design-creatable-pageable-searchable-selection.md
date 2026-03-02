# 可创建、可分页、可搜索选择组件设计方案

**日期**: 2026-02-28  
**状态**: 设计阶段  
**相关提案**: [proposal-creatable-pageable-searchable-selection.md](./proposal-creatable-pageable-searchable-selection.md)

---

## 一、设计目标

### 1.1 核心目标

创建一个**单一、封装完整的选择控件**，具备以下特性：
- ✅ **单一输入面**：一个TextBox同时用于展示和搜索（避免"关闭时TextBlock、打开时TextBox"的两套UI）
- ✅ **内嵌Popup**：Popup在控件模板内声明，父视图无需维护Popup状态
- ✅ **可搜索、可分页、可创建**：完整保留GenericSelectionPopupViewModel的功能
- ✅ **简化使用**：父视图通过标准属性配置，无需声明Popup或管理状态

### 1.2 当前问题回顾

现有`SearchableSelectionBox + 独立Popup + GenericSelectionPopup`拼装方式的问题：

| 问题 | 影响 |
|------|------|
| **两套UI** | TextBlock/TextBox切换，违反"单一输入面"原则 |
| **Popup外置** | 父视图需要维护`IsXxxPopupOpen`、PlacementTarget等状态 |
| **重复声明** | 每个选择字段需要在父视图中声明一次Popup |
| **依赖特定接口** | 强依赖IGenericSelectionPopupBindings，不够灵活 |

---

## 二、组件架构设计

### 2.1 组件层次

```
PageableSearchableSelectionBox (TemplatedControl)
├── PART_TextBox (TextBox)          // 唯一输入/展示面
└── PART_Popup (Popup)              // 内嵌弹窗
    └── Border（宽度与触发器对齐）
        ├── PART_ItemsList (DataGrid)   // 当前页列表
        ├── PART_Pager (Ursa.Pagination)  // 分页控件
        └── PART_AddNewPanel (StackPanel)  // "新增"入口（无结果时显示）
```

**关键设计点**：
- Popup的`PlacementTarget`绑定到`TemplatedParent`（控件自身）
- Popup默认宽度400px（与现有GenericSelectionPopup保持一致），可通过PopupWidth自定义
- 父视图**无需声明Popup**，只需声明控件本身

### 2.2 与ViewModel的关系

```
PageableSearchableSelectionBox (Control)
    │
    ├── DataContext 可为任何对象
    │   └── 通过绑定属性配置数据加载
    │
    └── 可选：DataContext 为 IPageableSelectionBindings
        └── 自动绑定 SelectedItem、SearchText 等属性
```

**两种使用方式**：

1. **属性配置方式**（推荐）：通过标准属性配置
```xml
<views:PageableSearchableSelectionBox
    LoadPageAsync="{Binding LoadProvidersAsync}"
    SelectedItem="{Binding SelectedProvider, Mode=TwoWay}"
    Watermark="请选择供应商"
    PageSize="10"
    PopupWidth="500" />  <!-- 可选：固定Popup宽度，未设置则默认为400px -->
```

2. **接口绑定方式**（兼容）：DataContext绑定实现IPageableSelectionBindings的对象
```xml
<views:PageableSearchableSelectionBox
    DataContext="{Binding ProvidersPopupViewModel}"
    SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
    SearchText="{Binding SearchText, Mode=TwoWay}" />
```

---

## 三、SelectionItem设计

### 3.1 SelectionItem类定义

为了统一处理不同类型的选择项，定义一个统一的包装类：

```csharp
/// <summary>
/// 统一的选择项包装类，包含Id和Name属性
/// </summary>
public class SelectionItem : IEquatable<SelectionItem>
{
    public int Id { get; }
    public string Name { get; }

    public SelectionItem(int id, string name)
    {
        Id = id;
        Name = name ?? string.Empty;
    }

    public bool Equals(SelectionItem? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SelectionItem);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
```

### 3.2 实体转换方法

为每种实体类型提供静态转换方法：

```csharp
/// <summary>
/// 实体扩展方法，提供SelectionItem转换
/// </summary>
public static class SelectionItemExtensions
{
    // Provider转换
    public static SelectionItem ToSelectionItem(this Provider provider)
    {
        return new SelectionItem(provider.Id, provider.ProviderName);
    }

    // Material转换
    public static SelectionItem ToSelectionItem(this Material material)
    {
        return new SelectionItem(material.Id, material.MaterialName);
    }

    // ProviderDto转换
    public static SelectionItem ToSelectionItem(this ProviderDto dto)
    {
        return new SelectionItem(dto.Id, dto.ProviderName);
    }

    // 镇街（string）转换
    public static SelectionItem ToSelectionItem(this string street)
    {
        return new SelectionItem(street.GetHashCode(), street);
    }
}
```

### 3.3 使用示例

#### 在ViewModel中加载数据

```csharp
public SelectionItem? SelectedProvider { get; set; }

public async Task<PagedResultDto<SelectionItem>> LoadProvidersAsync(
    string? searchText, int page, int pageSize, IReadOnlyList<int>? selectedIds)
{
    // 1. 加载数据并转换为SelectionItem
    var result = await _providerService.GetPagedAsync(searchText, page, pageSize, selectedIds);
    var items = result.Items.Select(p => p.ToSelectionItem()).ToList();
    
    // 2. 返回统一类型
    return new PagedResultDto<SelectionItem> 
    { 
        Items = items, 
        TotalCount = result.TotalCount 
    };
}
```

#### 创建新项

```csharp
public async Task<SelectionItem> CreateProviderItem(string name)
{
    // 1. 创建原始实体
    var provider = await _providerService.CreateAsync(name);
    
    // 2. 转换为SelectionItem
    return provider.ToSelectionItem();
}
```

### 3.3 优势说明

| 优势 | 说明 |
|------|------|
| **类型安全** | 强类型，无需反射，编译时检查 |
| **性能优秀** | 直接属性访问，无运行时开销 |
| **使用简单** | 统一的SelectionItem类型，无需DisplayMemberPath/ValueMemberPath |
| **扩展方便** | 为新实体类型添加一个ToSelectionItem()方法即可 |
| **UI一致** | 所有选择项都有Id和Name，XAML绑定统一 |
| **无需原始信息** | 组件只使用Id和Name，不保存原始实体引用 |

---

## 四、控件API设计

### 4.1 核心属性

| 属性 | 类型 | 说明 |
|------|------|------|
| **SelectedItem** | `SelectionItem?` | 当前选中项，TwoWay |
| **LoadPageAsync** | `Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<SelectionItem>>>` | 分页加载委托 |
| **Watermark** | `string?` | 无选中时的占位文本 |
| **PageSize** | `int` | 每页条数，默认10 |
| **IsPopupOpen** | `bool` | Popup打开状态，TwoWay（可选，用于外部控制） |

---

## 五、可选属性

### 5.1 可选属性表

| 属性 | 类型 | 说明 |
|------|------|------|
| **AllowCreateNew** | `bool` | 是否允许创建新项，默认true |
| **AddNewButtonText** | `string` | "新增"按钮文本，默认"新增" |
| **AddNewCommand** | `ICommand` | 自定义新增命令（可选，未提供则使用内部逻辑） |
| **LoadingDelayMs** | `int` | 搜索防抖延迟，默认300ms |
| **PopupWidth** | `double?` | Popup弹窗的固定宽度，null时默认为400px |

### 5.2 Popup宽度配置说明

**PopupWidth属性**提供灵活的Popup宽度控制：

| PopupWidth值 | 效果 | 使用场景 |
|--------------|------|----------|
| `null`（默认） | Popup使用固定宽度400px | 大多数场景，与现有GenericSelectionPopup保持一致 |
| 固定数值（如`500`） | Popup使用指定像素宽度 | 需要展示更多内容时 |

**设计考虑**：
- 默认宽度400px与现有GenericSelectionPopup的Width="400"保持一致
- Border设置固定宽度，确保界面稳定和可预测
- 可根据需要调整PopupWidth以适应不同场景

**示例**：
```xml
<!-- 默认宽度400px -->
<views:PageableSearchableSelectionBox
    LoadPageAsync="{Binding LoadProvidersAsync}"
    SelectedItem="{Binding SelectedProvider, Mode=TwoWay}" />

<!-- 自定义宽度500px -->
<views:PageableSearchableSelectionBox
    LoadPageAsync="{Binding LoadProvidersAsync}"
    SelectedItem="{Binding SelectedProvider, Mode=TwoWay}"
    PopupWidth="500" />
```

---

## 六、内部行为设计

### 6.1 状态机

```
状态转换：
[关闭] → 点击/聚焦 → [打开中] → [打开]
[打开] → 选择项 → [关闭] (已更新SelectedItem)
[打开] → Escape/点击外部 → [关闭] (已重置搜索文本)
[打开] → 输入 → [搜索中] → [打开] (更新搜索结果)
[打开] → 翻页 → [加载中] → [打开] (更新当前页)
```

### 4.2 关键逻辑

#### 打开Popup
1. 设置`IsPopupOpen = true`
2. TextBox获得焦点
3. TextBox.Text设置为当前SelectedItem的显示文本（或空）
4. 内部`_searchText` = TextBox.Text
5. 加载第1页数据（searchText + selectedIds）

#### 输入搜索
1. TextBox.TextChanged → 防抖（300ms）
2. 内部`_searchText` = TextBox.Text
3. CurrentPage = 1
4. 加载数据（searchText + page + pageSize）
5. Popup保持打开

#### 选择项
1. 用户点击列表项或按Enter
2. 更新`SelectedItem`
3. `IsPopupOpen = false`
4. TextBox.Text = 新选中项显示文本

#### 关闭Popup（Escape/外部点击）
1. `IsPopupOpen = false`
2. TextBox.Text = 当前SelectedItem显示文本（恢复）
3. 内部`_searchText` = TextBox.Text
4. TextBox获得焦点

#### 分页
1. 用户点击分页控件
2. CurrentPage更新
3. 加载数据（searchText + page + pageSize）
4. Popup保持打开

### 4.3 数据加载流程

```csharp
private async Task LoadDataAsync()
{
    // 1. 准备参数
    var searchText = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim();
    var selectedIds = GetSelectedIds(); // 从SelectedItem提取ID
    
    // 2. 调用加载委托
    var result = await LoadPageAsync?.Invoke(
        searchText,
        CurrentPage,
        PageSize,
        selectedIds,
        _cancellationTokenSource.Token
    );
    
    // 3. 更新显示
    PagedItems = result.Items;
    TotalCount = (int)result.TotalCount;
    TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    // 4. 恢复选中项（如果selectedIds在当前页）
    RestoreSelectedItemIfInCurrentPage();
}
```

---

## 七、模板设计（AXAML）

```xml
<ControlTemplate TargetType="views:PageableSearchableSelectionBox">
    <Panel>
        <!-- 触发器：单一TextBox -->
        <TextBox Name="PART_TextBox"
                 Height="32"
                 FontSize="12"
                 Watermark="{TemplateBinding Watermark}"
                 Text="{TemplateBinding SearchText, Mode=TwoWay}"
                 Background="White"
                 BorderBrush="#E5E7EB"
                 BorderThickness="1"
                 Padding="6,0,6,0" />
        
        <!-- 内嵌Popup -->
        <Popup Name="PART_Popup"
               Placement="Bottom"
               PlacementTarget="{TemplateBinding}"
               IsOpen="{TemplateBinding IsPopupOpen}"
               IsLightDismissEnabled="True">
            <Border Background="White"
                    BorderBrush="#E5E7EB"
                    BorderThickness="3"
                    CornerRadius="4"
                    Width="{TemplateBinding PopupWidth, FallbackValue=400}"
                    MaxHeight="300">
                <Grid RowDefinitions="*,50">
                    <!-- 列表区 -->
                    <DataGrid Name="PART_ItemsList"
                              Grid.Row="0"
                              AutoGenerateColumns="False"
                              ItemsSource="{Binding PagedItems, RelativeSource={RelativeSource TemplatedParent}}"
                              SelectedItem="{Binding SelectedItem, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"
                              IsReadOnly="True"
                              GridLinesVisibility="Horizontal"
                              HeadersVisibility="Column">
                        <DataGrid.Columns>
                            <DataGridTextColumn 
                                Header="名称" 
                                Binding="{Binding Name}" 
                                Width="*" />
                        </DataGrid.Columns>
                    </DataGrid>
                    
                    <!-- 分页区 -->
                    <Grid Name="PART_Pager" Grid.Row="1">
                        <u:Pagination CurrentPage="{Binding CurrentPage, Mode=TwoWay}"
                                      TotalCount="{Binding TotalCount}"
                                      PageSize="{Binding PageSize}"
                                      Command="{Binding PageChangeCommand}" />
                    </Grid>
                    
                    <!-- 无结果时的新增按钮 -->
                    <StackPanel Name="PART_AddNewPanel"
                                Grid.Row="0"
                                IsVisible="{Binding ShowAddNewButton}"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Spacing="10">
                        <TextBlock Text="未找到匹配结果" FontSize="12" />
                        <Button Content="{Binding AddNewButtonText}"
                                Command="{Binding AddNewItemCommand}" />
                    </StackPanel>
                </Grid>
            </Border>
        </Popup>
    </Panel>
</ControlTemplate>
```

---

## 八、与现有代码的兼容性

### 8.1 迁移策略

新控件采用`SelectionItem`统一类型，与现有`GenericSelectionItem<T>`不兼容，建议采取以下策略：

#### 阶段1：新控件开发（不影响现有代码）
1. 实现`PageableSearchableSelectionBox`
2. 实现`SelectionItem`类和扩展方法
3. 在测试视图中验证功能
4. 完善单元测试

#### 阶段2：逐步迁移（选择性地替换）
1. 在新功能中使用新控件
2. 修改ViewModel使用`SelectionItem`替代`GenericSelectionItem<T>`
3. 在次要视图中替换现有拼装
4. 收集反馈，迭代优化

#### 阶段3：全面迁移
1. 替换所有SearchableSelectionBox + Popup组合
2. 移除父视图中的Popup声明
3. 清理相关状态属性（IsXxxPopupOpen等）
4. 移除`GenericSelectionPopupViewModel<T>`（可选）

---

## 九、实现检查清单

### 9.1 核心功能
- [ ] SelectionItem类实现（Id、Name属性）
- [ ] 实体扩展方法（ToSelectionItem）
- [ ] 单一TextBox作为输入/展示面
- [ ] 点击/聚焦时自动打开Popup
- [ ] 内嵌Popup（PlacementTarget为控件自身）
- [ ] 输入时防抖（默认300ms）
- [ ] 支持分页加载
- [ ] 无结果时显示"新增"按钮
- [ ] Escape/外部点击时关闭并恢复显示
- [ ] 选择项后关闭Popup

### 9.2 属性与绑定
- [ ] SelectedItem（SelectionItem类型）双向绑定
- [ ] LoadPageAsync属性配置
- [ ] Watermark占位文本
- [ ] IsPopupOpen外部控制
- [ ] PopupWidth可选配置（默认400px）

### 9.3 样式与UX
- [ ] 样式与现有SearchableSelectionBox一致
- [ ] Popup宽度默认400px
- [ ] 键盘导航（上下箭头、Enter、Escape）
- [ ] 焦点管理正确

---

## 十、已确认事项

1. **新增功能的实现方式**：✅ 已确认
   - **决策**：选项A，通过`AddNewCommand`委托外部实现
   - **理由**：更灵活，让调用方控制新增逻辑

2. **分页模式**：✅ 已确认
   - **决策**：选项A，仅支持服务端分页
   - **理由**：简化实现，客户端分页可后续扩展

---

## 十二、用户使用体验对比

### 13.1 用户操作流程对比

**当前实现（SearchableSelectionBox + GenericSelectionPopup）**：

```
用户操作：
1. 点击选择框 → SearchableSelectionBox打开Popup
2. 输入搜索词 → GenericSelectionPopup加载并显示结果
3. 点击某一项 → GenericSelectionPopup更新SelectedItem
4. ViewModel监听变化 → 设置实际选中项 → 关闭Popup
5. Popup关闭 → SearchableSelectionBox显示选中项名称
```

**新实现（PageableSearchableSelectionBox）**：

```
用户操作：
1. 点击选择框 → PageableSearchableSelectionBox自动打开Popup
2. 输入搜索词 → PageableSearchableSelectionBox防抖后加载并显示结果
3. 点击某一项 → PageableSearchableSelectionBox更新SelectedItem并关闭Popup
4. Popup关闭 → TextBox自动显示选中项名称
```

### 13.2 用户体验对比

| 体验维度 | 当前实现 | 新实现 | 说明 |
|----------|---------|--------|------|
| **打开方式** | 点击选择框时自动打开 | 点击选择框时自动打开 | ✅ 一致 |
| **搜索体验** | 输入300ms后加载结果 | 输入300ms后加载结果 | ✅ 一致 |
| **选择反馈** | 点击后选中项高亮 | 点击后选中项高亮 | ✅ 一致 |
| **关闭方式** | 选中后Popup自动关闭 | 选中后Popup自动关闭 | ✅ 一致 |
| **取消选择** | Escape/外部点击关闭并恢复 | Escape/外部点击关闭并恢复 | ✅ 一致 |
| **新增功能** | 无结果时显示"新增"按钮 | 无结果时显示"新增"按钮 | ✅ 一致 |
| **分页功能** | 支持页码切换 | 支持页码切换 | ✅ 一致 |
| **键盘导航** | 上下箭头选择，Enter确认，Escape取消 | 上下箭头选择，Enter确认，Escape取消 | ✅ 一致 |
| **显示内容** | DataGrid显示名称 | DataGrid显示名称 | ✅ 一致 |

### 13.3 用户感知的差异

**对用户不可见的内部差异**：

| 方面 | 当前实现 | 新实现 | 用户影响 |
|------|---------|--------|---------|
| **状态管理** | Popup打开状态由ViewModel管理 | Popup打开状态由控件内部管理 | ❌ 用户无感知 |
| **数据加载** | 调用GenericSelectionPopupViewModel的RefreshAsync | 调用LoadPageAsync委托 | ❌ 用户无感知 |
| **新增实现** | GenericSelectionPopupViewModel内部实现createNewItemFunc | 通过AddNewCommand委托外部实现 | ❌ 用户无感知 |

**用户可感知的差异**：

| 方面 | 当前实现 | 新实现 | 用户影响 |
|------|---------|--------|---------|
| **绑定表达式** | 复杂的`$parent[UserControl].((vm:ViewModel)DataContext).IsProvidersPopupOpen` | 简单的`SelectedProvider="{Binding SelectedProvider, Mode=TwoWay}"` | ✅ 新实现更简单易维护 |
| **代码位置** | 选中项变化逻辑分散在ViewModel的多个WhenAnyValue订阅中 | 选中项变化集中在控件内部 | ✅ 新实现更清晰 |

### 13.4 兼容性保证

为确保用户迁移后体验一致，新实现需要：

1. **保持所有交互行为**：打开、关闭、搜索、选择、分页
2. **保持所有键盘快捷键**：上下箭头、Enter、Escape
3. **保持UI外观一致**：字体、颜色、间距、边框
4. **保持响应速度**：防抖延迟300ms、加载动画
5. **保持新增功能**：无结果时显示"新增"按钮

### 13.5 迁移验证

迁移前需要进行用户验收测试：

| 测试场景 | 预期结果 | 验证方式 |
|----------|---------|---------|
| **基本选择** | 点击、选择、关闭 | 手动操作对比 |
| **搜索功能** | 输入、查看结果、选择 | 搜索响应时间对比 |
| **分页切换** | 翻页、选择、关闭 | 分页交互对比 |
| **新增操作** | 搜索不存在的项、点击"新增" | 新增流程对比 |
| **键盘操作** | 方向键选择、Enter确认、Escape取消 | 键盘交互对比 |
| **快捷取消** | 输入后直接按Escape | 状态恢复对比 |

**验证标准**：
- ✅ 所有用户操作流程与新实现一致
- ✅ 所有交互反馈与新实现一致
- ✅ 所有性能指标与新实现一致或更好

---

## 十一、参考文档

- [提案文档](./proposal-creatable-pageable-searchable-selection.md)
- [SearchableSelectionBox当前实现](../MaterialClient/Views/SearchableSelectionBox.axaml)
- [GenericSelectionPopup当前实现](../MaterialClient/Views/GenericSelectionPopup.axaml)
- [GenericSelectionPopupViewModel](../MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs)

---

## 十二、新旧实现对比

### 12.1 当前实现（SearchableSelectionBox + Popup拼装）

**视图层（SolidWasteModeFormView.axaml）**：
```xml
<!-- 供应商选择 -->
<Grid ColumnDefinitions="72,*">
    <Label Content="{Binding ProviderLabelText}"
           VerticalAlignment="Center"
           HorizontalAlignment="Left"
           FontSize="12"
           Foreground="#333333"
           Margin="0,0,8,0" />
    <views:SearchableSelectionBox x:Name="ProvidersSelectionBox"
                                 Grid.Column="1"
                                 Height="32"
                                 DataContext="{Binding ProvidersPopupViewModel}"
                                 IsPopupOpen="{Binding $parent[UserControl].((vm:AttendedWeighingDetailViewModel)DataContext).IsProvidersPopupOpen, Mode=TwoWay}"
                                 PlaceholderText="请选择供应商"
                                 Margin="0,5,0,5" />
</Grid>

<!-- 独立弹窗 -->
<Popup Name="ProvidersSelectionPopup"
       Grid.RowSpan="2"
       Placement="Bottom"
       IsLightDismissEnabled="True"
       HorizontalOffset="0"
       VerticalOffset="0"
       IsOpen="{Binding IsProvidersPopupOpen, Mode=TwoWay}">
    <views:GenericSelectionPopup x:Name="ProvidersSelectionPopupControl"
                                 DataContext="{Binding ProvidersPopupViewModel}" />
</Popup>
```

**ViewModel层（AttendedWeighingDetailViewModel.cs）**：
```csharp
// 需要维护三个Popup ViewModel
[Reactive] private GenericSelectionPopupViewModel<ProviderDto>? _providersPopupViewModel;
[Reactive] private GenericSelectionPopupViewModel<Material>? _materialsPopupViewModel;
[Reactive] private GenericSelectionPopupViewModel<string>? _streetsPopupViewModel;

// 需要维护三个Popup打开状态
[Reactive] private bool _isProvidersPopupOpen;
[Reactive] private bool _isMaterialsPopupOpen;
[Reactive] private bool _isStreetsPopupOpen;

// 初始化逻辑复杂
ProvidersPopupViewModel = new GenericSelectionPopupViewModel<ProviderDto>(
    pagingMode: GenericSelectionPagingMode.ServerSide,
    displayTextSelector: p => p.ProviderName,
    loadPageFunc: (search, pageIndex, pageSize, selectedIds) => ...,
    getSelectedId: p => p.Id,
    createNewItemFunc: async name => { ... });

// 需要监听Popup状态变化和选中项变化
this.WhenAnyValue(x => x.IsProvidersPopupOpen, x => x.ProvidersPopupViewModel.SelectedItem)
    .Subscribe(tuple => { ... });
```

### 12.2 新实现（PageableSearchableSelectionBox）

**视图层（SolidWasteModeFormView.axaml）**：
```xml
<!-- 供应商选择 -->
<Grid ColumnDefinitions="72,*">
    <Label Content="{Binding ProviderLabelText}"
           VerticalAlignment="Center"
           HorizontalAlignment="Left"
           FontSize="12"
           Foreground="#333333"
           Margin="0,0,8,0" />
    <views:PageableSearchableSelectionBox
                                 Grid.Column="1"
                                 Height="32"
                                 SelectedItem="{Binding SelectedProvider, Mode=TwoWay}"
                                 LoadPageAsync="{Binding LoadProvidersAsync}"
                                 Watermark="请选择供应商"
                                 PopupWidth="500"
                                 Margin="0,5,0,5" />
</Grid>

<!-- 无需声明Popup -->
```

**ViewModel层（AttendedWeighingDetailViewModel.cs）**：
```csharp
// 直接使用SelectionItem
public SelectionItem? SelectedProvider { get; set; }

// 数据加载方法返回SelectionItem
public async Task<PagedResultDto<SelectionItem>> LoadProvidersAsync(
    string? searchText, int page, int pageSize, IReadOnlyList<int>? selectedIds)
{
    var result = await _providerService.GetPagedAsync(searchText, page, pageSize, selectedIds);
    var items = result.Items.Select(p => p.ToSelectionItem()).ToList();
    return new PagedResultDto<SelectionItem> { Items = items, TotalCount = result.TotalCount };
}

// 无需维护Popup状态
// 无需监听复杂的状态变化
// 代码更简洁清晰
```

### 12.3 对比总结

| 对比维度 | 当前实现 | 新实现 |
|-----------|---------|--------|
| **组件数量** | 每个选择字段需要3个组件<br>• SearchableSelectionBox<br>• Popup声明<br>• GenericSelectionPopup | 只需要1个组件：PageableSearchableSelectionBox |
| **XAML代码量** | 每个字段 ~20行（Label + SearchableSelectionBox + Popup） | 每个字段 ~10行（Label + PageableSearchableSelectionBox） |
| **Popup声明** | 父视图中需要声明3个独立的Popup | Popup内嵌在控件模板中，无需声明 |
| **状态维护** | 需要维护3个Popup ViewModel + 3个布尔状态 | 只需维护SelectedItem属性 |
| **绑定复杂度** | 需要复杂的绑定表达式<br>`$parent[UserControl].((vm:ViewModel)DataContext).IsProvidersPopupOpen` | 直接绑定`SelectedItem="{Binding SelectedProvider}"` |
| **新增逻辑** | 集成在GenericSelectionPopupViewModel内部 | 通过AddNewCommand委托外部实现，更灵活 |
| **类型处理** | 泛型GenericSelectionItem<T>，需要适配 | 统一SelectionItem类型，扩展方法简单 |
| **内存占用** | GenericSelectionPopupViewModel保存原始实体引用 | SelectionItem只包含Id和Name，更轻量 |
| **可维护性** | 分散在多个组件和文件中 | 封装在单一控件内，易于维护 |
| **用户体验** | 功能完整但实现复杂 | 功能完整且使用更简单 |

### 12.4 迁移收益

从当前实现迁移到新实现后的收益：

1. **代码量减少50%**：XAML从~60行减少到~30行
2. **状态管理简化**：移除6个属性（3个ViewModel + 3个布尔状态）
3. **可读性提升**：无需理解复杂的父子绑定和Popup状态流转
4. **性能优化**：SelectionItem更轻量，减少内存占用
5. **易于测试**：单一组件更容易进行单元测试和集成测试
6. **符合设计原则**：单一职责、内聚高耦合低
