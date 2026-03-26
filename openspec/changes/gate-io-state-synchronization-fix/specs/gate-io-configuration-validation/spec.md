## ADDED Requirements

### Requirement: 道闸 A/B 配置成对校验
系统 MUST 在启动时验证 A/B 道闸配置的成对性，确保恰好有一对 A/B 配置。

#### Scenario: 有效的一对 A/B 配置
- **WHEN** 系统启动时存在恰好两个 LPR 配置且 `EnableGateIo = true` 且一个 `Direction = A`，另一个 `Direction = B`
- **THEN** 系统 MUST 判定配置有效
- **AND** 系统 MUST 允许道闸功能正常启动
- **AND** 系统 MUST 记录日志："道闸配置校验通过: A=DeviceA, B=DeviceB"

#### Scenario: 缺少 A 侧配置（零对）
- **WHEN** 系统启动时不存在任何 `EnableGateIo = true` 且 `Direction = A` 的配置
- **THEN** 系统 MUST 判定配置无效
- **AND** 系统 MUST 记录警告日志："道闸配置校验失败: 缺少 A 侧配置，期望恰好一对 A/B，当前数量: A=0, B=1"
- **AND** 系统 MUST 禁用道闸功能（`_gateIoEnabled = false`）
- **AND** 系统 MUST 继续启动流程（不抛异常）

#### Scenario: 缺少 B 侧配置（零对）
- **WHEN** 系统启动时不存在任何 `EnableGateIo = true` 且 `Direction = B` 的配置
- **THEN** 系统 MUST 判定配置无效
- **AND** 系统 MUST 记录警告日志："道闸配置校验失败: 缺少 B 侧配置，期望恰好一对 A/B，当前数量: A=1, B=0"
- **AND** 系统 MUST 禁用道闸功能
- **AND** 系统 MUST 继续启动流程

#### Scenario: 多个 A 侧配置（多对）
- **WHEN** 系统启动时存在两个或更多 `EnableGateIo = true` 且 `Direction = A` 的配置
- **THEN** 系统 MUST 判定配置无效
- **AND** 系统 MUST 记录警告日志："道闸配置校验失败: 存在多个 A 侧配置，期望恰好一对 A/B，当前数量: A={Count}, B=1"
- **AND** 系统 MUST 列出所有冲突的设备名称："A 侧设备: {Device1}, {Device2}, ..."
- **AND** 系统 MUST 禁用道闸功能
- **AND** 系统 MUST 继续启动流程

#### Scenario: 多个 B 侧配置（多对）
- **WHEN** 系统启动时存在两个或更多 `EnableGateIo = true` 且 `Direction = B` 的配置
- **THEN** 系统 MUST 判定配置无效
- **AND** 系统 MUST 记录警告日志："道闸配置校验失败: 存在多个 B 侧配置，期望恰好一对 A/B，当前数量: A=1, B={Count}"
- **AND** 系统 MUST 列出所有冲突的设备名称："B 侧设备: {Device1}, {Device2}, ..."
- **AND** 系统 MUST 禁用道闸功能
- **AND** 系统 MUST 继续启动流程

#### Scenario: 无任何道闸配置
- **WHEN** 系统启动时不存在任何 `EnableGateIo = true` 的配置
- **THEN** 系统 MUST 判定配置无效
- **AND** 系统 MUST 记录信息日志："未检测到道闸配置，道闸功能已禁用"
- **AND** 系统 MUST 禁用道闸功能（或保持默认禁用状态）
- **AND** 系统 MUST 继续启动流程（无道闸模式）

### Requirement: 配置校验时机
系统 MUST 在服务启动时执行配置校验，并在配置保存后重新校验。

#### Scenario: 服务启动时校验
- **WHEN** `LprGateIoControlService.StartAsync()` 被调用
- **THEN** 系统 MUST 调用 `ValidateGateConfiguration()` 执行校验
- **AND** 系统 MUST 根据校验结果设置 `_gateIoEnabled` 标志
- **AND** 系统 MUST 在后续 LRP 触发逻辑中检查此标志

#### Scenario: 配置保存后重新校验
- **WHEN** 用户修改配置并保存（接收到 `SettingsSavedMessage`）
- **THEN** 系统 MUST 重新加载配置
- **AND** 系统 MUST 重新执行 `ValidateGateConfiguration()`
- **AND** 系统 MUST 更新 `_gateIoEnabled` 标志
- **AND** 系统 MUST 记录日志："配置已更新，重新校验结果: IsValid={IsValid}, Reason={Reason}"

#### Scenario: 配置校验失败时的降级模式
- **WHEN** 配置校验失败（`IsValid = false`）
- **THEN** 系统 MUST 设置 `_gateIoEnabled = false`
- **AND** 系统 MUST 在 `HandlePlateRecognizedAsync()` 开始时检查此标志
- **AND** 系统 MUST 在 `OnStatusChanged()` 开始时检查此标志
- **AND** 系统 MUST 当 `_gateIoEnabled = false` 时跳过所有道闸控制逻辑
- **AND** 系统 MUST 记录调试日志："道闸功能已禁用，跳过控制逻辑"

### Requirement: 配置校验错误信息可读性
系统 MUST 提供清晰可读的校验错误信息，帮助用户快速定位配置问题。

#### Scenario: 校验结果包含详细信息
- **WHEN** 配置校验失败
- **THEN** 系统 MUST 返回包含以下信息的校验结果：
  - `IsValid`: 布尔值，表示校验是否通过
  - `Reason`: 人类可读的失败原因描述
  - `CountA`: A 侧配置数量
  - `CountB`: B 侧配置数量
  - `DevicesA`: A 侧设备名称列表（逗号分隔）
  - `DevicesB`: B 侧设备名称列表（逗号分隔）

#### Scenario: 日志中展示完整上下文
- **WHEN** 记录配置校验失败的日志
- **THEN** 系统 MUST 包含以下信息：
  - 期望的配置："恰好一对 A/B"
  - 当前配置："A={CountA}, B={CountB}"
  - 失败原因：具体描述（例如"缺少 A 侧配置"、"存在多个 B 侧配置"）
  - 冲突设备列表：如果存在多个同侧配置，列出所有设备名称
