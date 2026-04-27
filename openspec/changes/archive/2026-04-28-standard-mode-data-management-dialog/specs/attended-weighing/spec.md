## MODIFIED Requirements

### Requirement: 操作后的条目导航
系统应在执行操作（保存、完成、匹配、作废）后，提供一致的、指向 WeighingListItemDto 的导航。

#### Scenario: 完成操作后的导航
- **WHEN** 用户在 AttendedWeighingDetailView 中将运单完成（首磅 → 已完成）
- **THEN** 系统应：
  - 刷新列表数据以反映已完成状态
  - 按以下优先级选择下一个条目：
    1. 优先选择下一个未完成的 Waybill（ItemType=Waybill, OrderType≠Completed）
    2. 若无未完成 Waybill，选择下一个未完成的 WeighingRecord（ItemType=WeighingRecord, OrderType≠Completed）
    3. 仅当所有条目均已完成时，选择最新已完成项
  - 若选中的是未完成项，在 AttendedWeighingDetailView 中打开该条目供继续操作
  - 若选中的是已完成项（fallback），在 AttendedWeighingMainView 中显示
  - 若目标条目不在当前页，则导航到正确页码
  - 遵循标签页切换规则（尊重 IsShowAllRecords 标志）

#### Scenario: 完成操作后存在未完成 Waybill
- **WHEN** 用户完成一个运单
- **AND** 列表中存在其他 OrderType≠Completed 的 Waybill
- **THEN** 系统应选择第一个未完成的 Waybill
- **AND** 在 AttendedWeighingDetailView 中打开该 Waybill

#### Scenario: 完成操作后无未完成 Waybill 但存在未完成 WeighingRecord
- **WHEN** 用户完成一个运单
- **AND** 列表中不存在未完成的 Waybill
- **AND** 列表中存在 OrderType≠Completed 的 WeighingRecord
- **THEN** 系统应选择第一个未完成的 WeighingRecord
- **AND** 在 AttendedWeighingDetailView 中打开该 WeighingRecord

#### Scenario: 完成操作后所有条目均已完成
- **WHEN** 用户完成一个运单
- **AND** 列表中不存在任何未完成的 Waybill 或 WeighingRecord
- **THEN** 系统应选择最新已完成项
- **AND** 在 AttendedWeighingMainView 中显示

#### Scenario: 保存操作后的导航
- **当** 用户在 AttendedWeighingDetailView 中保存称重记录或运单的修改
- **则** 系统应：
  - 刷新列表数据以反映保存后的变更
  - 在列表中保持已保存条目为选中状态
  - 保持在 AttendedWeighingDetailView（允许继续编辑）
  - 保持当前标签页（条目状态未变）
  - 若因排序导致条目移动，则导航到正确页码

#### Scenario: 匹配操作后的导航
- **当** 用户手动将一条称重记录与另一条记录匹配
- **则** 系统应：
  - 刷新列表数据以显示新生成的运单
  - 在列表中选中下一个未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一个未匹配条目
  - 若当前在"已完成"标签且未显示全部记录，则切换到"未匹配"标签
  - 导航到下一个条目所在页

#### Scenario: 作废操作后的导航
- **当** 用户作废（删除）一条称重记录
- **则** 系统应：
  - 刷新列表数据以移除已作废记录
  - 在列表中选中下一个未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一个未匹配条目
  - 保持当前标签页（条目被移除，未移动）
  - 导航到下一个条目所在页

## ADDED Requirements

### Requirement: 台账管理对话框按模式路由
系统 SHALL 根据当前 WeighingMode 决定"台账管理"按钮打开的对话框类型。

#### Scenario: 标准模式打开标准台账对话框
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击"台账管理"按钮
- **AND WHEN** 当前 `WeighingMode` 为 Standard
- **THEN** 系统 SHALL 创建 `StandardDataManagementDialogViewModel` 并打开 `StandardDataManagementDialogWindow`

#### Scenario: 固废模式打开固废台账对话框
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击"台账管理"按钮
- **AND WHEN** 当前 `WeighingMode` 为 SolidWaste
- **THEN** 系统 SHALL 创建 `DataManagementDialogViewModel` 并打开 `DataManagementDialogWindow`（保持现有行为不变）
