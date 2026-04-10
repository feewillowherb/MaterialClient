## MODIFIED Requirements

### Requirement: 推荐服务接口

系统须提供 `IRecommendationService` 接口，包含以下方法：
- `Task<WaybillRecommendationDto?> GetRecommendationByPlateNumberAsync(string plateNumber)` — 根据车牌号查询数据库获取最新已完成运单（不变）
- `Task<WaybillRecommendationDto?> GetLatestRecommendationAsync()` — 从全局内存缓存读取推荐数据（无参数）

#### Scenario: 缓存查找返回已缓存的推荐数据
- **WHEN** 调用 `GetLatestRecommendationAsync()`
- **AND** 全局缓存中存在推荐数据
- **THEN** 系统须返回缓存的 `WaybillRecommendationDto`

#### Scenario: 缓存为空时返回 null
- **WHEN** 调用 `GetLatestRecommendationAsync()`
- **AND** 全局缓存中无推荐数据
- **THEN** 系统须返回 `null`

#### Scenario: 数据库查询返回推荐数据
- **WHEN** 使用有效车牌号调用 `GetRecommendationByPlateNumberAsync`
- **AND** 该车牌号存在已完成的运单
- **THEN** 系统须返回 `WaybillRecommendationDto`，包含最新已完成运单（按 JoinTime/AddDate 降序排列）的 MaterialId、ProviderId、MaterialUnitId 和 WaybillQuantity

#### Scenario: 未知车牌号数据库查询返回空
- **WHEN** 使用无已完成运单记录的车牌号调用 `GetRecommendationByPlateNumberAsync`
- **THEN** 系统须返回 `null`

#### Scenario: 空车牌号数据库查询返回空
- **WHEN** 使用 null 或空白字符串作为车牌号调用 `GetRecommendationByPlateNumberAsync`
- **THEN** 系统须返回 `null`，不查询数据库

#### Scenario: 数据库查询对 ProviderId 回退到任意运单
- **WHEN** 最新已完成运单的 `ProviderId == null`
- **THEN** 系统须尝试查找同一车牌号下具有非空 ProviderId 的任意运单，并在结果中使用该 ProviderId
