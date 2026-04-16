## Why

MaterialClient 目前仅支持**下载方向同步**（从平台拉取物料和供应商）。后端已有完整的**上行同步 API**（`SyncController`），支持幂等性、乐观锁和变更日志追踪，但客户端无法将本地物料/供应商变更推送到服务端。这限制了现场操作人员在本地创建或编辑物料和供应商后同步回中央平台的业务场景。

## What Changes

- 为新的 `Sync/*` 端点添加 Refit API 客户端方法（`UpsertMaterialGood`、`UpsertMaterialProvider`、批量变体及 `Changes`）
- 在 MaterialClient.Common 中创建对应的 DTO（`UpsertMaterialGoodDto`、`UpsertMaterialProviderDto`、`UpsertBatchRequestDto`、`UpsertResultDto`、`SyncChangeItemDto`、`SyncChangesQueryDto`）
- 实现上行同步服务（`IUploadSyncService`），支持将本地物料/供应商变更推送到服务端，包含重试、幂等性和冲突处理
- 通过新的 `SyncState` 实体按实体追踪同步状态（本地版本 vs 服务端版本、待同步状态）
- 将上行同步集成到 `PollingBackgroundService` 中，与现有下载同步并列运行
- 新增"数据管理"对话框窗口，用于查看同步状态和手动触发同步操作
- 同步成功后通过 `MessageBus` 发布 `MaterialSyncedMessage` / `ProviderSyncedMessage`，以便 ViewModel 刷新缓存数据

## Capabilities

### New Capabilities

- `upload-sync`：物料和供应商从客户端到服务端的上行同步，包括单条/批量 upsert、幂等键管理、乐观并发冲突检测和重试逻辑
- `sync-state-tracking`：按实体本地追踪同步状态（待同步、已同步、冲突），实现客户端与服务端之间的版本对齐
- `sync-data-management-ui`：数据管理对话框，用于查看同步状态、手动触发同步和解决冲突

### Modified Capabilities

_（无现有 spec 层面的需求变更。现有下载同步、推荐缓存和轮询行为保持不变。）_

## Impact

### Code Changes

| File Path | Change Type | Reason | Scope |
|-----------|-------------|--------|-------|
| `MaterialClient.Common/Api/IMaterialPlatformApi.cs` | Modify | 添加 Sync 端点的 Refit 方法 | API 层 |
| `MaterialClient.Common/Api/Dtos/Sync*.cs` | Create | 同步 DTO（Upsert、Result、ChangeLog） | DTO 层 |
| `MaterialClient.Common/Services/UploadSyncService.cs` | Create | 上行同步服务实现 | Service 层 |
| `MaterialClient.Common/Entities/SyncState.cs` | Create | 按实体追踪同步状态的实体 | Domain 层 |
| `MaterialClient.Common/Events/MaterialSyncedMessage.cs` | Create | 物料同步事件的 MessageBus 消息 | Events |
| `MaterialClient.Common/Events/ProviderSyncedMessage.cs` | Create | 供应商同步事件的 MessageBus 消息 | Events |
| `MaterialClient/Backgrounds/PollingBackgroundService.cs` | Modify | 在轮询周期中添加上行同步步骤 | Background workers |
| `MaterialClient.EFCore/MaterialClientDbContext.cs` | Modify | 添加 `DbSet<SyncState>` 和实体配置 | Data access |
| `MaterialClient/EFCore/EntityConfigurations/SyncStateConfiguration.cs` | Create | SyncState 的 EF Core 实体配置 | Data access |
| `MaterialClient/Views/DataManagementWindow.axaml` | Create | 数据管理对话框 UI | UI |
| `MaterialClient/Views/DataManagementWindow.axaml.cs` | Create | 数据管理对话框 code-behind | UI |
| `MaterialClient/ViewModels/DataManagementViewModel.cs` | Create | 数据管理 ViewModel | ViewModel |

### External Dependencies

- FdSoft.Material：同步 API 端点已部署（`SyncController`），无需后端变更

### Systems

- 现有下载同步流程不受影响
- RecommendationCache 继续正常工作；同步事件通过 MessageBus 触发缓存失效
- 需要为新的 `SyncState` 表进行数据库迁移

## User Interaction Flow

```mermaid
sequenceDiagram
    participant U as 现场操作员
    participant UI as 数据管理对话框
    participant VM as DataManagementViewModel
    participant SS as UploadSyncService
    participant API as FdSoft.Material /Sync API
    participant DB as 本地 SQLite

    U->>UI: 打开数据管理
    UI->>VM: 初始化
    VM->>DB: 加载同步状态
    DB-->>VM: 返回待同步/冲突项
    UI-->>U: 显示同步状态列表

    U->>UI: 点击"全部同步"
    UI->>VM: SyncAll 命令
    VM->>SS: UploadPendingChangesAsync()
    SS->>DB: 查询待同步 SyncStates
    DB-->>SS: 返回待同步项
    SS->>API: POST /Sync/UpsertMaterialGoodsBatch
    API-->>SS: UpsertResultDto[]（已应用/冲突）
    SS->>DB: 更新 SyncState（已应用/冲突）
    SS-->>VM: 同步结果
    VM->>VM: 通过 MessageBus 发布 MaterialSyncedMessage
    UI-->>U: 显示同步结果

    alt 检测到冲突
        SS-->>VM: 冲突项及 serverData
        VM-->>U: 显示冲突详情
        U->>VM: 选择：保留本地 / 使用服务端
        VM->>SS: ResolveConflictAsync()
        SS->>API: 使用解决后的数据重新 upsert
    end
```

## UI Prototype

```
┌──────────────────────────────────────────────────────────────────┐
│ 数据管理                                                    [X] │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  上次同步: 2026-04-16 14:30:00          [刷新] [全部同步]        │
│                                                                  │
│  ┌─ 物料 (12 待同步, 2 冲突) ─────────────────────────────────┐ │
│  │  状态    | 名称          | 编码     | 版本    | 更新时间   │ │
│  │  ────────┼──────────────┼──────────┼─────────┼────────── │ │
│  │  待同步  | Gravel A     | MAT-001  | 3 → --  | 14:28    │ │
│  │  待同步  | Sand B       | MAT-002  | 1 → --  | 14:25    │ │
│  │  冲突    | Cement C     | MAT-003  | 5 vs 6  | 14:20    │ │
│  │  已同步  | Steel D      | MAT-004  | 7 → 7   | 14:15    │ │
│  └──────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ 供应商 (3 待同步, 0 冲突) ────────────────────────────────┐ │
│  │  状态    | 名称          | 统一代码 | 版本    | 更新时间   │ │
│  │  ────────┼──────────────┼──────────┼─────────┼────────── │ │
│  │  待同步  | Supplier A   | 9111...  | 2 → --  | 14:10    │ │
│  │  已同步  | Supplier B   | 9222...  | 4 → 4   | 13:55    │ │
│  └──────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  [自动同步: 开启]  间隔: 10 分钟                                  │
└──────────────────────────────────────────────────────────────────┘
```
