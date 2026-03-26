## MODIFIED Requirements

### Requirement: LPR 道闸 I/O 通用配置项
系统 MUST 提供面向 LPR 设备的道闸 I/O 通用配置能力，包括是否启用开关与 `IoChannel` 通道号，并将其持久化到现有 LPR 配置存储中。

**变更说明**：`Direction` 字段的枚举值从 `In/Out` 改为 `A/B`，用于表示物理侧别而非入口/出口角色。

#### Scenario: 在 Vzvision 配置中展示并编辑 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置 `LprDeviceType = Vzvision`
- **THEN** 系统 MUST 显示"是否启用道闸 I/O 功能"开关与 `IoChannel` 输入项，并允许编辑
- **AND** 系统 MUST 显示道闸侧别选项：`A` 或 `B`（取代原有的 `In/Out` 选项）

#### Scenario: 非 Vzvision 设备不暴露 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置非 Vzvision 设备类型
- **THEN** 系统 MUST 不显示或不允许编辑道闸 I/O 配置项

#### Scenario: 保存并加载 I/O 配置
- **WHEN** 用户保存并重新打开设置
- **THEN** 系统 MUST 正确序列化和反序列化 `EnableGateIo`、`IoChannel` 与 `Direction`，保持值不丢失
- **AND** 系统 MUST 将 `Direction` 值序列化为整数（`A=0`, `B=1`）

#### Scenario: 非 Vzvision 设备保留配置但当前不执行
- **WHEN** 非 Vzvision 设备存在 `EnableGateIo` 与 `IoChannel` 配置
- **THEN** 系统 MUST 保留并加载配置值，但在运行时按能力门控判定为当前不支持

#### Scenario: 枚举值从 In/Out 迁移到 A/B
- **WHEN** 系统从旧版本升级（配置中存在 `Direction.In/Out`）
- **THEN** 系统 MUST 将整数值 `0` 反序列化为 `Direction.A`，`1` 反序列化为 `Direction.B`
- **AND** 系统 MUST 在 UI 中显示 `A/B` 选项而非 `In/Out`

### Requirement: 识别后触发开闸信号
系统 MUST 通过 MessageBus 驱动的后置动作流程，在满足条件时调用 Vzvision SDK 向指定通道下发 `500ms` 自动复位开闸脉冲。

**变更说明**：增加会话状态门控和称重状态检查，仅在允许状态下触发开闸。

#### Scenario: 启用配置后识别触发开闸（新会话）
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = true`，且识别链路通过 MessageBus 收到有效车辆识别消息且称重状态为 `OffScale` 且道闸会话未激活
- **THEN** 系统 MUST 创建新会话并设置 `EntrySide =` 识别设备的 `Direction`（A 或 B）
- **AND** 系统 MUST 调用 `VzLPRClient_SetIOOutputAutoResp(handle, ioChannel, 500)`（500ms 自动复位）

#### Scenario: 会话期间禁止重复触发
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到 LRP 识别事件（无论来自 A 侧或 B 侧）
- **THEN** 系统 MUST 不调用 I/O 开闸接口
- **AND** 系统 MUST 记录日志："道闸会话已激活，拒绝 LRP 触发"

#### Scenario: 稳定/稳重阶段禁止 LRP 开闸
- **WHEN** 称重状态为 `WaitingForStability` 或 `WeightStabilized` 且收到 LRP 识别事件
- **THEN** 系统 MUST 不调用 I/O 开闸接口
- **AND** 系统 MUST 记录日志："称重状态 {Status}，禁止 LRP 开闸"

#### Scenario: 等待下磅阶段禁止入口 LRP 开闸
- **WHEN** 称重状态为 `WaitingForDeparture` 且收到 LRP 识别事件
- **THEN** 系统 MUST 不调用 I/O 开闸接口（出口开闸由状态转换自动触发）
- **AND** 系统 MUST 记录日志："称重状态 WaitingForDeparture，禁止 LRP 开闸（等待自动打开出口）"

#### Scenario: 未启用配置时不触发开闸
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = false`，且收到车辆识别事件
- **THEN** 系统 MUST 不调用 I/O 开闸接口

#### Scenario: 非 Vzvision 设备记录未支持日志
- **WHEN** 设备类型不是 Vzvision，且识别后进入道闸 I/O 后置动作评估
- **THEN** 系统 MUST 不调用 I/O 开闸接口，并 MUST 记录"当前设备类型未支持道闸 I/O 功能"的日志

#### Scenario: 配置校验失败时禁用道闸功能
- **WHEN** 道闸配置校验失败（A/B 不成对或缺少配置）
- **THEN** 系统 MUST 不调用 I/O 开闸接口
- **AND** 系统 MUST 记录日志："道闸配置校验失败，道闸功能已禁用"

### Requirement: I/O 控制职责分离
系统 MUST 将道闸 I/O 控制职责与车牌识别职责分离，识别服务不得直接承担硬件 I/O 下发编排责任。

#### Scenario: 识别服务与 I/O 服务解耦
- **WHEN** 发生识别事件处理流程
- **THEN** 系统 MUST 通过 MessageBus 发布/订阅与独立 I/O 控制服务执行开闸动作，而非在识别解析逻辑中直接调用 SDK 下发

#### Scenario: I/O 服务通过 MessageBus 订阅接收触发
- **WHEN** 识别服务发布车牌识别消息或专用 I/O 触发消息
- **THEN** I/O 控制服务 MUST 通过 MessageBus 订阅接收并执行业务门控判断

#### Scenario: I/O 服务订阅称重状态变化（新增）
- **WHEN** 称重状态机发布 `StatusChangedMessage`
- **THEN** I/O 控制服务 MUST 通过 MessageBus 订阅接收并执行会话状态同步逻辑

### Requirement: 等待下磅时自动打开出口道闸（新增）
系统 MUST 在称重状态转换为 `WaitingForDeparture` 时自动打开出口道闸（另一侧）。

#### Scenario: 状态转换触发出口开闸
- **WHEN** 称重状态转换为 `WaitingForDeparture` 且道闸会话已激活且出口未开闸（`ExitOpened = false`）
- **THEN** 系统 MUST 计算出口侧（`exitSide = EntrySide == A ? B : A`）
- **AND** 系统 MUST 查找出口侧 LPR 配置（`Direction == exitSide` 且 `EnableGateIo == true`）
- **AND** 系统 MUST 调用 `VzLPRClient_SetIOOutputAutoResp(handle, exitIoChannel, 500)`
- **AND** 系统 MUST 设置 `ExitOpened = true`

#### Scenario: 出口已开闸时跳过重复触发
- **WHEN** 称重状态转换为 `WaitingForDeparture` 且 `ExitOpened = true`
- **THEN** 系统 MUST 跳过出口开闸操作
- **AND** 系统 MUST 记录调试日志："出口道闸已开，跳过重复触发"

#### Scenario: 车辆离磅时清理会话状态（新增）
- **WHEN** 称重状态转换为 `OffScale`
- **THEN** 系统 MUST 清理道闸会话状态（`SessionActive = false`、`EntrySide = null`、`ExitOpened = false`）
- **AND** 系统 MUST 记录日志："称重状态 OffScale，清理道闸会话"
