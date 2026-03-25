# 道闸 IO 控制系统重构

## Why

当前道闸 IO 控制功能存在严重的稳定性和安全性问题：车辆上磅未稳定时无法锁定道闸，可能导致车辆误入；缺乏状态可观测性，异常时无法人工干预；IO 控制逻辑与 LPR 紧密耦合，难以扩展其他 IO 控制模式。这些问题在称重业务高峰期会造成运营风险和安全隐患。

## What Changes

### 核心功能增强
- **实现稳定状态控制逻辑**：车辆上磅未稳定时自动锁定所有道闸 IO 并持续写入 0 信号；地磅稳定后根据车辆进入方向自动开启对应出口道闸
- **引入响应式状态管理**：基于现有 ReactiveUI 框架实现道闸 IO 状态流，提供状态变更的订阅和广播机制
- **增强状态展示接口**：提供道闸 IO 状态查询接口（锁定/解锁/异常）、状态变更事件通知、异常状态人工重置接口

### 架构优化
- **领域分离设计**：引入独立的 `GateIODirection` 枚举（Entry/Exit），与 LPR 的 `LicensePlateDirection` 解耦，确保道闸 IO 领域的独立性
- **解耦 IO 控制与 LPR**：设计独立的 `IGateIOController` 接口，当前实现 `VzLPRGateIOController`，预留工厂模式支持未来 IO 控制器扩展
  - **当前场景**：Vzvision LPR 设备自带 IO 功能，LPR 与 IO 使用同一 SDK
  - **未来扩展**：支持独立 IO 控制器（如海康 IO 模块），通过 `GateIOControllerId` 关联 LPR 识别事件与 IO 控制器
  - 详见 design.md 中的"架构扩展性：LPR 与独立 IO 控制器的协同"章节
- **配置验证与状态显示**：道闸 IO 功能启动时验证配置完整性（进出口道闸必须成对配置），配置无效时禁止启动；在状态栏显示道闸 IO 运行状态

### UI 增强
- **状态栏实时显示**：道闸 IO 当前状态（锁定/解锁/异常）、进出口配置状态
- **异常处理界面**：提供人工重置按钮，允许强制解除锁定状态

## Capabilities

### New Capabilities
- `gate-io-state-management`: 道闸 IO 状态机管理，包括状态定义（Idle/Locked/Opening/Error）、状态转换逻辑、状态持久化
- `gate-io-stability-control`: 地磅稳定状态控制逻辑，包括上磅锁定、稳定解锁、方向识别开闸
- `gate-io-configuration-validation`: 道闸 IO 配置验证，包括进出口成对检查、启动前验证、运行时监控
- `gate-io-manual-override`: 人工干预接口，包括状态查询、异常重置、强制解锁

### Modified Capabilities
- `vzvision-gate-io-control`: **BREAKING** - 从简单识别触发开闸升级为状态驱动的完整控制流程；新增状态管理职责；新增地磅状态事件订阅；新增配置验证要求

## Impact

### 代码变更范围
- **新增文件**：
  - `MaterialClient.Common/Entities/Enums/GateIOState.cs` - 状态枚举（Idle/Locked/Opening/Error）
  - `MaterialClient.Common/Entities/Enums/GateIODirection.cs` - 道闸方向枚举（Entry/Exit），独立于 LPR 的 Direction
  - `MaterialClient.Common/Services/GateIO/IGateIOController.cs` - IO 控制器接口（使用 GateIODirection）
  - `MaterialClient.Common/Services/GateIO/VzLPRGateIOController.cs` - VzLPR 实现（包含方向映射逻辑）
  - `MaterialClient.Common/Services/GateIO/GateIOControllerFactory.cs` - 工厂类
  - `MaterialClient.Common/Services/GateIO/GateIOStateService.cs` - 状态管理服务
  - `MaterialClient.Common/Events/GateIOStateChangedMessage.cs` - 状态变更消息
  - `MaterialClient.Common/Events/GateIOConfigurationValidationResult.cs` - 验证结果

- **修改文件**：
  - `MaterialClient.Common/Services/LprGateIoControlService.cs` - 重构为状态驱动模式
  - `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs` - 添加配置验证属性
  - `MaterialClient/ViewModels/StatusViewModel.cs` - 添加道闸 IO 状态显示
  - `MaterialClient/Views/StatusWindow.axaml` - 添加状态栏 UI 和重置按钮

- **删除文件**：
  - 无（向后兼容，仅重构内部实现）

### API 变更
- **新增 API**：
  - `GateIOStateService.GetStateAsync()` - 获取当前状态
  - `GateIOStateService.ResetAsync()` - 重置异常状态
  - `GateIOStateService.ForceUnlockAsync()` - 强制解锁
  - `IGateIOController.ValidateConfiguration()` - 验证配置

- **修改 API**：
  - `LprGateIoControlService.Initialize()` - 新增配置验证步骤
  - `LprGateIoControlService.StartAsync()` - 新增状态订阅逻辑

### 依赖变更
- **新增依赖**：无（复用现有 ReactiveUI 和 System.Reactive）
- **修改依赖**：无

### 系统影响
- **数据库**：无变更（配置仍在现有 LPR 配置存储中）
- **性能**：状态管理服务额外开销可忽略（内存状态机 + 事件广播）
- **兼容性**：向后兼容现有配置，新增配置验证不影响未启用道闸 IO 的 LPR 设备
- **测试**：需要新增单元测试覆盖状态机逻辑、集成测试覆盖完整流程

### 风险评估
- **高风险**：状态机逻辑错误可能导致道闸控制异常（通过充分单元测试缓解）
- **中风险**：配置验证可能阻止现有配置启动（通过提供降级选项缓解）
- **低风险**：UI 变更影响用户体验（通过渐进式发布缓解）
