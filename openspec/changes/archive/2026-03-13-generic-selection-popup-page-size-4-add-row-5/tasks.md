## 1. 分页大小默认值

- [x] 1.1 将 `GenericSelectionPopupViewModel` 中 `DefaultPageSize` 从 10 改为 4

## 2. 列表区域布局（4 行数据 + 第 5 行新增）

- [x] 2.1 在 `GenericSelectionPopup.axaml` 的 Row 0 内改为子 Grid：上方 DataGrid 高度 120（4×30）、行高 30，下方第 5 行在 `ShowAddNewButtonBelowList` 为 true 时显示「新增」按钮，绑定沿用 `AddNewButtonText`、`AddNewItemCommand`
- [x] 2.2 移除当前 Row 1（有结果时新增按钮独立一行的 Border/Button），将主 Grid 行定义为列表块（*）+ 分页行（50）
- [x] 2.3 无结果时 Row 0 仍显示居中「未找到匹配结果」与「新增」按钮，与有结果时的 4+1 布局互斥（IsVisible 或布局切换保持不变）

## 3. 子类/独立弹窗（可选）

- [x] 3.1 若 `MaterialsSelectionPopupViewModel` 使用同一 `GenericSelectionPopup` 视图且未传 pageSize，确认其默认 PageSize 与 4 一致（或显式传入 4）；若使用独立视图则跳过（已确认：MaterialsSelectionPopup 使用独立视图 MaterialsSelectionPopup.axaml，非 GenericSelectionPopup）

## 4. DataGrid 可见 4 行（修复列头占用高度）

- [x] 4.1 将 `GenericSelectionPopup.axaml` 中列表块第一行及 DataGrid 高度从 120 改为 150（列头约 30 + 4×30 数据），使 4 条数据行无需滚动即可全部可见
- [x] 4.2 将列表块第一行及 DataGrid 高度从 150 改为 156（主题列头 36 + 4×30 数据），使无纵向溢出、不触发滚轮
