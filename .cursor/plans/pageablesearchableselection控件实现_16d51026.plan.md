---
name: PageableSearchableSelection控件实现
overview: 实现一个封装完整的可创建、可分页、可搜索的选择控件，使用单一TextBox作为输入/展示面，Popup内嵌在控件模板中，通过SelectionItem统一类型和扩展方法实现轻量级数据绑定。
todos:
  - id: create-selection-item
    content: 创建 SelectionItem 类 (MaterialClient.Common/Models/SelectionItem.cs)
    status: completed
  - id: create-selection-extensions
    content: 创建 SelectionItemExtensions 扩展方法 (MaterialClient.Common/Extensions/SelectionItemExtensions.cs)
    status: completed
  - id: create-control-code
    content: 创建 PageableSearchableSelectionBox.axaml.cs 代码文件
    status: completed
  - id: create-control-xaml
    content: 创建 PageableSearchableSelectionBox.axaml 模板文件
    status: completed
  - id: add-project-files
    content: 将新文件添加到项目文件 (.csproj)
    status: completed
  - id: test-integration
    content: 在测试视图中集成并验证功能
    status: completed
isProject: false
---

## 实现计划

### 一、创建 SelectionItem 基础类型

**1.1 创建 SelectionItem 类**  
位置：`MaterialClient.Common/Models/SelectionItem.cs`

- 实现 Id (int) 和 Name (string) 属性
- 实现 IEquatable 接口
- 提供 Equals 和 GetHashCode 重写

**1.2 创建 SelectionItemExtensions 扩展方法**  
位置：`MaterialClient.Common/Extensions/SelectionItemExtensions.cs`

- ToSelectionItem(this Provider provider)
- ToSelectionItem(this Material material)
- ToSelectionItem(this ProviderDto dto)
- ToSelectionItem(this string street)

### 二、创建 PageableSearchableSelectionBox 控件

**2.1 创建 PageableSearchableSelectionBox.axaml.cs**  
位置：`MaterialClient/Views/PageableSearchableSelectionBox.axaml.cs`

核心属性：

- SelectedItem (SelectionItem?, TwoWay)
- LoadPageAsync (Func委托)
- Watermark (string?)
- PageSize (int, 默认10)
- IsPopupOpen (bool, TwoWay)
- PopupWidth (double?, 默认400)
- AllowCreateNew (bool, 默认true)
- AddNewButtonText (string, 默认"新增")
- AddNewCommand (ICommand?)
- LoadingDelayMs (int, 默认300)

内部属性：

- PagedItems (IReadOnlyList)
- CurrentPage (int)
- TotalCount (int)
- TotalPages (int)
- SearchText (string?)
- ShowAddNewButton (bool)

关键方法：

- OnApplyTemplate() - 获取模板部件引用
- LoadDataAsync() - 加载数据
- OnTextBoxGotFocus() - 打开Popup
- OnTextBoxTextChanged() - 防抖搜索
- OnDataGridSelectionChanged() - 选择项
- OnKeyDown() - 键盘导航
- RestoreSelectedItemIfInCurrentPage() - 恢复选中项

**2.2 创建 PageableSearchableSelectionBox.axaml**  
位置：`MaterialClient/Views/PageableSearchableSelectionBox.axaml`

模板结构：

- PART_TextBox (TextBox) - 唯一输入/展示面
- PART_Popup (Popup) - 内嵌弹窗
  - Border (固定宽度400px或自定义)
    - PART_ItemsList (DataGrid) - 当前页列表
    - PART_Pager (Ursa.Pagination) - 分页控件
    - PART_AddNewPanel (StackPanel) - "新增"入口

### 三、实现核心逻辑

**3.1 打开Popup逻辑**

- 设置 IsPopupOpen = true
- TextBox获得焦点
- TextBox.Text设置为当前SelectedItem的Name
- 加载第1页数据

**3.2 输入搜索逻辑**

- 使用 ReactiveUI 的 Throttle (300ms防抖)
- 更新 SearchText
- 重置 CurrentPage = 1
- 调用 LoadPageAsync 加载数据

**3.3 选择项逻辑**

- 用户点击列表项或按Enter
- 更新 SelectedItem
- IsPopupOpen = false
- TextBox.Text = 新选中项.Name

**3.4 关闭Popup逻辑**

- Escape/外部点击/选择项
- IsPopupOpen = false
- TextBox.Text = 当前SelectedItem.Name（恢复）

**3.5 分页逻辑**

- Ursa.Pagination 绑定到 CurrentPage/TotalCount/PageSize
- 翻页时调用 LoadPageAsync
- Popup保持打开

**3.6 新增逻辑**

- 无结果时显示 PART_AddNewPanel
- 点击"新增"按钮触发 AddNewCommand
- 如果 AddNewCommand 未设置，使用内部默认逻辑

### 四、与现有代码的集成

**4.1 不影响现有代码**

- 新控件独立于 SearchableSelectionBox + GenericSelectionPopup
- 可逐步迁移

**4.2 测试验证**

- 在 SolidWasteModeFormView 中添加示例使用
- 验证搜索、分页、选择、新增功能

### 五、项目文件修改

**5.1 添加文件到项目**

- MaterialClient.Common.csproj 添加 SelectionItem.cs 和 SelectionItemExtensions.cs
- MaterialClient.csproj 添加 PageableSearchableSelectionBox.axaml 和 PageableSearchableSelectionBox.axaml.cs

**5.2 命名空间引用**

- PageableSearchableSelectionBox.axaml 添加 Ursa 命名空间引用

## 文件清单

新增文件：

1. `MaterialClient.Common/Models/SelectionItem.cs`
2. `MaterialClient.Common/Extensions/SelectionItemExtensions.cs`
3. `MaterialClient/Views/PageableSearchableSelectionBox.axaml`
4. `MaterialClient/Views/PageableSearchableSelectionBox.axaml.cs`

## 使用示例

```xml
<views:PageableSearchableSelectionBox
    Height="32"
    SelectedItem="{Binding SelectedProvider, Mode=TwoWay}"
    LoadPageAsync="{Binding LoadProvidersAsync}"
    Watermark="请选择供应商"
    PageSize="10"
    PopupWidth="500" />
```

```csharp
public SelectionItem? SelectedProvider { get; set; }

public async Task<PagedResultDto<SelectionItem>> LoadProvidersAsync(
    string? searchText, int page, int pageSize, IReadOnlyList<int>? selectedIds)
{
    var result = await _providerService.GetPagedAsync(searchText, page, pageSize, selectedIds);
    var items = result.Items.Select(p => p.ToSelectionItem()).ToList();
    return new PagedResultDto<SelectionItem> { Items = items, TotalCount = result.TotalCount };
}
```

