## 1. 核心实现

- [ ] 1.1 在 `AttendedWeighingViewModel` 中新增 `SelectNextUnfinishedItemAsync` 私有方法，实现优先级逻辑：未完成 Waybill → 未完成 WeighingRecord → 兜底已完成
- [ ] 1.2 修改 `OnDetailCompleteCompleted`，增加 `IsSolidWasteMode` 分支：SolidWaste 沿用 `NavigateToItemAsync`，Standard 调用 `SelectNextUnfinishedItemAsync`
- [ ] 1.3 确保标签页切换规则正确应用（按需切换至未匹配标签页，尊重 IsShowAllRecords）
- [ ] 1.4 确保调用 `SelectViewForItem` 正确选择视图（未完成项打开 DetailView，已完成兜底显示 MainView）

## 2. 测试

- [ ] 2.1 为 `SelectNextUnfinishedItemAsync` 编写单元测试：存在未完成 Waybill → 选中 Waybill
- [ ] 2.2 为 `SelectNextUnfinishedItemAsync` 编写单元测试：无未完成 Waybill，存在未完成 WeighingRecord → 选中 WeighingRecord
- [ ] 2.3 为 `SelectNextUnfinishedItemAsync` 编写单元测试：所有条目已完成 → 兜底至已完成条目
- [ ] 2.4 编写单元测试验证 `OnDetailCompleteCompleted` 在 `IsSolidWasteMode=true` 时仍调用 `NavigateToItemAsync`（SolidWaste 不受影响）
- [ ] 2.5 编写单元测试验证 `OnDetailCompleteCompleted` 在 `IsSolidWasteMode=false` 时调用 `SelectNextUnfinishedItemAsync`（Standard 新逻辑）
