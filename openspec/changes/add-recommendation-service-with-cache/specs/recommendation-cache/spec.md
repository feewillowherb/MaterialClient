## 新增需求

### 需求：推荐数据缓存结构

系统须维护按车牌号索引的内存缓存，存储 `WaybillRecommendationDto` 值。缓存须使用 `IMemoryCache` 配合 `ReaderWriterLockSlim` 实现线程安全访问，遵循 `RecommendPlateNumberService` 建立的模式。

#### 场景：缓存键格式
- **当** 存储或检索推荐数据时
- **则** 缓存键须使用前缀格式（如 `Recommendation_{车牌号}`），避免与其他缓存条目冲突

#### 场景：缓存条目格式
- **当** 缓存一条运单数据
- **则** 值须为 `WaybillRecommendationDto`，包含 `MaterialId`、`ProviderId`、`MaterialUnitId` 和 `WaybillQuantity`

### 需求：运单完成时更新缓存

系统须在通过 `CompleteOrderAsync` 完成运单时更新推荐缓存。

#### 场景：完成时更新缓存
- **当** 对具有非空车牌号的运单调用 `CompleteOrderAsync`
- **则** 系统须从运单构建 `WaybillRecommendationDto` 并按车牌号键存入缓存

#### 场景：空车牌号跳过缓存更新
- **当** 对车牌号为 null 或空白的运单调用 `CompleteOrderAsync`
- **则** 系统须跳过缓存更新

### 需求：运单更新时更新缓存

系统须在通过 `UpdateWaybillAsync` 更新运单时更新推荐缓存。

#### 场景：运单编辑后更新缓存
- **当** 对具有非空车牌号的运单调用 `UpdateWaybillAsync`
- **则** 系统须从更新后的运单重新构建 `WaybillRecommendationDto` 并按车牌号键存入缓存

#### 场景：空车牌号跳过缓存更新
- **当** 对车牌号为 null 或空白的运单调用 `UpdateWaybillAsync`
- **则** 系统须跳过缓存更新

### 需求：缓存 LRU 淘汰

系统须强制执行最大缓存容量为 200 条。达到上限时，系统须移除最旧的 10 条记录。

#### 场景：缓存已达容量
- **当** 新增条目且缓存已包含 200 条记录
- **则** 系统须在添加新条目前移除最旧的 10 条记录

#### 场景：缓存未达容量
- **当** 新增条目且缓存少于 200 条记录
- **则** 系统须直接添加条目，不执行淘汰

#### 场景：重复车牌号更新
- **当** 该车牌号的缓存条目已存在
- **则** 系统须用新的推荐数据更新现有条目（不创建重复条目）

### 需求：缓存线程安全

所有缓存读写操作须受 `ReaderWriterLockSlim` 保护。

#### 场景：并发读取
- **当** 多个线程同时调用 `GetLatestRecommendationAsync`
- **则** 所有读取须正常完成，无数据损坏

#### 场景：并发读写
- **当** 一个线程写入的同时另一个线程读取
- **则** 读取线程须看到旧值或新值，不会出现部分状态
