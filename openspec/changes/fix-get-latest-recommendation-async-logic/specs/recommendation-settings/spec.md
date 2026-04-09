## MODIFIED Requirements

### Requirement: 明细 ViewModel 中的数据源选择

`StandardWeighingDetailViewModel` 须从 `ISettingsService` 读取 `EnableLatestRecommendation` 并选择相应的推荐数据源。

#### Scenario: 默认行为（设置未启用）
- **WHEN** `EnableLatestRecommendation` 为 `false`
- **AND** 当前车牌号非空
- **THEN** ViewModel 须调用 `IRecommendationService.GetRecommendationByPlateNumberAsync(plateNumber)` 查询数据库

#### Scenario: 默认行为车牌为空
- **WHEN** `EnableLatestRecommendation` 为 `false`
- **AND** 当前车牌号为 null 或空白
- **THEN** ViewModel 须跳过推荐数据获取

#### Scenario: 启用最新推荐
- **WHEN** `EnableLatestRecommendation` 为 `true`
- **THEN** ViewModel 须调用 `IRecommendationService.GetLatestRecommendationAsync()` 从全局缓存读取（不要求车牌号非空）

#### Scenario: 构造注入
- **WHEN** 构造 `StandardWeighingDetailViewModel`
- **THEN** 须通过构造注入接收 `IRecommendationService` 和 `ISettingsService`
