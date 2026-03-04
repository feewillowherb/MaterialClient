## 1. 事件基础设施

- [x] 1.1 创建带操作上下文字段的 `ItemOperationCompletedEventArgs` 类
- [x] 1.2 在 `AttendedWeighingDetailViewModel` 中更新事件定义为使用新事件参数
- [x] 1.3 在 Save/Complete/Match/Abolish 方法中修改事件调用，传入完整上下文
- [x] 1.4 确认事件参数包含：ItemId、ItemType、OrderType、IsCompleted、OperationType

## 2. 核心导航逻辑

- [x] 2.1 在 `AttendedWeighingViewModel` 中实现 `NavigateToItemAsync(ItemOperationCompletedEventArgs)` 方法
- [x] 2.2 抽取 `DetermineTargetTab(WeighingListItemDto)` 逻辑，尊重 `IsShowAllRecords` 标志
- [x] 2.3 实现 `FindItemAcrossPagesAsync(long itemId, WeighingListItemType)` 用于分页搜索
- [x] 2.4 根据 ItemType 与 OrderType 添加视图选择逻辑（主视图 vs 详情视图）

## 3. 事件处理重构

- [x] 3.1 将 `OnDetailSaveCompleted` 重构为使用统一 `NavigateToItemAsync`
- [x] 3.2 将 `OnDetailCompleteCompleted` 重构为使用统一 `NavigateToItemAsync`
- [x] 3.3 将 `OnDetailMatchCompleted` 重构为使用统一 `NavigateToItemAsync`
- [x] 3.4 将 `OnDetailAbolishCompleted` 重构为使用统一 `NavigateToItemAsync`
- [x] 3.5 将 `OnDetailManualMatchSaveCompleted` 重构为使用统一 `NavigateToItemAsync`

## 4. 标签切换逻辑

- [x] 4.1 实现标签切换决策逻辑（尊重 `IsShowAllRecords`）
- [x] 4.2 仅当当前标签不包含目标条目时进行标签切换
- [x] 4.3 确保 `IsShowUnmatched` ↔ `IsShowCompleted` 切换正确

## 5. 测试与验证

- [x] 5.1 测试保存操作：验证条目选中、标签状态、视图显示
- [x] 5.2 测试完成操作：验证已完成运单在正确标签/页上以主视图显示
- [x] 5.3 测试匹配操作：验证导航到已匹配运单
- [x] 5.4 测试作废操作：验证导航到下一未匹配条目
- [x] 5.5 测试跨页导航：验证条目在不同页时能定位
- [x] 5.6 测试标签切换：验证尊重 `IsShowAllRecords` 标志
- [x] 5.7 测试视图选择：验证已完成运单为主视图、其他为详情视图
- [x] 5.8 确认有人值守称重工作流无回归

## 6. 文档

- [x] 6.1 更新代码注释以说明导航逻辑
- [x] 6.2 在 AttendedWeighingViewModel 中记录标签切换规则
- [x] 6.3 记录事件参数结构与用法
