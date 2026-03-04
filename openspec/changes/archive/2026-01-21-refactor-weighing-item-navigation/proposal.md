# 变更：重构称重条目导航与跟踪

## 为什么

当前有人值守称重流程在操作（保存、完成、匹配、作废）后存在不一致的对象跟踪。`AttendedWeighingDetailViewModel` 中的事件处理程序无法正确收尾 `WeighingListItemDto` 对象，导致：

1. **操作后上下文丢失**：用户完成一单后，界面未导航到正确条目或视图
2. **视图显示错误**：系统未根据条目状态在 MainView（摘要）与 DetailView（编辑）间正确切换
3. **标签页导航混乱**：条目在状态间移动（未匹配 → 已完成）时，标签选择未跟随条目
4. **分页盲区**：若目标条目在另一页，系统无法找到或导航到该页

这破坏了用户工作流，需要手动搜索才能继续操作。

## 变更内容

- **事件基础设施增强**：在事件参数中提供完整操作上下文（条目 ID、类型、完成状态、操作类型）
- **统一导航逻辑**：建立集中的 `NavigateToItemAsync` 方法，处理所有操作后导航
- **智能标签切换**：实现尊重 `IsShowAllRecords` 标志的规则，仅在必要时切换标签
- **跨页条目查找**：增加分页导航以在多页间查找并选中条目
- **视图选择自动化**：根据条目类型与完成状态自动选择 MainView 或 DetailView

## 影响

### 涉及的规范
- `attended-weighing`（新能力）— 定义条目跟踪与导航行为的需求

### 涉及的代码
- `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs` — 事件定义与触发
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` — 导航逻辑与事件处理
- `MaterialClient.Common/Events/` — 新事件参数类

### 破坏性变更
无 — 此为内部重构，在不变更对外 API 的前提下改进现有行为。

### 用户可见变更
- 完成一单后，用户停留在 MainView 中该已完成运单上（此前会丢失选中）
- 在需要时自动切换到显示更新后条目的标签（尊重「全部记录」模式）
- 系统自动跨页查找条目（此前仅搜索当前页）
- 根据条目状态显示正确视图（MainView/DetailView）

## 迁移

无需迁移 — 仅为行为改进，非破坏性变更。
