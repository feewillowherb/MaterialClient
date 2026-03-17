## Why

当前收料员在 `AttendedWeighingWindow` 窗口中缺少针对“台账管理/数据管理”的集中查看入口，无法在称重主流程界面中快速浏览、对比和验收历史称重记录。现有系统虽然有类似 WPF 窗口（`Samples/Index.xaml`）展示完整表格，但在 Avalonia 客户端中尚未落地，影响了操作一致性与验收效率，因此需要新增一个仅做样式和布局验收的对话框。

## What Changes

- 在 `AttendedWeighingWindow` 窗口中点击“数据管理”按钮时，弹出一个对话框窗口，用于展示称重台账列表页面布局。
- 对话框仅实现 UI 布局与样式渲染，不接入真实后端数据、不实现筛选、分页、查询逻辑等交互行为。
- 对话框内容参考 `Samples/Index.xaml` / `Index.xaml.cs` 以及示例截图，实现类似的表头、表格列、分页区、查询条件区等视觉结构。
- 在渲染时内置一条本地测试数据，用于验收表格行样式、列宽、对齐方式等视觉效果（不持久化，也不依赖远端接口）。

## Capabilities

### New Capabilities
- `attended-weighing-data-management-dialog-layout`: 定义“收料称重-数据管理对话框”页面布局规范，包括表格列、查询区、分页区等 UI 结构，以及用于样式验收的本地测试数据要求，仅关注视觉和布局，不涉及业务逻辑。

### Modified Capabilities
- `<existing-name>`: <what requirement is changing>

## Impact

- 影响 `AttendedWeighingWindow` 相关 UI 结构，需要新增一个基于 Avalonia 的对话框视图（及其代码隐藏/视图模型）用于台账列表展示。
- 参考 `Samples/Index.xaml` / `Index.xaml.cs` 中的列定义、表头和分页区域布局，将其迁移或重绘为 Avalonia 风格控件，保证整体视觉风格与现有客户端一致。
- 仅在 UI 层新增布局与示例数据，不改变现有称重业务流程、接口协议或数据库结构，属于前端页面扩展，对后端系统无破坏性影响。
