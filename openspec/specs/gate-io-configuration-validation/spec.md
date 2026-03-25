# 道闸 IO 配置验证

## Purpose

定义道闸 IO 配置的验证规则和流程，确保进出口成对配置、每方向单设备、以及必填字段的完整性检查。

## Requirements

### Requirement: 进出口成对配置验证
系统 MUST 在道闸 IO 功能启动前验证进出口道闸配置是否成对存在。

#### Scenario: 进出口都配置时验证通过
- **WHEN** 配置中存在进口道闸（`Direction = In` 且 `EnableGateIo = true`）且存在出口道闸（`Direction = Out` 且 `EnableGateIo = true`）
- **THEN** 系统 MUST 认为配置有效
- **AND** MUST 允许道闸 IO 功能启动

#### Scenario: 仅配置进口时验证失败
- **WHEN** 配置中仅存在进口道闸（`Direction = In` 且 `EnableGateIo = true`）但不存在出口道闸
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："进出口道闸必须成对配置"

#### Scenario: 仅配置出口时验证失败
- **WHEN** 配置中仅存在出口道闸（`Direction = Out` 且 `EnableGateIo = true`）但不存在进口道闸
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："进出口道闸必须成对配置"

#### Scenario: 都未配置时验证通过
- **WHEN** 配置中不存在任何启用了道闸 IO 的 LPR 设备
- **THEN** 系统 MUST 认为配置有效（功能未启用）
- **AND** MUST 跳过道闸 IO 服务初始化

### Requirement: 每方向单设备验证
系统 MUST 验证进出口方向各自最多配置一个道闸 IO 设备。

#### Scenario: 单个进口配置验证通过
- **WHEN** 配置中进口方向（`Direction = In`）仅有一个启用了道闸 IO 的 LPR 设备
- **THEN** 系统 MUST 认为该方向配置有效

#### Scenario: 单个出口配置验证通过
- **WHEN** 配置中出口方向（`Direction = Out`）仅有一个启用了道闸 IO 的 LPR 设备
- **THEN** 系统 MUST 认为该方向配置有效

#### Scenario: 多个进口配置验证失败
- **WHEN** 配置中进口方向有两个或更多启用了道闸 IO 的 LPR 设备
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："进口道闸只能配置一个"

#### Scenario: 多个出口配置验证失败
- **WHEN** 配置中出口方向有两个或更多启用了道闸 IO 的 LPR 设备
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："出口道闸只能配置一个"

### Requirement: IoChannel 必填字段验证
系统 MUST 验证所有启用了道闸 IO 的 LPR 设备都必须配置有效的 `IoChannel` 值。

#### Scenario: IoChannel 有值时验证通过
- **WHEN** LPR 设备启用了道闸 IO（`EnableGateIo = true`）且 `IoChannel` 字段为非空字符串
- **THEN** 系统 MUST 认为该设备配置有效

#### Scenario: 进口 IoChannel 为空时验证失败
- **WHEN** 进口 LPR 设备启用了道闸 IO 但 `IoChannel` 字段为 `null` 或空字符串
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："进口道闸的 IoChannel 不能为空"

#### Scenario: 出口 IoChannel 为空时验证失败
- **WHEN** 出口 LPR 设备启用了道闸 IO 但 `IoChannel` 字段为 `null` 或空字符串
- **THEN** 系统 MUST 认为配置无效
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："出口道闸的 IoChannel 不能为空"

#### Scenario: 未启用道闸 IO 时不验证 IoChannel
- **WHEN** LPR 设备未启用道闸 IO（`EnableGateIo = false`）
- **THEN** 系统 MUST 不验证 `IoChannel` 字段（允许为空）

### Requirement: 设备类型能力验证
系统 MUST 验证启用了道闸 IO 的 LPR 设备类型是否支持 IO 控制功能。

#### Scenario: Vzvision 设备验证通过
- **WHEN** LPR 设备类型为 `Vzvision` 且启用了道闸 IO
- **THEN** 系统 MUST 认为该设备支持 IO 控制
- **AND** MUST 允许配置验证通过

#### Scenario: 不支持的设备类型验证失败
- **WHEN** LPR 设备类型不是 `Vzvision` 且启用了道闸 IO
- **THEN** 系统 MUST 认为该设备不支持 IO 控制
- **AND** MUST 返回验证失败结果
- **AND** MUST 在日志中记录错误消息："设备类型 {DeviceType} 暂不支持道闸 IO 功能"

#### Scenario: 未启用道闸 IO 时不验证设备类型
- **WHEN** LPR 设备类型不支持 IO 控制但未启用道闸 IO（`EnableGateIo = false`）
- **THEN** 系统 MUST 不验证设备类型能力

### Requirement: 应用启动时验证
系统 MUST 在应用启动时自动执行配置验证。

#### Scenario: 应用启动时自动验证
- **WHEN** 应用初始化（`App.OnInitialized()` 或 `ConfigureServices()`）
- **THEN** 系统 MUST 自动调用配置验证器
- **AND** MUST 传入当前所有 LPR 设备配置

#### Scenario: 验证成功时启动道闸 IO 服务
- **WHEN** 配置验证返回成功结果
- **THEN** 系统 MUST 初始化并启动 `GateIOStateService`
- **AND** MUST 在日志中记录信息消息："道闸 IO 配置验证通过，服务已启动"

#### Scenario: 验证失败时跳过道闸 IO 服务
- **WHEN** 配置验证返回失败结果
- **THEN** 系统 MUST 不初始化 `GateIOStateService`
- **AND** MUST 在日志中记录错误消息："道闸 IO 配置验证失败，服务未启动"
- **AND** MUST 在状态栏显示错误提示

#### Scenario: 验证失败不影响应用启动
- **WHEN** 配置验证失败
- **THEN** 系统 MUST 继续启动应用的其他功能
- **AND** MUST 仅禁用道闸 IO 相关功能

### Requirement: 验证结果通知机制
系统 MUST 提供配置验证结果的详细通知机制。

#### Scenario: 返回结构化验证结果
- **WHEN** 配置验证完成
- **THEN** 系统 MUST 返回 `GateIOConfigurationValidationResult` 对象
- **AND** 该对象 MUST 包含 `IsValid`（布尔值）和 `Errors`（错误消息列表）字段

#### Scenario: 收集所有验证错误
- **WHEN** 配置存在多个验证错误
- **THEN** 系统 MUST 收集所有错误消息
- **AND** MUST 在 `Errors` 列表中返回所有错误（非首个错误即停止）

#### Scenario: 验证结果包含错误详情
- **WHEN** 配置验证失败
- **THEN** 系统 MUST 在错误消息中明确指出失败原因（如缺少出口配置、IoChannel 为空）
- **AND** MUST 指出受影响的 LPR 设备名称

### Requirement: 配置验证可重入性
系统 MUST 支持在运行时重新执行配置验证。

#### Scenario: 配置变更后重新验证
- **WHEN** 用户修改 LPR 配置并保存（`SettingsSavedMessage` 事件）
- **THEN** 系统 MUST 重新执行配置验证
- **AND** MUST 根据验证结果启动或停止 `GateIOStateService`

#### Scenario: 从禁用到启用时验证
- **WHEN** 用户将某个 LPR 设备的 `EnableGateIo` 从 `false` 改为 `true` 并保存
- **THEN** 系统 MUST 重新验证配置
- **AND** 如果验证通过，MUST 启动道闸 IO 服务

#### Scenario: 从启用到禁用时验证
- **WHEN** 用户将某个 LPR 设备的 `EnableGateIo` 从 `true` 改为 `false` 并保存
- **THEN** 系统 MUST 重新验证配置
- **AND** MUST 根据验证结果更新服务状态

### Requirement: 配置验证性能要求
系统 MUST 在合理时间内完成配置验证。

#### Scenario: 验证响应时间
- **WHEN** 配置验证执行
- **THEN** 系统 MUST 在 100ms 内完成验证（假设 LPR 设备数量 < 10）
- **AND** MUST 不阻塞应用启动主线程

#### Scenario: 大量配置时的性能
- **WHEN** 配置中存在大量 LPR 设备（> 100）
- **THEN** 系统 MUST 在 1 秒内完成验证
- **AND** MUST 使用异步方法避免阻塞

### Requirement: 配置验证日志记录
系统 MUST 详细记录配置验证的过程和结果。

#### Scenario: 记录验证开始
- **WHEN** 配置验证开始
- **THEN** 系统 MUST 记录信息日志："开始验证道闸 IO 配置"

#### Scenario: 记录验证成功
- **WHEN** 配置验证成功
- **THEN** 系统 MUST 记录信息日志："道闸 IO 配置验证通过"
- **AND** MUST 记录验证的设备数量和详情

#### Scenario: 记录验证失败
- **WHEN** 配置验证失败
- **THEN** 系统 MUST 记录错误日志："道闸 IO 配置验证失败"
- **AND** MUST 记录所有验证错误消息

#### Scenario: 记录验证跳过
- **WHEN** 未有任何 LPR 设备启用道闸 IO
- **THEN** 系统 MUST 记录信息日志："道闸 IO 功能未启用，跳过验证"
