## ADDED Requirements

### Requirement: Edit solid waste waybill in attended weighing
系统在有人值守固废称重界面中，必须支持基于当前选中运单，通过受控方式更新与固废模式相关的运单状态 `OrderType`（例如在首磅与完成状态之间切换），而不直接修改重量字段。

#### Scenario: Show edit button for editable solid waste waybill
- **WHEN** 当前称重模式为固废模式，且存在已完成首次称重并允许修改的固废运单被选中
- **THEN** 界面上应在“打印”按钮左侧显示“修改”按钮，并且按钮处于可点击状态

#### Scenario: Hide or disable edit button when not applicable
- **WHEN** 当前不存在可编辑的固废运单（例如未选中运单、运单状态不允许修改或用户无权限）
- **THEN** “修改”按钮必须隐藏或处于不可用状态，且不会触发任何修改逻辑

#### Scenario: Invoke service to change order type
- **WHEN** 用户在固废称重界面点击“修改”按钮
- **THEN** 系统必须通过 `IWeighingMatchingService` 的相关接口，基于当前匹配到的运单，调用领域方法仅更新该运单的 `OrderType`（例如从 FirstWeight 切换为 Completed 或反之），并在修改成功后刷新界面显示

#### Scenario: Handle validation and domain rule failures
- **WHEN** 用户点击“修改”按钮且调用领域方法 `OrderType` 时违反领域规则（例如不允许对当前状态的运单修改 `FirstWeight`）
- **THEN** 系统必须阻止变更落地，并向用户呈现明确的错误信息或提示，而不会产生部分更新或不一致数据
