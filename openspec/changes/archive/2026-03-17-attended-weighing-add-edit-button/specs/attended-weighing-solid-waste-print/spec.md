## MODIFIED Requirements

### Requirement: 已完成运单在 MainView 中显示
系统在有人值守称重中，已完成的运单应在 `AttendedWeighingMainView` 中以只读形式展示，并与固废运单的“修改/打印”操作保持一致的视图选择逻辑。

#### Scenario: 已完成固废运单在 MainView 中显示且允许打印
- **WHEN** 导航到的条目为固废运单（Waybill）
- **AND** 运单的 OrderType 为 Completed
- **THEN** 系统应显示 `AttendedWeighingMainView` 作为只读摘要视图
- **AND** 在该视图中显示“打印”按钮
- **AND** 若运单在之前通过“修改”按钮更新了 `FirstWeight`，则打印内容必须基于最新的 `FirstWeight` 与相关数据

#### Scenario: 可编辑固废条目在 DetailView 中显示且不提供打印按钮
- **WHEN** 导航到的条目为固废相关条目
- **AND** 条目不是已完成的运单（例如 OrderType = FirstWeight 或 Unmatch）
- **THEN** 系统应显示 `AttendedWeighingDetailView` 以允许编辑
- **AND** 在该视图中不应显示仅适用于已完成固废运单的“打印”按钮

#### Scenario: 修改后重新回到 MainView 并允许再次打印
- **WHEN** 用户在固废称重界面通过“修改”按钮更新了运单的 `FirstWeight` 等数据
- **AND** 修改完成后运单状态仍为 Completed
- **THEN** 系统应导航回 `AttendedWeighingMainView` 显示该运单
- **AND** 允许用户在该视图中再次使用“打印”按钮输出基于更新后数据的凭证
