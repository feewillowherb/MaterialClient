## 1. DataManagementDialogWindow 窗口高度固定

- [x] 1.1 移除 `DataManagementDialogWindow.axaml` Window 元素上的 `SizeToContent="Height"` 属性
- [x] 1.2 在 Window 元素上添加 `Height="500"` 属性
- [x] 1.3 将外层 Grid 的 RowDefinition 第 2 行从 `Auto` 改为 `*`
- [x] 1.4 将内层 Grid（搜索栏+DataGrid）的 RowDefinition 第 2 行（DataGrid 行）从 `Auto` 改为 `*`
- [x] 1.5 将 DataGrid 的 `VerticalScrollBarVisibility` 从 `Disabled` 改为 `Auto`

## 2. MaterialManagementWindow 窗口高度固定

- [x] 2.1 移除 `MaterialManagementWindow.axaml` Window 元素上的 `SizeToContent="Height"` 属性
- [x] 2.2 在 Window 元素上添加 `Height="500"` 属性
- [x] 2.3 将外层 Grid 的 RowDefinition 第 2 行从 `Auto` 改为 `*`
- [x] 2.4 将内层 Grid（搜索栏+DataGrid）的 RowDefinition 第 2 行（DataGrid 行）从 `Auto` 改为 `*`
- [x] 2.5 将 DataGrid 的 `VerticalScrollBarVisibility` 从 `Disabled` 改为 `Auto`

## 3. ProviderManagementWindow 窗口高度固定

- [x] 3.1 移除 `ProviderManagementWindow.axaml` Window 元素上的 `SizeToContent="Height"` 属性
- [x] 3.2 在 Window 元素上添加 `Height="500"` 属性
- [x] 3.3 将外层 Grid 的 RowDefinition 第 2 行从 `Auto` 改为 `*`
- [x] 3.4 将内层 Grid（搜索栏+DataGrid）的 RowDefinition 第 2 行（DataGrid 行）从 `Auto` 改为 `*`
- [x] 3.5 将 DataGrid 的 `VerticalScrollBarVisibility` 从 `Disabled` 改为 `Auto`

## 4. 验证

- [x] 4.1 构建项目确认无编译错误
- [ ] 4.2 验证三个窗口打开时高度固定为 500px
- [ ] 4.3 验证 10 行数据完整显示无需滚动
- [ ] 4.4 验证超过 10 行时 DataGrid 出现垂直滚动条
