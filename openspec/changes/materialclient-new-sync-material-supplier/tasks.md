## 1. 同步 DTO 和 API 客户端

- [ ] 1.1 创建 `MaterialClient.Common/Api/Dtos/SyncDtos.cs`，包含 record 类型：`UpsertMaterialGoodDto`、`UpsertMaterialProviderDto`、`UpsertBatchRequestDto<T>`、`UpsertResultDto`、`SyncChangeItemDto`、`SyncChangesQueryDto`。确保 JSON 属性名与服务端匹配（`baseVersion`、`clientRequestId` 使用 camelCase）。
- [ ] 1.2 扩展 `MaterialClient.Common/Api/IMaterialPlatformApi.cs` 中的 `IMaterialPlatformApi`，添加 5 个 Refit 方法：`UpsertMaterialGoodAsync`、`UpsertMaterialProviderAsync`、`UpsertMaterialGoodsBatchAsync`、`UpsertMaterialProviderBatchAsync`、`GetSyncChangesAsync`。

## 2. SyncState 实体和 EF Core

- [ ] 2.1 创建 `MaterialClient.Common/Entities/SyncState.cs` 实体，包含属性：Id, EntityType（enum: Material=0, Provider=1）, EntityId, LocalVersion, ServerVersion, Status（enum: Pending=0, Applied=1, Conflict=2）, ClientRequestId, LastAttemptAt, RetryCount, CreatedAt, UpdatedAt。
- [ ] 2.2 创建 `MaterialClient.EFCore/EntityConfigurations/SyncStateConfiguration.cs`，使用 Fluent API：必填字段、`(EntityType, EntityId)` 唯一索引、表名 `SyncStates`。
- [ ] 2.3 在 `MaterialClientDbContext` 中添加 `DbSet<SyncState> SyncStates`。
- [ ] 2.4 生成 `SyncStates` 表的 EF Core 迁移。

## 3. 上行同步服务

- [ ] 3.1 创建 `MaterialClient.Common/Services/UploadSyncService.cs`，包含 `IUploadSyncService` 接口和 `UploadSyncService` 实现。注册为 `ISingletonDependency`，使用 `[AutoConstructor]`。注入：`IRepository<Material, int>`、`IRepository<Provider, int>`、`IRepository<SyncState, int>`、`IMaterialPlatformApi`、`ILogger<UploadSyncService>`。
- [ ] 3.2 实现 `UploadPendingMaterialsAsync`：查询待同步物料的 SyncState，从 Material 实体构建 `UpsertMaterialGoodDto`（映射所有字段包括 Units、Action="Create"/"Update"），按每批 100 条分块，调用批量端点，处理结果（applied/conflict/invalid）。
- [ ] 3.3 实现 `UploadPendingProvidersAsync`：查询待同步供应商的 SyncState，从 Provider 实体构建 `UpsertMaterialProviderDto`，按每批 100 条分块，调用批量端点，处理结果。
- [ ] 3.4 实现冲突处理：当 `status = "conflict"` 时，将服务端数据应用到本地实体（服务端优先），将 SyncState 更新为 Applied。
- [ ] 3.5 实现 `UploadAllPendingAsync`：先运行物料上传，再运行供应商上传，返回包含计数的 `UploadSyncSummary` record。
- [ ] 3.6 实现自动清理：在每个上传周期开始时删除超过 30 天的 Applied SyncState 条目。
- [ ] 3.7 创建 `MaterialClient.Common/Events/MaterialSyncedMessage.cs` 和 `ProviderSyncedMessage.cs` MessageBus 消息类。上传成功后发布。

## 4. PollingBackgroundService 集成

- [ ] 4.1 在 `PollingBackgroundService.DoWorkAsync` 中添加上行同步步骤：在下载同步（SyncProvider）之后、PushWaybill 之前调用 `IUploadSyncService.UploadAllPendingAsync`。使用 try-catch 包裹以防止阻塞后续步骤。

## 5. 数据管理 UI

- [ ] 5.1 创建 `MaterialClient/ViewModels/DataManagementViewModel.cs` ReactiveUI ViewModel。实现：按 EntityType 分组加载 SyncState 到 `MaterialSyncStates`/`ProviderSyncStates` 可观察集合，`SyncAllCommand`、`RefreshCommand`。通过 MessageBus 订阅 `MaterialSyncedMessage`/`ProviderSyncedMessage` 实现实时更新。
- [ ] 5.2 创建 `MaterialClient/Views/DataManagementWindow.axaml`，包含：上次同步时间戳显示、刷新/全部同步按钮、两个 DataGrid（物料和供应商），列包括（状态、名称、编码/统一社会信用代码、版本、最后更新时间），区域标题显示待同步/冲突计数。
- [ ] 5.3 创建 `MaterialClient/Views/DataManagementWindow.axaml.cs` code-behind，在 `OnClosed` 中通过 `CompositeDisposable` 清理 MessageBus 订阅。
- [ ] 5.4 设置同步状态指示器样式：待同步（黄色/琥珀色）、已同步（绿色）、冲突（红色），版本显示格式（已同步为"localVersion → serverVersion"，冲突为"localVersion vs serverVersion"）。

## 6. 测试

- [ ] 6.1 添加 `UploadSyncService` 单元测试：测试批量分块（250 条 → 3 批）、幂等键使用、版本冲突处理（服务端优先）、瞬态失败重试行为、空待同步列表提前返回。
- [ ] 6.2 添加 SyncState 状态转换单元测试：Pending→Applied、Pending→Conflict、Conflict→Applied、瞬态失败保持 Pending。
- [ ] 6.3 添加自动清理单元测试：验证超过 30 天的 Applied 条目被删除，近期条目保留。
