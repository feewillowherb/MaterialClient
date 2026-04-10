## 1. 服务层 — 推荐服务

- [x] 1.1 创建 `MaterialClient.Common/Services/RecommendationService.cs`，包含 `IRecommendationService` 接口（含 `GetRecommendationByPlateNumberAsync` 和 `GetLatestRecommendationAsync`）及 `RecommendationService` 实现类，使用 `[AutoConstructor]`、`ISingletonDependency`
- [x] 1.2 实现 `GetRecommendationByPlateNumberAsync` — 将 `WeighingMatchingService.GetRecommendationByPlateNumberAsync` 中的现有查询逻辑（UnitOfWork、AsNoTracking、OrderByDescending、ProviderId 回退）迁移至 `RecommendationService`
- [x] 1.3 实现内部缓存数据结构 — `IMemoryCache` 配合 `ReaderWriterLockSlim`，缓存键使用前缀格式（`Recommendation_{车牌号}`），上限 200 条，LRU 淘汰（移除最旧 10 条）
- [x] 1.4 实现 `GetLatestRecommendationAsync` — 按车牌号从 `IMemoryCache` 读取 `WaybillRecommendationDto`，未找到则返回 null
- [x] 1.5 实现 `UpdateRecommendationCache` 内部方法 — 从 `Waybill` 实体构建 `WaybillRecommendationDto`，按车牌号键写入缓存，处理重复（原地更新），执行 LRU 淘汰

## 2. 服务层 — WeighingMatchingService 修改

- [x] 2.1 从 `IWeighingMatchingService` 接口和 `WeighingMatchingService` 实现中移除 `GetRecommendationByPlateNumberAsync`
- [x] 2.2 通过构造函数将 `IRecommendationService` 注入 `WeighingMatchingService`（添加到 `[AutoConstructor]` 分部类字段）
- [x] 2.3 在 `CompleteOrderAsync` 中，于 `_recommendPlateNumberService.AddPlateNumberToCache` 之后调用 `_recommendationService.UpdateRecommendationCache(waybill)` 更新推荐缓存
- [x] 2.4 在 `UpdateWaybillAsync` 中，于 `await _waybillRepository.UpdateAsync(waybill)` 之后调用 `_recommendationService.UpdateRecommendationCache(waybill)` 更新推荐缓存（仅当车牌号非 null/空白时）

## 3. 配置 — SystemSettings

- [x] 3.1 在 `MaterialClient.Common/Configuration/SystemSettings.cs` 中新增 `bool EnableLatestRecommendation { get; set; } = false` 属性，附带 XML 文档注释

## 4. 设置 UI — ViewModel

- [x] 4.1 在 `SettingsWindowViewModel` 中新增 `[Reactive] private bool _enableLatestRecommendation` 字段及公开属性 `EnableLatestRecommendation`
- [x] 4.2 在 `LoadSettingsAsync` 中新增 `EnableLatestRecommendation = settings.SystemSettings.EnableLatestRecommendation`
- [x] 4.3 在 `SaveAsync` 中新增 `systemSettings.EnableLatestRecommendation = EnableLatestRecommendation`

## 5. 设置 UI — 视图

- [x] 5.1 在 `SettingsWindow.axaml` 的系统设置区域内新增绑定到 `EnableLatestRecommendation` 的 `ToggleSwitch`，附带适当的标签文字

## 6. ViewModel — 数据源选择

- [x] 6.1 在 `StandardWeighingDetailViewModel` 构造函数中新增 `IRecommendationService` 和 `ISettingsService` 参数，存储为只读字段
- [x] 6.2 在 `LoadModeSpecificDataAsync` 中，将 `_serviceProvider.GetRequiredService<IWeighingMatchingService>()` 替换为注入的 `_recommendationService`
- [x] 6.3 在 `LoadModeSpecificDataAsync` 中，从 `_settingsService.GetSettingsAsync()` 读取 `EnableLatestRecommendation` 并分支：为 true → 调用 `GetLatestRecommendationAsync`，为 false → 调用 `GetRecommendationByPlateNumberAsync`
- [x] 6.4 更新 `AttendedWeighingDetailViewModelBase` 或其派生类的构造函数链，按需传递新的依赖项

## 7. 验证

- [x] 7.1 确认所有变更后应用构建无错误
- [x] 7.2 确认 `GetRecommendationByPlateNumberAsync` 不再被 `IWeighingMatchingService` 及其调用方引用
- [x] 7.3 确认设置页面正确保存和加载 `EnableLatestRecommendation`
- [x] 7.4 确认设置关闭（数据库路径）和设置启用（缓存路径）时推荐数据正确填充
