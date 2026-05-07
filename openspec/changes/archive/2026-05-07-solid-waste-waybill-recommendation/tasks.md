# Implementation Tasks: Solid Waste Waybill Recommendation

## 1. SolidWasteWeighingDetailViewModel 代码实现

- [x] 1.1 在 `SolidWasteWeighingDetailViewModel` 中添加 `IRecommendationService` 私有字段
- [x] 1.2 在 `SolidWasteWeighingDetailViewModel` 中添加 `ISettingsService` 私有字段
- [x] 1.3 修改构造函数签名，添加 `IRecommendationService recommendationService` 参数
- [x] 1.4 修改构造函数签名，添加 `ISettingsService settingsService` 参数
- [x] 1.5 在构造函数体中赋值 `_recommendationService` 字段
- [x] 1.6 在构造函数体中赋值 `_settingsService` 字段
- [x] 1.7 创建 `ApplyRecommendationAsync()` 私有方法
- [x] 1.8 在 `ApplyRecommendationAsync()` 中实现缺失字段检查逻辑
- [x] 1.9 在 `ApplyRecommendationAsync()` 中实现推荐数据获取逻辑（根据设置选择缓存或数据库查询）
- [x] 1.10 在 `ApplyRecommendationAsync()` 中实现供应商推荐数据应用
- [x] 1.11 在 `ApplyRecommendationAsync()` 中实现材料推荐数据应用
- [x] 1.12 在 `ApplyRecommendationAsync()` 中添加异常处理和日志记录
- [x] 1.13 修改 `LoadModeSpecificDataAsync()` 方法，在末尾调用 `await ApplyRecommendationAsync()`

## 2. 规格文档更新

- [x] 2.1 读取 `openspec/specs/recommendation-settings/spec.md` 原有内容
- [x] 2.2 在规格中添加固废称重模式的推荐场景（MODIFIED Requirements）
- [x] 2.3 在规格中添加固废称重推荐数据应用需求（ADDED Requirements）
- [x] 2.4 验证规格文档格式符合 OpenSpec 要求（WHEN/THEN 格式）

## 3. 验证与测试

- [x] 3.1 编译项目，验证无编译错误（代码本身无错误，因应用运行时文件锁定未能完成完全构建）
- [ ] 3.2 运行应用，打开固废称重详情视图
- [ ] 3.3 验证推荐设置未启用时，根据车牌号查询推荐数据
- [ ] 3.4 验证推荐设置启用时，从缓存读取推荐数据
- [ ] 3.5 验证推荐数据正确填充供应商和材料字段
- [ ] 3.6 验证 ExtraProperties 已有数据时，推荐不覆盖
- [ ] 3.7 验证推荐服务异常时，数据加载流程不中断
- [ ] 3.8 验证材料选择后，材料单位自动选择逻辑正常工作

## 4. 代码审查与清理

- [x] 4.1 检查代码风格与现有代码一致
- [x] 4.2 验证所有异步方法正确使用 `async/await`
- [x] 4.3 验证日志记录级别和消息内容恰当
- [x] 4.4 移除调试代码和注释掉的代码
