## Why

当前道闸 I/O 控制服务（`LprGateIoControlService`，将重命名为 `GateIoControlService`）仅响应车牌识别事件，不订阅称重状态变化（`AttendedWeighingStatus`），导致车辆离磅后道闸会话状态无法自动清理，影响下一辆车的正常通行。根据 `docs/evaluation-vzvision-lpr-gate-io-function-assessment-2026-03-25.md` 评估文档，需要实现状态同步机制以解决此问题。

## What Changes

- **服务重命名**：将 `LprGateIoControlService` 重命名为 `GateIoControlService`，消除命名歧义
- **新增道闸会话状态管理**：在 `GateIoControlService` 中实现 `GateIoSession` 会话机制，跟踪车辆入口侧、会话激活状态和出口开闸状态
- **订阅称重状态变化**：`GateIoControlService` 订阅 `StatusChangedMessage`，在状态转换为 `OffScale` 时自动清理会话
- **实现状态门控逻辑**：根据 `AttendedWeighingStatus` 控制道闸开关时机（稳定/稳重阶段禁开闸，等待下磅阶段开出口闸）
- **统一道闸侧别枚举**：将 `LicensePlateDirection` 从 `In/Out` 改为 `A/B`，与 LPR 设备配置解耦，支持双向通行
- **添加启动配置校验**：验证 A/B 配置成对性（恰好一对），防止配置错误导致道闸误动作
- **支持降级模式**：道闸控制失败仅记录日志，不阻断称重主流程，允许人工遥控器干预
- **双控制模式架构**：设计支持两种道闸 I/O 控制方式
  - **方式 1（默认）**：通过 LRP SDK（Vzvision）控制道闸 I/O（当前实现）
  - **方式 2（预留）**：直接通过 COM 控制道闸 I/O（预留接口设计，实现抛出"不支持"异常）

## Capabilities

### New Capabilities
- `gate-io-session-management`: 道闸会话状态管理，包括会话创建、维护和清理的生命周期控制
- `gate-io-state-synchronization`: 道闸 I/O 与称重状态同步，确保会话状态与称重状态机一致
- `gate-io-direction-gating`: 基于称重状态和入口侧的道闸开关门控逻辑
- `gate-io-configuration-validation`: 道闸 A/B 配置有效性校验（启动时执行）
- `gate-io-dual-control-mode`: 双控制模式架构，支持 LRP SDK 和 COM 直接控制两种方式

### Modified Capabilities
- `vzvision-gate-io-control`: 扩展现有道闸 I/O 控制能力，增加状态门控与会话管理（原仅为"识别即开闸脉冲"）

## Impact

**影响代码模块**：
- `MaterialClient.Common\Services\GateIoControlService.cs`（原 `LprGateIoControlService.cs`）：服务重命名、新增会话状态、订阅 `StatusChangedMessage`、实现状态门控逻辑、新增双控制模式接口
- `MaterialClient.Common\Entities\Enums\LicensePlateDirection.cs`：枚举值从 `In/Out` 改为 `A/B`（**BREAKING**）
- `MaterialClient.Common\Configuration\LicensePlateRecognitionConfig.cs`：`Direction` 字段类型变更影响序列化/反序列化
- `MaterialClient.Common\Events\StatusChangedMessage.cs`：现有事件，新增订阅者
- `MaterialClient\ViewModels\AddLprDialogViewModel.cs`：UI 配置界面需适配 A/B 选项（原 In/Out）
- `MaterialClient\ViewModels\SettingsWindowViewModel.cs`：设置界面需适配 A/B 选项

**影响 API/接口**：
- `ILprGateIoControlService` → `IGateIoControlService`：接口重命名
- `LprGateIoControlService` → `GateIoControlService`：服务类重命名
- `IGateIoControlService` 新增方法：
  - `OnStatusChanged()`：处理称重状态变化
  - `ValidateGateConfiguration()`：验证 A/B 配置成对性
  - `ClearSession()`：清理会话状态
  - `OpenGateViaLrpSdk()`：通过 LRP SDK 控制道闸（方式 1，现有实现）
  - `OpenGateViaCom()`：通过 COM 直接控制道闸（方式 2，预留实现抛出异常）

**影响依赖**：
- 无新增外部依赖

**数据迁移需求**：
- 现有配置中的 `LicensePlateDirection.In/Out` 需迁移为 `A/B`（可通过默认值映射处理）

**兼容性**：
- 无道闸模式（`EnableGateIo = false`）不受影响
- 现有道闸功能继续工作，但需要配置项迁移
