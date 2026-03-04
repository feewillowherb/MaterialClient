## 新增需求

### 需求：操作后条目导航

系统应在执行操作（保存、完成、匹配、作废）后，提供对 WeighingListItemDto 对象的一致导航。

#### 场景：完成操作后的导航
- **当**用户在 AttendedWeighingDetailView 中完成运单（首磅 → 已完成）时
- **则**系统应：
  - 刷新列表数据以反映已完成状态
  - 在列表中选中新完成的运单
  - 在 AttendedWeighingMainView（而非详情视图）中显示该已完成运单
  - 若条目不在当前页，则导航到正确页
  - 仅在必要时切换到相应标签（尊重 IsShowAllRecords 标志）

#### 场景：保存操作后的导航
- **当**用户在 AttendedWeighingDetailView 中保存称重记录或运单的修改时
- **则**系统应：
  - 刷新列表数据以反映保存后的变更
  - 在列表中保持已保存条目为选中状态
  - 保持在 AttendedWeighingDetailView（允许继续编辑）
  - 保持当前标签（条目状态未变）
  - 若因排序导致条目移动，则导航到正确页

#### 场景：匹配操作后的导航
- **当**用户手动将一条称重记录与另一条匹配时
- **则**系统应：
  - 刷新列表数据以显示新生成的运单
  - 在列表中选中下一未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一未匹配条目
  - 若当前在已完成标签且未显示全部记录，则切换到未匹配标签
  - 为下一条目导航到正确页

#### 场景：作废操作后的导航
- **当**用户作废（删除）一条称重记录时
- **则**系统应：
  - 刷新列表数据以移除已作废记录
  - 在列表中选中下一未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一未匹配条目
  - 保持当前标签（条目被移除而非移动）
  - 为下一条目导航到正确页

### 需求：标签切换规则

系统应实现尊重用户上下文、仅在必要时切换的智能标签逻辑。

#### 场景：标签切换尊重「全部记录」模式
- **当** IsShowAllRecords 标志为 true（用户选择了「全部记录」标签）时
- **则**系统在任何操作后均不得自动切换标签
- **原因**：该标签下所有条目均可见，与完成状态无关

#### 场景：条目变为已完成时的标签切换
- **当**条目变为已完成（OrderType 变为 Completed）
- **且** IsShowUnmatched 为 true（用户在「未匹配」标签）
- **且** IsShowAllRecords 为 false
- **则**系统应切换到 IsShowCompleted = true（「已完成」标签）

#### 场景：条目变为未匹配时的标签切换
- **当**条目变为未匹配（OrderType 变为 FirstWeight 或 Unmatch）
- **且** IsShowCompleted 为 true（用户在「已完成」标签）
- **且** IsShowAllRecords 为 false
- **则**系统应切换到 IsShowUnmatched = true（「未匹配」标签）

#### 场景：当前标签已包含目标条目时不切换
- **当**操作完成且结果条目的状态与当前标签筛选一致时
- **则**系统不得切换标签
- **示例**：用户在「已完成」标签下保存一条已完成运单 → 保持「已完成」标签

### 需求：跨页条目导航

系统应能跨分页边界查找并导航到条目。

#### 场景：条目在当前页
- **当**操作后导航到目标条目
- **且**目标条目在当前页
- **则**系统应：
  - 立即选中该条目且不翻页
  - 在 O(1) 时间内完成导航

#### 场景：条目在其他页
- **当**操作后导航到目标条目
- **且**目标条目不在当前页
- **则**系统应：
  - 从第 1 页起跨页搜索
  - 导航到包含目标条目的页
  - 找到后选中该条目
  - 将搜索限制在最多 10 页以防过度加载

#### 场景：搜索后未找到条目
- **当**操作后导航到目标条目
- **且**在可用页中搜索后仍无法找到目标条目
- **则**系统应：
  - 回退为选中当前列表第一项
  - 记录关于缺失条目的警告日志
  - 不向用户显示错误（优雅降级）

### 需求：按条目状态的视图选择

系统应根据条目的类型与完成状态自动选择主视图或详情视图。

#### 场景：已完成运单在主视图中显示
- **当**导航到的条目为 Waybill
- **且**该运单的 OrderType 为 Completed
- **则**系统应显示 AttendedWeighingMainView（只读摘要视图）

#### 场景：可编辑条目在详情视图中显示
- **当**导航到的条目不是已完成运单时
- **示例**：WeighingRecord（未匹配）、OrderType = FirstWeight 的 Waybill
- **则**系统应显示 AttendedWeighingDetailView（可编辑表单视图）

#### 场景：完成操作后的视图选择
- **当**用户完成运单（将 OrderType 从 FirstWeight 改为 Completed）时
- **则**系统应从 AttendedWeighingDetailView 切换到 AttendedWeighingMainView
- **原因**：条目现已只读，主视图更适合查看

### 需求：操作事件上下文

系统应在操作事件中提供完整上下文信息，以支持正确导航。

#### 场景：事件参数包含操作上下文
- **当**在 AttendedWeighingDetailView 中完成操作（保存、完成、匹配、作废）时
- **则**触发的事件应包含：
  - ItemId：结果条目的 ID
  - ItemType：条目是 WeighingRecord 还是 Waybill
  - OrderType：当前订单类型（Unmatch、FirstWeight、Completed）
  - IsCompleted：用于快速判断完成状态的布尔标志
  - OperationType：标识所执行操作的字符串

#### 场景：完成操作事件
- **当**用户成功完成运单时
- **则**应触发 CompleteCompleted 事件，且：
  - ItemId = 运单 ID
  - ItemType = Waybill
  - OrderType = Completed
  - IsCompleted = true
  - OperationType = "Complete"

#### 场景：保存操作事件
- **当**用户保存对记录或运单的修改时
- **则**应触发 SaveCompleted 事件，且：
  - ItemId = 已保存条目的 ID
  - ItemType = 条目当前类型
  - OrderType = 条目当前订单类型
  - IsCompleted = 根据 OrderType
  - OperationType = "Save"

### 需求：统一导航逻辑

系统应使用单一统一方法处理所有操作后导航，以保证一致性。

#### 场景：所有操作均使用 NavigateToItemAsync
- **当**任一操作事件处理被触发（保存、完成、匹配、作废、手动匹配）时
- **则**处理方应调用统一的 NavigateToItemAsync 方法
- **且** NavigateToItemAsync 应负责：
  - 数据刷新
  - 标签切换决策
  - 页导航
  - 条目选中
  - 视图选择

#### 场景：导航逻辑可预测且可测
- **当**测试导航行为时
- **则**所有导航路径均应经过 NavigateToItemAsync
- **从而**实现单点测试与维护
- **并保证**所有操作行为一致
