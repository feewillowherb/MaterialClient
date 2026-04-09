# 推荐数据缓存

## 目的

管理全局唯一的内存推荐数据缓存，存储单个 `WaybillRecommendationDto?` 值，使用 `IMemoryCache` 存取。

## 需求

### 需求：推荐数据缓存结构

系统须维护全局唯一的内存缓存，存储单个 `WaybillRecommendationDto?` 值。缓存须使用 `IMemoryCache`，以固定键 `"Recommendation_Global"` 存取。

#### 场景：全局缓存键格式
- **当** 存储或检索推荐数据时
- **则** 缓存键须使用固定值 `"Recommendation_Global"`，不包含车牌号

#### 场景：缓存条目格式
- **当** 缓存一条运单数据
- **则** 值须为 `WaybillRecommendationDto`，包含 `MaterialId`、`ProviderId`、`MaterialUnitId` 和 `WaybillQuantity`

#### 场景：全局覆盖
- **当** 任意车牌号的运单完成发货或更新
- **则** 系统须用新值覆盖全局缓存，之前的缓存值被替换

### 需求：运单完成时更新缓存

系统须在通过 `CompleteOrderAsync` 完成运单时更新推荐缓存。

#### 场景：完成时更新缓存
- **当** 对任意运单调用 `CompleteOrderAsync`
- **则** 系统须从运单构建 `WaybillRecommendationDto` 并覆盖全局缓存

#### 场景：null 运单跳过缓存更新
- **当** 传入的运单为 null
- **则** 系统须跳过缓存更新

### 需求：运单更新时更新缓存

系统须在通过 `UpdateWaybillAsync` 更新运单时更新推荐缓存。

#### 场景：运单编辑后更新缓存
- **当** 对任意运单调用 `UpdateWaybillAsync`
- **则** 系统须从更新后的运单重新构建 `WaybillRecommendationDto` 并覆盖全局缓存

#### 场景：null 运单跳过缓存更新
- **当** 传入的运单为 null
- **则** 系统须跳过缓存更新
