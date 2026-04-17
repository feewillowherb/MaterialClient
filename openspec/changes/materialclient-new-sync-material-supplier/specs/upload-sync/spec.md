## ADDED Requirements

### Requirement: 上行同步服务接口

系统须提供 `IUploadSyncService` 接口，包含以下方法：
- `Task UploadAllPendingAsync(CancellationToken ct)` — 上传所有待同步的物料和供应商
- `Task<UploadSyncSummary> UploadPendingMaterialsAsync(CancellationToken ct)` — 仅上传待同步的物料
- `Task<UploadSyncSummary> UploadPendingProvidersAsync(CancellationToken ct)` — 仅上传待同步的供应商

#### Scenario: 上传所有待同步项成功

- **WHEN** 调用 `UploadAllPendingAsync`
- **AND** `SyncState` 中存在待同步的物料和供应商
- **THEN** 系统须先通过批量端点上传物料，再通过批量端点上传供应商
- **AND** 返回 `UploadSyncSummary`，包含已应用、冲突和失败项的计数

#### Scenario: 无待同步项

- **WHEN** 调用 `UploadAllPendingAsync`
- **AND** 不存在状态为 `Pending` 的 `SyncState` 条目
- **THEN** 系统须立即返回，不发起 API 调用
- **AND** 摘要须指示零项已处理

#### Scenario: 上传过程中取消

- **WHEN** 调用 `UploadAllPendingAsync`
- **AND** 在批处理期间 `CancellationToken` 被触发
- **THEN** 系统须停止处理剩余批次
- **AND** 已处理的批次须保留其更新后的同步状态

### Requirement: 分批上传与自动分块

系统须按照后端要求，以每批最多 100 条的方式分批上传待同步项。

#### Scenario: 待同步物料超过 100 条

- **WHEN** 存在 250 条待同步物料的 `SyncState` 条目
- **THEN** 系统须发起 3 次 API 调用：分别为 100、100 和 50 条
- **AND** 每个批次须独立处理（一个批次失败不得阻止其他批次）

#### Scenario: 批量 API 调用遇到瞬态错误

- **WHEN** 批量上传 API 调用因瞬态 HTTP 错误（5xx、超时）失败
- **THEN** 系统须通过 Refit 客户端上配置的现有 Polly 重试策略进行重试
- **AND** 重试耗尽后，失败批次中的条目须保持 `Pending` 状态

### Requirement: 每条同步状态对应唯一幂等键

每条 `SyncState` 条目须在创建时生成唯一的 `ClientRequestId`（GUID）。该键须在每次上传尝试时发送。

#### Scenario: 使用相同幂等键重试

- **WHEN** 上传尝试失败并重试
- **THEN** 系统须发送 `SyncState` 条目中相同的 `ClientRequestId`
- **AND** 如果原始请求已成功，服务端须返回缓存的响应

#### Scenario: 新实体获得新幂等键

- **WHEN** 为某个实体创建新的 `SyncState` 条目
- **THEN** 系统须为 `ClientRequestId` 生成新的 `Guid`

### Requirement: 基于版本跟踪的乐观并发

系统须在每次上传请求中包含 `SyncState.LocalVersion` 作为 `baseVersion`。服务端响应中的版本须存储在 `SyncState.ServerVersion` 中。

#### Scenario: 版本匹配 — 已应用

- **WHEN** 服务端返回 `status = "applied"` 及 `version`
- **THEN** 系统须将 `SyncState.ServerVersion` 更新为返回的版本
- **AND** 将 `SyncState.Status` 设为 `Applied`

#### Scenario: 版本冲突

- **WHEN** 服务端返回 `status = "conflict"` 及 `serverVersion` 和 `serverData`
- **THEN** 系统须将 `SyncState.Status` 设为 `Conflict`
- **AND** 将 `SyncState.ServerVersion` 存储为服务端版本
- **AND** 将服务端数据应用到本地实体（服务端优先策略）

#### Scenario: 无效请求

- **WHEN** 服务端返回 `status = "invalid"` 及 `validationErrors`
- **THEN** 系统须记录验证错误
- **AND** 保持 `SyncState.Status` 为 `Pending`

### Requirement: 同步端点的 Refit API 客户端

系统须扩展 `IMaterialPlatformApi`，为所有同步 API 端点添加 Refit 方法。

#### Scenario: 单条物料上行同步端点

- **WHEN** 上传单条物料
- **THEN** 系统须调用 `POST /Sync/UpsertMaterialGood`，请求体为 `UpsertMaterialGoodDto`
- **AND** 接收 `UpsertResultDto` 响应

#### Scenario: 批量物料上行同步端点

- **WHEN** 批量上传物料
- **THEN** 系统须调用 `POST /Sync/UpsertMaterialGoodsBatch`，请求体为 `UpsertBatchRequestDto<UpsertMaterialGoodDto>`
- **AND** 接收 `List<UpsertResultDto>` 响应

#### Scenario: 单条供应商上行同步端点

- **WHEN** 上传单条供应商
- **THEN** 系统须调用 `POST /Sync/UpsertMaterialProvider`，请求体为 `UpsertMaterialProviderDto`
- **AND** 接收 `UpsertResultDto` 响应

#### Scenario: 批量供应商上行同步端点

- **WHEN** 批量上传供应商
- **THEN** 系统须调用 `POST /Sync/UpsertMaterialProviderBatch`，请求体为 `UpsertBatchRequestDto<UpsertMaterialProviderDto>`
- **AND** 接收 `List<UpsertResultDto>` 响应

#### Scenario: 变更日志查询端点

- **WHEN** 查询服务端变更日志
- **THEN** 系统须调用 `GET /Sync/Changes?sinceChangeId={id}&entityType={type}&limit={n}`
- **AND** 接收 `List<SyncChangeItemDto>` 响应

### Requirement: 同步完成后发送 MessageBus 通知

系统须在成功完成上行同步后发送 MessageBus 消息，以便 ViewModel 刷新缓存数据（例如，推荐缓存失效）。

#### Scenario: 物料同步成功

- **WHEN** 一条或多条物料成功上传（status = applied）
- **THEN** 系统须发布 `MaterialSyncedMessage`，包含已同步实体 ID 列表
- **AND** 消息须在任意线程发送（非 UI 线程）

#### Scenario: 供应商同步成功

- **WHEN** 一条或多条供应商成功上传（status = applied）
- **THEN** 系统须发布 `ProviderSyncedMessage`，包含已同步实体 ID 列表

#### Scenario: 无同步项

- **WHEN** 上行同步完成，但已应用项为零
- **THEN** 系统不得发布任何 MessageBus 消息

### Requirement: 与 PollingBackgroundService 集成

`PollingBackgroundService` 须在所有下载同步步骤完成后包含上行同步步骤。

#### Scenario: 上行同步在下载同步之后执行

- **WHEN** `PollingBackgroundService.DoWorkAsync` 执行
- **THEN** 系统须先运行下载同步步骤（VerifyAuth、SyncMaterial、SyncMaterialType、SyncProvider）
- **AND** 然后运行 `IUploadSyncService.UploadAllPendingAsync`
- **AND** 最后按原有逻辑运行 PushWaybill 和 UploadAttachments

#### Scenario: 上行同步失败不阻塞后续步骤

- **WHEN** 上行同步步骤抛出异常
- **THEN** 异常须被捕获并记录
- **AND** 后续步骤（PushWaybill、UploadAttachments）须继续执行

### Requirement: 同步操作的 DTO 定义

系统须在 `MaterialClient.Common/Api/Dtos/` 中定义以下 record 类型：

- `UpsertMaterialGoodDto` — 对应服务端 DTO：Action, GoodsId, GoodsName, GoodsCode, Specifications, BasicUnit, UpperLimit, LowerLimit, MaterialTypeId, ProId, CoId, Units, baseVersion, clientRequestId
- `UpsertMaterialProviderDto` — 对应服务端 DTO：Action, ProviderId, ProviderName, ContectName, ContectPhone, UsciCode, CoId, MaterialTypeId, baseVersion, clientRequestId
- `UpsertBatchRequestDto<T>` — 泛型批量包装器，包含 Items 列表
- `UpsertResultDto` — 服务端响应：status, entityId, version, serverVersion, serverData, conflictFields, validationErrors, message
- `SyncChangeItemDto` — 变更日志项：ChangeId, EntityType, EntityId, Action, Version, ChangedAtUtc, Payload
- `SyncChangesQueryDto` — 查询参数：sinceChangeId, entityType, limit

#### Scenario: DTO 字段命名与服务端匹配

- **WHEN** 同步 DTO 被序列化为 JSON
- **THEN** 字段名须与服务端期望的 JSON 属性名匹配（baseVersion、clientRequestId 使用 camelCase）

### Requirement: UploadSyncService 作为单例并使用 AutoConstructor

`UploadSyncService` 须通过 ABP 约定（`ISingletonDependency`）注册为单例，并使用 `[AutoConstructor]` 进行依赖注入。

#### Scenario: 服务注册

- **WHEN** 应用初始化 DI 容器
- **THEN** `UploadSyncService` 须注册为实现 `IUploadSyncService` 的单例
- **AND** 构造函数参数须自动注入：`IRepository<Material, int>`、`IRepository<Provider, int>`、`IRepository<SyncState, int>`、`IMaterialPlatformApi`、`ILogger<UploadSyncService>`
