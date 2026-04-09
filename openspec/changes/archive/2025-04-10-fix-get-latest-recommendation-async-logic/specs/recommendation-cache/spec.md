## MODIFIED Requirements

### Requirement: 推荐数据缓存结构

系统须维护全局唯一的内存缓存，存储单个 `WaybillRecommendationDto?` 值。缓存须使用 `IMemoryCache`，以固定键 `"Recommendation_Global"` 存取。

#### Scenario: 全局缓存键格式
- **WHEN** 存储或检索推荐数据时
- **THEN** 缓存键须使用固定值 `"Recommendation_Global"`，不包含车牌号

#### Scenario: 缓存条目格式
- **WHEN** 缓存一条运单数据
- **THEN** 值须为 `WaybillRecommendationDto`，包含 `MaterialId`、`ProviderId`、`MaterialUnitId` 和 `WaybillQuantity`

#### Scenario: 全局覆盖
- **WHEN** 任意车牌号的运单完成发货或更新
- **THEN** 系统须用新值覆盖全局缓存，之前的缓存值被替换

### Requirement: 运单完成时更新缓存

系统须在通过 `CompleteOrderAsync` 完成运单时更新推荐缓存。

#### Scenario: 完成时更新缓存
- **WHEN** 对任意运单调用 `CompleteOrderAsync`
- **THEN** 系统须从运单构建 `WaybillRecommendationDto` 并覆盖全局缓存

#### Scenario: null 运单跳过缓存更新
- **WHEN** 传入的运单为 null
- **THEN** 系统须跳过缓存更新

### Requirement: 运单更新时更新缓存

系统须在通过 `UpdateWaybillAsync` 更新运单时更新推荐缓存。

#### Scenario: 运单编辑后更新缓存
- **WHEN** 对任意运单调用 `UpdateWaybillAsync`
- **THEN** 系统须从更新后的运单重新构建 `WaybillRecommendationDto` 并覆盖全局缓存

#### Scenario: null 运单跳过缓存更新
- **WHEN** 传入的运单为 null
- **THEN** 系统须跳过缓存更新

## REMOVED Requirements

### Requirement: 缓存 LRU 淘汰
**Reason**: 全局单值缓存无需淘汰策略，固定键直接覆盖
**Migration**: 缓存写入改为 `IMemoryCache.Set("Recommendation_Global", dto)`

### Requirement: 缓存线程安全
**Reason**: 全局单值的 `IMemoryCache.Set/Get` 操作天然线程安全，无需额外锁
**Migration**: 移除 `ReaderWriterLockSlim`，直接使用 `IMemoryCache` 的 `Set` 和 `TryGetValue`
