## 1. 接口签名变更

- [x] 1.1 将 `IRecommendationService.GetLatestRecommendationAsync` 签名从 `GetLatestRecommendationAsync(string plateNumber)` 改为 `GetLatestRecommendationAsync()`
- [x] 1.2 更新 XML 文档注释，移除车牌号参数描述

## 2. RecommendationService 实现简化

- [x] 2.1 替换常量：移除 `CacheKeyPrefix`、`CacheIndexKey`、`MaxCacheSize`、`EvictCount`，新增 `GlobalCacheKey = "Recommendation_Global"`
- [x] 2.2 移除 `ReaderWriterLockSlim _lock` 字段
- [x] 2.3 简化 `GetLatestRecommendationAsync()`：移除 `plateNumber` 参数，直接 `_memoryCache.TryGetValue(GlobalCacheKey, out WaybillRecommendationDto? dto)`
- [x] 2.4 简化 `UpdateRecommendationCache(Waybill)`：移除车牌号非空检查、index 管理、LRU 淘汰逻辑，改为直接 `_memoryCache.Set(GlobalCacheKey, dto)`
- [x] 2.5 删除私有辅助方法 `BuildCacheKey`、`GetOrCreateIndex`、`UpdateIndex`
- [x] 2.6 更新类级别 XML 文档注释，反映全局缓存语义

## 3. ViewModel 调用适配

- [x] 3.1 更新 `StandardWeighingDetailViewModel.LoadModeSpecificDataAsync` 中 `GetLatestRecommendationAsync()` 调用，移除 `PlateNumber` 参数
- [x] 3.2 调整 `needsRecommendation` 条件：将 `!string.IsNullOrWhiteSpace(PlateNumber)` 从全局条件中移除，仅在数据库查询路径（`EnableLatestRecommendation == false`）时检查车牌号
- [x] 3.3 验证推荐数据应用逻辑（L126-174）无需修改

## 4. 规格文档更新

- [x] 4.1 更新 `openspec/specs/recommendation-cache/spec.md`：将按车牌索引的 LRU 缓存描述替换为全局唯一缓存
- [x] 4.2 更新 `openspec/specs/recommendation-service/spec.md`：将 `GetLatestRecommendationAsync` 接口描述改为无参
- [x] 4.3 更新 `openspec/specs/recommendation-settings/spec.md`：更新调用签名和条件判断描述
