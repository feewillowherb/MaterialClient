## ADDED Requirements

### Requirement: 道闸会话创建与初始化
系统 MUST 在满足条件时创建新的道闸会话，并记录入口侧信息。

#### Scenario: 首次 LRP 识别时创建会话
- **WHEN** 称重状态为 `OffScale` 且道闸会话未激活（`SessionActive = false`）且收到 LRP 识别事件
- **THEN** 系统 MUST 创建新会话并设置：
  - `SessionActive = true`
  - `EntrySide =` 识别设备的 `Direction`（A 或 B）
  - `ExitOpened = false`
  - `SessionStartedAt =` 当前 UTC 时间
- **AND** 系统 MUST 调用 Vzvision SDK 打开入口道闸（`SetIoOutputAutoRespAsync`，500ms 脉冲）

#### Scenario: 会话期间拒绝重复触发
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到任何 LRP 识别事件（无论来自 A 侧或 B 侧）
- **THEN** 系统 MUST 拒绝触发开闸
- **AND** 系统 MUST 记录日志："道闸会话已激活，拒绝 LRP 触发: Device={Device}, EntrySide={EntrySide}"

#### Scenario: 会话创建失败不影响称重流程
- **WHEN** 创建会话或调用 SDK 开闸时发生异常
- **THEN** 系统 MUST 记录错误日志
- **AND** 系统 MUST 不抛出异常或中断称重流程
- **AND** 系统 MUST 允许称重状态机继续正常工作

### Requirement: 道闸会话清理与重置
系统 MUST 在车辆离磅后自动清理会话状态，允许下一辆车进入。

#### Scenario: OffScale 状态时清理会话
- **WHEN** 称重状态转换为 `OffScale` 且道闸会话已激活
- **THEN** 系统 MUST 重置会话状态：
  - `SessionActive = false`
  - `EntrySide = null`
  - `ExitOpened = false`
- **AND** 系统 MUST 记录日志："道闸会话已清理: SessionDuration={Duration}"

#### Scenario: 会话清理后允许新车进入
- **WHEN** 会话已清理（`SessionActive = false`）且收到新的 LRP 识别事件
- **THEN** 系统 MUST 创建新会话并正常触发入口开闸

#### Scenario: 会话清理失败不影响下一辆车
- **WHEN** 会话清理过程中发生异常
- **THEN** 系统 MUST 记录错误日志
- **AND** 系统 MUST 强制重置会话状态为未激活（防止会话泄漏）

### Requirement: 道闸会话状态查询
系统 MUST 提供会话状态查询接口，用于日志记录和故障排查。

#### Scenario: 查询当前会话状态
- **WHEN** 系统需要记录日志或排查故障
- **THEN** 系统 MUST 能够读取当前会话状态（`SessionActive`、`EntrySide`、`ExitOpened`、`SessionStartedAt`）
- **AND** 系统 MUST 能够计算会话持续时间（`DateTime.UtcNow - SessionStartedAt`）

#### Scenario: 会话状态的字符串表示
- **WHEN** 系统需要记录会话状态到日志
- **THEN** 系统 MUST 格式化输出为："SessionActive={SessionActive}, EntrySide={EntrySide}, ExitOpened={ExitOpened}, Duration={Duration}"
