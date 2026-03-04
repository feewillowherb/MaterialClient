# 实施任务

## 1. 变量重命名
- [x] 1.1 在 `AttendedWeighingService.cs` 中将 `_filteredPlateColors` 重命名为 `_lowPriorityPlateColors`
- [x] 1.2 更新该服务内所有对重命名变量的引用
- [x] 1.3 在配置读取代码中将配置键从 `FilteredPlateColors` 重命名为 `LowPriorityPlateColors`
- [x] 1.4 将 `PlateColorFilterConfig.cs` 中的属性名从 `FilteredPlateColors` 改为 `LowPriorityPlateColors`
- [x] 1.5 将日志措辞从「filtered」改为「low-priority」（低优先级）

## 2. 数据结构更新
- [x] 2.1 在 `PlateNumberCacheRecord` 中增加 `ColorType` 属性（可空 `LprAllInOneColorType?`）
- [x] 2.2 ~~在 `PlateNumberCacheRecord` 中根据颜色增加 `IsLowPriority` 计算属性~~（不需要——在选择时计算）

## 3. 缓存逻辑更新
- [x] 3.1 更新 `OnPlateNumberRecognized()`，在缓存中保存颜色信息
- [x] 3.2 移除对低优先级颜色的提前返回（约第 400–406 行），改为以低优先级存储
- [x] 3.3 更新缓存 AddOrUpdate 逻辑，在增加次数时保留颜色信息

## 4. 选择逻辑更新
- [x] 4.1 更新 `GetMostFrequentPlateNumber()`，实现基于优先级的选择
- [x] 4.2 优先尝试找最频高优先级车牌
- [x] 4.3 仅当不存在高优先级车牌时，回退到最频低优先级车牌
- [x] 4.4 在选中低优先级车牌时增加日志

## 5. 配置迁移
- [x] 5.1 在 appsettings.json 中改用 `LowPriorityPlateColors` 键
- [x] 5.2 ~~为旧键 `FilteredPlateColors` 增加向后兼容检查（可选）~~（决定不兼容）
- [x] 5.3 在发布说明中记录迁移步骤（已在 design.md 中说明）

## 6. 测试
- [x] 6.1 增加高优先级覆盖低优先级的测试
- [x] 6.2 增加无高优先级时使用低优先级的测试
- [x] 6.3 增加低优先级不能覆盖已有高优先级的测试
- [x] 6.4 ~~将现有车牌过滤测试改为反映优先级行为~~（现有测试在新行为下仍通过）
- [x] 6.5 增加缓存中颜色信息持久化测试
- [x] 6.6 测试新键名下的配置加载

## 7. 验证
- [x] 7.1 运行现有测试确保无回归（36/38 通过；2 个与本次变更无关的既有失败）
- [ ] 7.2 在真实硬件配置下测试（若具备）——**需实体硬件**
- [x] 7.3 确认日志能清晰体现优先级选择
- [x] 7.4 确认配置迁移正确
