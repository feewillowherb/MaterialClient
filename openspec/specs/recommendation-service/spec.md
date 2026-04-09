# 推荐服务

## 目的

提供推荐数据查询接口，支持数据库查询和内存缓存两种数据源，从 WeighingMatchingService 中提取推荐逻辑。

## 需求

### 需求：推荐服务接口

系统须提供 `IRecommendationService` 接口，包含以下方法：
- `Task<WaybillRecommendationDto?> GetRecommendationByPlateNumberAsync(string plateNumber)` — 根据车牌号查询数据库获取最新已完成运单
- `Task<WaybillRecommendationDto?> GetLatestRecommendationAsync()` — 从全局内存缓存读取推荐数据（无参数）

#### 场景：数据库查询返回推荐数据
- **当** 使用有效车牌号调用 `GetRecommendationByPlateNumberAsync`
- **且** 该车牌号存在已完成的运单
- **则** 系统须返回 `WaybillRecommendationDto`，包含最新已完成运单（按 JoinTime/AddDate 降序排列）的 MaterialId、ProviderId、MaterialUnitId 和 WaybillQuantity

#### 场景：未知车牌号数据库查询返回空
- **当** 使用无已完成运单记录的车牌号调用 `GetRecommendationByPlateNumberAsync`
- **则** 系统须返回 `null`

#### 场景：空车牌号数据库查询返回空
- **当** 使用 null 或空白字符串作为车牌号调用 `GetRecommendationByPlateNumberAsync`
- **则** 系统须返回 `null`，不查询数据库

#### 场景：缓存查找返回已缓存的推荐数据
- **当** 调用 `GetLatestRecommendationAsync()`
- **且** 全局缓存中存在推荐数据
- **则** 系统须返回缓存的 `WaybillRecommendationDto`

#### 场景：缓存为空时返回 null
- **当** 调用 `GetLatestRecommendationAsync()`
- **且** 全局缓存中无推荐数据
- **则** 系统须返回 `null`

#### 场景：数据库查询对 ProviderId 回退到任意运单
- **当** 最新已完成运单的 `ProviderId == null`
- **则** 系统须尝试查找同一车牌号下具有非空 ProviderId 的任意运单，并在结果中使用该 ProviderId

### 需求：推荐服务实现

系统须将 `RecommendationService` 实现为单例服务（`ISingletonDependency`），使用 `[AutoConstructor]` 特性，遵循 ABP 约定。

#### 场景：服务注册
- **当** 应用初始化依赖注入容器
- **则** 系统须将 `RecommendationService` 注册为实现 `IRecommendationService` 的单例

#### 场景：构造注入
- **当** `RecommendationService` 被实例化
- **则** 系统须通过构造注入接收：`IRepository<Waybill, long>`、`ILogger<RecommendationService>` 和 `IMemoryCache`

### 需求：从 WeighingMatchingService 提取

`GetRecommendationByPlateNumberAsync` 方法须从 `IWeighingMatchingService` 和 `WeighingMatchingService` 中移除。实现须迁移至 `RecommendationService`，不改变行为。

#### 场景：WeighingMatchingService 不再暴露推荐查询
- **当** 检查 `IWeighingMatchingService` 接口
- **则** 不得包含 `GetRecommendationByPlateNumberAsync`

#### 场景：现有调用方使用新服务
- **当** `StandardWeighingDetailViewModel` 需要推荐数据
- **则** 须使用 `IRecommendationService` 而非 `IWeighingMatchingService`
