# Purpose

TBD

# Requirements

### Requirement: 废单按钮根据列表项状态控制可见性
"废单"按钮仅在列表项可废除时可见：WeighingRecord（始终可废除）和 FirstWeight 状态的运单。已完成运单（`OrderType == Completed`）不显示"废单"按钮。

#### Scenario: 已完成运单隐藏废单按钮
- **WHEN** 用户查看 `OrderType == Completed` 的运单详情
- **THEN** "废单"按钮不可见

#### Scenario: FirstWeight 运单显示废单按钮
- **WHEN** 用户查看 `OrderType == FirstWeight` 的运单详情
- **THEN** "废单"按钮可见

#### Scenario: WeighingRecord 显示废单按钮
- **WHEN** 用户查看 WeighingRecord 详情
- **THEN** "废单"按钮可见

### Requirement: 运单废除时显示范围选择对话框
用户在 FirstWeight 运单上点击"废单"时，系统 SHALL 显示范围选择对话框（Ursa.Avalonia OverlayDialog），提供三个选项：仅废除进场称重记录、仅废除出场称重记录、或全部废除。

#### Scenario: FirstWeight 运单显示范围选择对话框
- **WHEN** 用户在查看运单详情（`ItemType == WeighingListItemType.Waybill` 且 `OrderType == FirstWeight`）时点击"废单"按钮
- **THEN** 系统 SHALL 显示 OverlayDialog，包含三个可选选项：
  1. "进场称重记录"（Join Only）—— 废除进场称重记录
  2. "出场称重记录"（Out Only）—— 废除出场称重记录
  3. "全部废除"（Both）—— 废除两条记录和运单
- **AND** 对话框 SHALL 包含"取消"和"确认"按钮

#### Scenario: WeighingRecord 显示简单确认
- **WHEN** 用户在查看 WeighingRecord 详情（`ItemType == WeighingListItemType.WeighingRecord`）时点击"废单"按钮
- **THEN** 系统 SHALL 显示简单 MessageBox 确认（"确定要废除此单吗？"），不显示范围选择对话框
- **AND** 确认后，系统 SHALL 通过 `_weighingRecordRepository.DeleteAsync` 硬删除该 WeighingRecord

#### Scenario: 用户取消范围选择对话框
- **WHEN** 用户在范围选择对话框中点击"取消"
- **THEN** 系统 SHALL 关闭对话框，不执行任何废除操作

#### Scenario: 未选择任何选项时确认按钮禁用
- **WHEN** 范围选择对话框打开时，未选择任何废除范围
- **THEN** "确认"按钮 SHALL 处于禁用状态
- **AND** 当用户选择任一废除范围后，"确认"按钮 SHALL 变为可用

### Requirement: 部分废除时解除保留记录的匹配
用户选择部分废除（JoinOnly 或 OutOnly）时，系统 SHALL 软删除选中的 WeighingRecord，并清除保留记录上的匹配引用，使其重新进入未匹配池。

#### Scenario: 仅废除进场记录
- **WHEN** 用户选择"进场称重记录"（JoinOnly）并确认
- **THEN** 系统 SHALL：
  1. 软删除进场 WeighingRecord（`IsDeleted = true`）
  2. 对出场 WeighingRecord 调用 `Unmatch()`（清除 `MatchedId`、`WaybillId`、`MatchedType`）
  3. 将运单的 `OrderType` 设为 `Esc`，`AbortReason` 设为描述性字符串
- **AND** 保留的出场记录 SHALL 出现在未匹配列表中（`MatchedId == null`）

#### Scenario: 仅废除出场记录
- **WHEN** 用户选择"出场称重记录"（OutOnly）并确认
- **THEN** 系统 SHALL：
  1. 软删除出场 WeighingRecord（`IsDeleted = true`）
  2. 对进场 WeighingRecord 调用 `Unmatch()`（清除 `MatchedId`、`WaybillId`、`MatchedType`）
  3. 将运单的 `OrderType` 设为 `Esc`，`AbortReason` 设为描述性字符串
- **AND** 保留的进场记录 SHALL 出现在未匹配列表中（`MatchedId == null`）

#### Scenario: 全部废除
- **WHEN** 用户选择"全部废除"（Both）并确认
- **THEN** 系统 SHALL：
  1. 软删除进场和出场两条 WeighingRecords（`IsDeleted = true`）
  2. 将运单的 `OrderType` 设为 `Esc`，`AbortReason` 设为描述性字符串

### Requirement: 废除操作是事务性的
选择性废除操作 SHALL 在单个数据库事务（Unit of Work）中执行。如果任何步骤失败，所有变更 SHALL 回滚。

#### Scenario: 废除操作执行中途失败
- **WHEN** 系统正在执行选择性废除时发生数据库错误
- **THEN** 系统 SHALL 回滚所有变更（无记录被软删除、无记录被解除匹配、无运单被废除）
- **AND** 系统 SHALL 记录错误日志并向用户显示错误消息

### Requirement: 废除运单后通过现有轮询机制同步到平台
运单废除操作本地成功后，系统 SHALL 将运单标记为待同步（`IsPendingSync = true`），由现有轮询服务通过 `SynchronizationModifyOrderAsync` 推送到平台。

#### Scenario: 废除后标记待同步
- **WHEN** 运单废除操作成功完成（任何范围）
- **THEN** 系统 SHALL 在运单上调用 `SetPendingSync()` 设置 `IsPendingSync = true`
- **AND** 现有轮询服务的 `PushUpdatedWaybillsAsync` SHALL 在下一周期检测到该运单并调用 `SynchronizationModifyOrderAsync` 同步

### Requirement: WeighingRecord.Unmatch() 领域方法
系统 SHALL 在 `WeighingRecord` 上提供 `Unmatch()` 方法，将 `MatchedId`、`WaybillId` 和 `MatchedType` 清除为 null。

#### Scenario: Unmatch 清除所有匹配引用
- **WHEN** 对 `MatchedId = 42`、`WaybillId = 100`、`MatchedType = Join` 的 WeighingRecord 调用 `Unmatch()`
- **THEN** 该记录的 `MatchedId` SHALL 为 `null`，`WaybillId` SHALL 为 `null`，`MatchedType` SHALL 为 `null`

### Requirement: Waybill.AbortWaybill() 领域方法
系统 SHALL 在 `Waybill` 上提供 `AbortWaybill(string reason)` 方法，设置 `OrderType = Esc` 和 `AbortReason = reason`。

#### Scenario: Abort 设置已取消状态
- **WHEN** 对运单调用 `AbortWaybill("operator error")`
- **THEN** 该运单的 `OrderType` SHALL 为 `OrderTypeEnum.Esc`
- **AND** 该运单的 `AbortReason` SHALL 为 `"operator error"`

### Requirement: 废除成功后发送 DetailOperationCompletedMessage
废除操作成功后，系统 SHALL 通过 MessageBus 发送 `DetailOperationCompletedMessage`，`OperationType = Abolish`，与现有废除流程一致。

#### Scenario: 消息发送触发列表刷新
- **WHEN** 废除操作成功完成
- **THEN** 系统 SHALL 发送 `DetailOperationCompletedMessage`，包含 `itemId = waybillId`、`itemType = WeighingListItemType.Waybill`、`operationType = DetailOperationType.Abolish`
- **AND** `AttendedWeighingViewModel` SHALL 刷新列表并导航到下一个未匹配项
