# Purpose

提供道闸会话状态管理能力，确保车辆通过过程中的入口侧锁定、会话互斥和状态清理。

## ADDED Requirements

### Requirement: 幽灵会话自动检测与重置
系统 MUST 在道闸会话激活期间，当检测到"幽灵会话"（车辆从未上磅但会话未清理）时，允许新车牌触发会话重置并恢复正常流程。

#### Scenario: 新车牌触发幽灵会话重置
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到新车牌识别事件（`PlateNumber` 与会话记录的 `PlateNumber` 不同，忽略大小写）且出口未开（`ExitOpened = false`）且称重状态为离秤（`WeighingStatus = OffScale`）
- **THEN** 系统 MUST 重置当前会话（调用 `Reset()`）
- **AND** 系统 MUST 记录警告日志，包含旧车牌、旧入口侧、旧会话时长、新车牌、新设备名
- **AND** 系统 MUST 继续创建新会话并触发入口开闸（不拒绝新车牌）

#### Scenario: 同一车牌重复识别时跳过
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到车牌识别事件且 `PlateNumber` 与会话记录的 `PlateNumber` 相同（忽略大小写）
- **THEN** 系统 MUST 跳过本次识别（不做任何处理）
- **AND** 系统 MUST 记录调试日志，包含车牌号和当前会话状态

#### Scenario: 正在称重时拒绝新车牌
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到新车牌识别事件且不满足幽灵会话条件（出口已开或称重状态不为离秤）
- **THEN** 系统 MUST 拒绝触发开闸
- **AND** 系统 MUST 记录信息日志，包含设备名、会话车牌、新车牌

---

### Requirement: 幽灵会话重置后发布领域事件
系统 MUST 在成功完成幽灵会话重置（满足既有「新车牌触发幽灵会话重置」场景，且 `Reset()` 已执行、新会话已创建）之后，立即发布一条用于称重侧同步的领域事件（见 `ghost-session-plate-cache-sync` 能力），事件 MUST 包含被废弃会话的车牌号。

#### Scenario: 重置成功后发布事件
- **WHEN** 「新车牌触发幽灵会话重置」场景已执行完毕且系统将开闸或继续处理当前 LRP
- **THEN** 系统 MUST 发布领域事件，且载荷 MUST 包含废弃前的会话车牌
- **AND THEN** 发布 MUST 发生在入口道闸异步开闸调用开始之前（同一线程内、在首次 `await` 开闸逻辑之前完成发送），以便订阅方尽快处理

### Requirement: 道闸会话创建与初始化
系统 MUST 在满足条件时创建新的道闸会话，并记录入口侧信息和车牌号。

#### Scenario: 首次 LRP 识别时创建会话
- **WHEN** 称重状态为 `OffScale` 且道闸会话未激活（`SessionActive = false`）且收到 LRP 识别事件
- **THEN** 系统 MUST 创建新会话并设置：
  - `SessionActive = true`
  - `EntrySide =` 识别设备的 `Direction`（A 或 B）
  - `ExitOpened = false`
  - `SessionStartedAt =` 当前 UTC 时间
  - `PlateNumber =` 识别到的车牌号
- **AND** 系统 MUST 调用 Vzvision SDK 打开入口道闸（`SetIoOutputAutoRespAsync`，500ms 脉冲）

#### Scenario: 会话期间拒绝重复触发
- **WHEN** 道闸会话已激活（`SessionActive = true`）且收到任何 LRP 识别事件（无论来自 A 侧或 B 侧）且不满足幽灵会话重置条件
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
  - `PlateNumber =` 空字符串
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
- **THEN** 系统 MUST 能够读取当前会话状态（`SessionActive`、`EntrySide`、`ExitOpened`、`SessionStartedAt`、`PlateNumber`）
- **AND** 系统 MUST 能够计算会话持续时间（`DateTime.UtcNow - SessionStartedAt`）

#### Scenario: 会话状态的字符串表示
- **WHEN** 系统需要记录会话状态到日志
- **THEN** 系统 MUST 格式化输出为："SessionActive={SessionActive}, EntrySide={EntrySide}, ExitOpened={ExitOpened}, Duration={Duration}, Plate={PlateNumber}"
