## ADDED Requirements

### Requirement: 基于会话的入口侧锁定
系统 MUST 在首次 LRP 识别时锁定入口侧，后续识别事件不得改变入口侧。

#### Scenario: 首次识别锁定入口侧
- **WHEN** 会话未激活且收到 LRP 识别事件且设备 `Direction = A`
- **THEN** 系统 MUST 设置 `EntrySide = A` 并锁定
- **AND** 出口侧 MUST 为 `B`（`A ↔ B` 互为对侧）
- **AND** 系统 MUST 打开 A 侧道闸

#### Scenario: B 侧首次识别锁定入口侧
- **WHEN** 会话未激活且收到 LRP 识别事件且设备 `Direction = B`
- **THEN** 系统 MUST 设置 `EntrySide = B` 并锁定
- **AND** 出口侧 MUST 为 `A`
- **AND** 系统 MUST 打开 B 侧道闸

#### Scenario: 会话期间拒绝改变入口侧
- **WHEN** 会话已激活且收到来自不同侧的 LRP 识别事件（例如入口侧为 A，收到 B 侧识别）
- **THEN** 系统 MUST 拒绝改变 `EntrySide`
- **AND** 系统 MUST 拒绝触发开闸
- **AND** 系统 MUST 记录日志："会话已激活，拒绝改变入口侧: CurrentEntrySide={CurrentEntrySide}, TriggerSide={TriggerSide}"

### Requirement: 基于入口侧的出口开闸逻辑
系统 MUST 根据入口侧计算出口侧，仅在 `WaitingForDeparture` 状态下打开出口道闸一次。

#### Scenario: 入口侧为 A 时打开出口 B
- **WHEN** 会话入口侧为 `A`（`EntrySide = A`）且状态转换为 `WaitingForDeparture` 且 `ExitOpened = false`
- **THEN** 系统 MUST 计算出口侧为 `B`
- **AND** 系统 MUST 查找 `Direction = B` 且 `EnableGateIo = true` 的 LPR 配置
- **AND** 系统 MUST 调用 SDK 打开 B 侧道闸
- **AND** 系统 MUST 设置 `ExitOpened = true`

#### Scenario: 入口侧为 B 时打开出口 A
- **WHEN** 会话入口侧为 `B`（`EntrySide = B`）且状态转换为 `WaitingForDeparture` 且 `ExitOpened = false`
- **THEN** 系统 MUST 计算出口侧为 `A`
- **AND** 系统 MUST 查找 `Direction = A` 且 `EnableGateIo = true` 的 LPR 配置
- **AND** 系统 MUST 调用 SDK 打开 A 侧道闸
- **AND** 系统 MUST 设置 `ExitOpened = true`

#### Scenario: 出口已开闸时跳过重复触发
- **WHEN** 状态转换为 `WaitingForDeparture` 且 `ExitOpened = true`
- **THEN** 系统 MUST 跳过出口开闸操作
- **AND** 系统 MUST 记录调试日志："出口道闸已开: ExitSide={ExitSide}，跳过重复触发"

### Requirement: 出口侧配置有效性验证
系统 MUST 在打开出口道闸前验证出口侧配置的有效性。

#### Scenario: 出口侧未启用道闸功能
- **WHEN** 状态转换为 `WaitingForDeparture` 且出口侧配置的 `EnableGateIo = false`
- **THEN** 系统 MUST 记录警告日志："出口侧未启用道闸功能: ExitSide={ExitSide}"
- **AND** 系统 MUST 跳过出口开闸操作
- **AND** 系统 MUST 设置 `ExitOpened = true`（标记为已处理，避免重复日志）

#### Scenario: 出口侧 IoChannel 配置无效
- **WHEN** 出口侧配置的 `IoChannel` 无法解析为 `uint`
- **THEN** 系统 MUST 记录警告日志："出口侧 IoChannel 配置无效: ExitSide={ExitSide}, IoChannel={IoChannel}"
- **AND** 系统 MUST 跳过出口开闸操作
- **AND** 系统 MUST 设置 `ExitOpened = true`

#### Scenario: 出口侧配置不存在
- **WHEN** 无法找到出口侧的 LPR 配置（`Direction == exitSide` 且 `EnableGateIo == true`）
- **THEN** 系统 MUST 记录警告日志："未找到出口侧配置: ExitSide={ExitSide}"
- **AND** 系统 MUST 跳过出口开闸操作
- **AND** 系统 MUST 设置 `ExitOpened = true`

### Requirement: Entry/Exit 与 Direction A/B 解耦
系统 MUST 将 Entry/Exit（入口/出口）作为运行时会话角色，与配置中的 Direction A/B 解耦。

#### Scenario: A 侧可作为入口或出口
- **WHEN** 首次识别来自 A 侧 LPR
- **THEN** 系统 MUST 将 `EntrySide` 设为 `A`（A 为入口角色）
- **AND** 系统 MUST 将出口侧设为 `B`（B 为出口角色）

#### Scenario: B 侧可作为入口或出口
- **WHEN** 首次识别来自 B 侧 LPR
- **THEN** 系统 MUST 将 `EntrySide` 设为 `B`（B 为入口角色）
- **AND** 系统 MUST 将出口侧设为 `A`（A 为出口角色）

#### Scenario: 同一物理侧在不同会话中角色不同
- **WHEN** 上一次会话中 A 侧为入口，当前会话首次识别来自 B 侧
- **THEN** 系统 MUST 将 `EntrySide` 设为 `B`（B 为入口角色）
- **AND** 系统 MUST 将出口侧设为 `A`（A 在此会话中为出口角色）
