## Why

本地 Material 和 Provider 记录是直接在 SQLite 数据库中创建的，缺少服务端 ID。下游实体（Waybill、WeighingRecord、WaybillMaterial）引用了这些本地 ID，导致无法与服务端对账。需要一个一次性同步服务，将现有本地数据推送到服务端并更新所有外键引用，使后续操作完全可追溯。

## What Changes

- 在 **Toolkit** 项目中添加 `IMaterialProviderSyncService` / `MaterialProviderSyncService`，暴露无参数的 `SyncAsync()` 入口。
- 在 `SyncAsync` 中实现四阶段流水线：
  1. **导出** — 读取所有本地 `Material` 和 `Provider` 行；构建 ID 映射表（本地 Id → 服务端 Id）。
  2. **推送** — 对每一行调用 `IMaterialPlatformApi.CreateMaterialByNameAsync` 和 `CreateProviderAsync`；记录返回的服务端 ID。
  3. **更新引用** — 重写 `Waybill`、`WeighingRecord`、`WaybillMaterial` 上的 `MaterialId` / `ProviderId`；用服务端返回的 `MaterialGoodListResultDto.ToEntity()` / `MaterialProviderListResultDto.ToEntity()` 替换整个 `Material` / `Provider` 实体。
  4. **清理关联表** — 清空 `MaterialType` 和 `MaterialUnit` 表所有数据（MaterialUnit 通过 `MaterialId` 引用 Material，同步后旧 ID 失效；MaterialType 为本地分类数据，需清除以便后续从服务端重新拉取）。
  5. **验证** — 断言无悬挂外键引用。
- 通过 ABP 约定（`ITransientDependency` + `[AutoConstructor]`）注册服务，与现有 Toolkit 服务保持一致。
- 不修改 Common 实体、DTO 或现有业务逻辑。

## Capabilities

### New Capabilities
- `material-provider-sync`：一次性将本地创建的 Material 和 Provider 记录同步到服务端，完整更新 Waybill、WeighingRecord、WaybillMaterial 的引用链。

### Modified Capabilities
<!-- 无现有 spec 级别行为变更。 -->

## Impact

- **MaterialClient.Toolkit** — 新增服务文件 `Services/MaterialProviderSyncService.cs`（接口 + 实现）。
- **MaterialClient.Common** — 仅只读消费（`IMaterialPlatformApi`、实体、DbContext）；不做修改。
- **受影响表** — `Materials`、`Providers`、`Waybills`、`WeighingRecords`、`WaybillMaterials`（同步期间写入）；`MaterialTypes`、`MaterialUnits`（全量清空）。
- **依赖** — `IMaterialPlatformApi`（Refit 客户端，已注册）、`MaterialClientDbContext`（EF Core）。
- **风险** — 同步会覆盖本地实体 ID；必须在数据库事务中运行且仅执行一次。建议同步前备份 / 设置防护标志。
