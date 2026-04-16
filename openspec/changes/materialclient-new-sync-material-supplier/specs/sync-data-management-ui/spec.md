## ADDED Requirements

### Requirement: 数据管理 ViewModel

系统须提供 `DataManagementViewModel`，用于管理数据管理对话框的同步状态显示和用户交互。

#### Scenario: ViewModel 初始化时加载同步状态

- **WHEN** 创建 `DataManagementViewModel`
- **THEN** 须从数据库查询所有 `SyncState` 条目
- **AND** 按 `EntityType` 分组填充 `MaterialSyncStates` 和 `ProviderSyncStates` 可观察集合

#### Scenario: 刷新命令重新加载数据

- **WHEN** 用户触发 `RefreshCommand`
- **THEN** 系统须重新从数据库查询所有 `SyncState` 条目
- **AND** 更新可观察集合

#### Scenario: 全部同步命令触发上传

- **WHEN** 用户触发 `SyncAllCommand`
- **THEN** 系统须调用 `IUploadSyncService.UploadAllPendingAsync`
- **AND** 完成后刷新同步状态显示
- **AND** 显示已应用、冲突和失败项的摘要

#### Scenario: 同步过程中禁用全部同步命令

- **WHEN** 上行同步操作正在进行
- **THEN** `SyncAllCommand` 须被禁用
- **AND** 在操作完成或失败后重新启用

### Requirement: 数据管理窗口布局

系统须提供 `DataManagementWindow` 对话框窗口，显示物料和供应商的同步状态。

#### Scenario: 窗口显示物料同步状态

- **WHEN** 打开数据管理窗口
- **THEN** 须显示一个 DataGrid，列包括：状态、名称、编码/统一社会信用代码、版本、最后更新时间
- **AND** 行须按同步状态分组（待同步、冲突、已同步）

#### Scenario: 窗口显示供应商同步状态

- **WHEN** 打开数据管理窗口
- **THEN** 须为供应商显示单独的 DataGrid，列包括：状态、名称、统一社会信用代码、版本、最后更新时间

#### Scenario: 窗口显示最后同步时间戳

- **WHEN** 打开数据管理窗口
- **THEN** 须显示最后一次成功同步操作的时间戳

#### Scenario: 窗口标题显示待同步和冲突计数

- **WHEN** 打开数据管理窗口
- **THEN** 每个区域标题须显示待同步和冲突项的数量

### Requirement: 同步状态显示格式

系统须使用不同的视觉指示器显示同步状态。

#### Scenario: 待同步状态指示器

- **WHEN** `SyncState` 条目的 `Status = Pending`
- **THEN** 状态列须显示"待同步"并使用黄色/琥珀色指示器
- **AND** 版本列须显示"localVersion → --"

#### Scenario: 已同步状态指示器

- **WHEN** `SyncState` 条目的 `Status = Applied`
- **THEN** 状态列须显示"已同步"并使用绿色指示器
- **AND** 版本列须显示"localVersion → serverVersion"

#### Scenario: 冲突状态指示器

- **WHEN** `SyncState` 条目的 `Status = Conflict`
- **THEN** 状态列须显示"冲突"并使用红色指示器
- **AND** 版本列须显示"localVersion vs serverVersion"

### Requirement: MessageBus 订阅实现实时更新

`DataManagementViewModel` 须通过 MessageBus 订阅 `MaterialSyncedMessage` 和 `ProviderSyncedMessage`，以便在同步事件发生时更新显示。

#### Scenario: 收到物料同步消息

- **WHEN** 通过 MessageBus 收到 `MaterialSyncedMessage`
- **THEN** ViewModel 须刷新物料同步状态集合

#### Scenario: 收到供应商同步消息

- **WHEN** 通过 MessageBus 收到 `ProviderSyncedMessage`
- **THEN** ViewModel 须刷新供应商同步状态集合

#### Scenario: MessageBus 订阅生命周期

- **WHEN** `DataManagementViewModel` 被释放
- **THEN** 所有 MessageBus 订阅须通过 `CompositeDisposable` 释放

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
