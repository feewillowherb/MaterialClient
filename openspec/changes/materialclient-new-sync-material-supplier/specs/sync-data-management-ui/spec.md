## REMOVED Requirements

**[反馈 2026-04-16]**：用户不需要关心数据同步状态，同步应在后台自动完成。

以下规范已从设计中移除：
- `DataManagementWindow` - 数据管理对话框
- `DataManagementViewModel` - 数据管理 ViewModel
- 所有与同步状态显示相关的 UI 要求

### 影响分析

**移除的需求：**
- 数据管理 ViewModel
- 数据管理窗口布局
- 同步状态显示格式
- MessageBus 订阅实现实时更新（UI 部分）

**保留的需求：**
- MessageBus 消息（`MaterialSyncedMessage`、`ProviderSyncedMessage`）仍保留，用于 ViewModel 刷新缓存数据
- 与 `PollingBackgroundService` 集成保留，同步在后台自动执行

**替代方案：**
- 同步完全在后台通过 `PollingBackgroundService` 自动执行
- 冲突解决采用"服务端优先"策略，无需用户干预
- 日志记录用于审计和故障排除

### 已移至其他规范的场景

`与 PollingBackgroundService 集成` 需求已移至 `upload-sync` 规范中。
