## REMOVED Requirements

### Requirement: 数据管理 UI 同步状态展示

此需求已移除。用户不需要关心数据同步状态，同步必须在后台自动完成。

#### Scenario: 移除同步状态管理窗口

- **WHEN** 系统执行本次变更
- **THEN** `DataManagementWindow` 与 `DataManagementViewModel` 的同步状态展示需求必须被移除
- **AND** 同步状态显示格式要求必须被移除

#### Scenario: 由后台自动同步替代 UI 管理

- **WHEN** 不再提供数据管理 UI
- **THEN** 系统必须通过 `PollingBackgroundService` 自动执行同步
- **AND** 相关集成要求必须在 `upload-sync` 规范中定义
