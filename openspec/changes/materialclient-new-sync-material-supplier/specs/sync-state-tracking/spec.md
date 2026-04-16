## ADDED Requirements

### Requirement: SyncState 实体结构

系统须在 SQLite 中维护 `SyncState` 实体，包含以下属性：
- `Id`（int，主键，自增）
- `EntityType`（enum：`Material` = 0, `Provider` = 1）
- `EntityId`（int，外键关联 Material 或 Provider）
- `LocalVersion`（long，本地数据库中实体的当前版本）
- `ServerVersion`（long?，尚未同步时为 null）
- `Status`（enum：`Pending` = 0, `Applied` = 1, `Conflict` = 2）
- `ClientRequestId`（Guid，上传幂等键）
- `LastAttemptAt`（DateTime?，上次上传尝试时间戳）
- `RetryCount`（int，上传失败次数）
- `CreatedAt`（DateTime，创建时间戳）
- `UpdatedAt`（DateTime，最后更新时间戳）

#### Scenario: 实体表名

- **WHEN** EF Core 创建 SyncState 表
- **THEN** 表名须为 `SyncStates`

#### Scenario: 实体标识唯一约束

- **WHEN** 插入 `SyncState` 条目
- **THEN** `EntityType` 和 `EntityId` 的组合须唯一
- **AND** 插入重复项须抛出约束违反异常

### Requirement: 实体变更时创建 SyncState

当本地 Material 或 Provider 实体被创建或更新（通过检测到本地修改的下载同步）时，系统须创建状态为 `Pending` 的 `SyncState` 条目。

#### Scenario: 从平台下载新物料

- **WHEN** `SyncMaterialService.SyncMaterialAsync` 下载新物料
- **THEN** 系统不得创建 `SyncState` 条目（仅下载，无需上传）

#### Scenario: 为符合上传条件的实体创建 SyncState 条目

- **WHEN** 实体被标记为需要上传同步
- **THEN** 系统须创建 `SyncState` 条目，`Status = Pending`，生成新的 `ClientRequestId` GUID，`LocalVersion` 设为实体的当前版本

#### Scenario: 更新已有的 SyncState 条目

- **WHEN** 实体已有 `SyncState` 条目且被再次修改
- **THEN** 系统须将已有条目的 `Status` 更新为 `Pending`，递增 `LocalVersion`，并将 `RetryCount` 重置为 0

### Requirement: SyncState 状态转换

系统须强制执行以下 `SyncState.Status` 状态转换：

```
Pending ──上传成功──► Applied
Pending ──版本冲突──► Conflict
Pending ──瞬态失败──► Pending（递增 RetryCount）
Conflict ──服务端数据已应用──► Applied
Applied ──实体被修改──► Pending
```

#### Scenario: 上传成功转换为 Applied

- **WHEN** 上传返回 `status = "applied"`
- **THEN** `SyncState.Status` 须设为 `Applied`
- **AND** `ServerVersion` 须设为返回的版本

#### Scenario: 冲突转换为 Conflict

- **WHEN** 上传返回 `status = "conflict"`
- **THEN** `SyncState.Status` 须设为 `Conflict`

#### Scenario: 冲突解决后转换为 Applied

- **WHEN** 冲突解决机制将服务端数据应用到本地实体
- **THEN** `SyncState.Status` 须设为 `Applied`

#### Scenario: 瞬态失败保持 Pending

- **WHEN** 上传在所有重试后因瞬态错误失败
- **THEN** `SyncState.Status` 须保持 `Pending`
- **AND** `RetryCount` 须递增
- **AND** `LastAttemptAt` 须更新为当前时间

### Requirement: 自动清理过期的 Applied 条目

系统须在每次上传同步周期中删除 `Status = Applied` 且超过 30 天的 `SyncState` 条目。

#### Scenario: 同步期间执行清理

- **WHEN** 调用 `UploadAllPendingAsync`
- **THEN** 系统须删除所有 `Status = Applied` 且 `UpdatedAt` 超过 30 天的 `SyncState` 条目
- **AND** 此清理须在处理待同步项之前执行

#### Scenario: 无过期条目可清理

- **WHEN** 没有 Applied 条目超过 30 天
- **THEN** 清理步骤须正常完成，不产生错误

### Requirement: SyncState 的 EF Core 配置

系统须在专用的 `SyncStateConfiguration` 类中使用 EF Core Fluent API 配置 `SyncState` 实体。

#### Scenario: 必填字段配置

- **WHEN** 构建 EF Core 模型
- **THEN** `EntityType`、`EntityId`、`LocalVersion`、`Status`、`ClientRequestId`、`CreatedAt`、`UpdatedAt` 须配置为必填

#### Scenario: 唯一索引配置

- **WHEN** 构建 EF Core 模型
- **THEN** 须在 `(EntityType, EntityId)` 上创建唯一索引

#### Scenario: DbSet 注册

- **WHEN** 配置 `MaterialClientDbContext`
- **THEN** 须包含 `DbSet<SyncState> SyncStates`
