# 道闸 IO 控制系统重构 - 实现任务清单

## 1. 基础类型和枚举定义

- [x] 1.1 创建 `GateIOState` 枚举（Idle/Locked/Opening/Error）
- [x] 1.2 创建 `GateIODirection` 枚举（Entry/Exit）- 独立于 LPR 的 LicensePlateDirection
- [x] 1.3 创建 `GateIOStateChangedMessage` 事件类（包含 PreviousState、CurrentState、Timestamp、Reason）
- [x] 1.4 创建 `GateIOConfigurationValidationResult` 类（包含 IsValid、Errors）
- [x] 1.5 创建 `GateIOStateDetails` 类（包含 State、LockedDirection、LockedDuration、LastError）
- [x] 1.6 在代码注释中说明 `GateIODirection` 与 `LicensePlateDirection` 的映射关系

## 2. IO 控制器接口和实现

- [x] 2.1 创建 `IGateIOController` 接口（使用 GateIODirection 而非 LicensePlateDirection）
- [x] 2.2 创建 `VzLPRGateIOController` 类实现 `IGateIOController`
- [x] 2.3 在 `VzLPRGateIOController` 中实现 LPR Direction 到 GateIO Direction 的映射方法
- [x] 2.4 在 `VzLPRGateIOController` 中实现 `ValidateConfigurationAsync` 方法（使用 GateIODirection）
- [x] 2.5 在 `VzLPRGateIOController` 中实现 `OpenGateAsync` 方法（接受 GateIODirection 参数）
- [x] 2.6 在 `VzLPRGateIOController` 中实现 `CloseGateAsync` 方法（接受 GateIODirection 参数）
- [x] 2.7 在 `VzLPRGateIOController` 中实现 `WriteOutputAsync` 方法（接受 GateIODirection 参数）
- [x] 2.8 创建 `GateIOControllerFactory` 静态工厂类

## 3. 配置验证器实现

- [x] 3.1 创建 `GateIOConfigurationValidator` 类
- [x] 3.2 实现进出口成对配置验证逻辑（使用 GateIODirection）
- [x] 3.3 实现每方向单设备验证逻辑（Entry/Exit 各最多一个）
- [x] 3.4 实现 IoChannel 必填字段验证
- [x] 3.5 实现设备类型能力验证
- [x] 3.6 实现 LPR Direction 到 GateIO Direction 的映射并验证
- [x] 3.7 实现验证结果收集和返回逻辑
- [x] 3.8 添加详细的验证日志记录

## 4. 状态管理服务核心实现

- [x] 4.1 创建 `GateIOStateService` 类框架
- [x] 4.2 实现状态机核心逻辑（状态转换规则和验证）
- [x] 4.3 实现 `BehaviorSubject<GateIOState>` 状态流
- [x] 4.4 实现状态变更事件广播（MessageBus 发送 GateIOStateChangedMessage）
- [x] 4.5 实现状态查询接口（GetState、GetStateAsync、GetStateWithDetailsAsync）
- [x] 4.6 实现服务生命周期管理（StartAsync、StopAsync）

## 5. 事件订阅和状态协调逻辑

- [x] 5.1 在 `GateIOStateService` 中订阅 `LicensePlateRecognizedMessage` 事件
- [x] 5.2 在 `GateIOStateService` 中订阅 `StatusChangedMessage` 事件
- [x] 5.3 实现识别事件处理逻辑（从 LPR Direction 映射到 GateIO Direction）
- [x] 5.4 实现地磅稳定状态处理逻辑（解锁和开闸）
- [x] 5.5 实现地磅 WaitingForStability 状态处理逻辑（触发锁定）
- [x] 5.6 实现地磅 OffScale 状态处理逻辑（重置为 Idle）
- [x] 5.7 实现车牌识别和地磅状态的协调逻辑（先识别后上磅、先上磅后识别）

## 6. 锁定状态控制逻辑

- [x] 6.1 实现进入 Locked 状态时的逻辑（启动定时器）
- [x] 6.2 实现每 100ms 持续写入 0 的定时器逻辑（使用 GateIODirection.Entry 和 Exit）
- [x] 6.3 实现锁定期间记录车辆进入方向（存储 GateIODirection）
- [x] 6.4 实现退出 Locked 状态时的逻辑（停止定时器）
- [x] 6.5 实现锁定超时检测和自动进入 Error 状态逻辑
- [ ] 6.6 添加超时阈值配置支持（appsettings.json 中的 GateIO:LockTimeoutSeconds）

## 7. 开闸逻辑实现

- [x] 7.1 实现地磅稳定后解锁逻辑
- [x] 7.2 实现根据 GateIODirection 确定开闸目标的逻辑（Entry→Exit、Exit→Entry）
- [x] 7.3 实现 Opening 状态的执行逻辑
- [x] 7.4 实现开闸完成后重置为 Idle 的逻辑
- [x] 7.5 实现方向信息缺失时的默认行为处理

## 8. 人工干预接口实现

- [x] 8.1 实现 `ResetAsync` 方法（重置 Error 状态为 Idle）
- [x] 8.2 实现 `ForceUnlockAsync` 方法（强制解锁 Locked 状态）
- [x] 8.3 实现人工操作的审计日志记录
- [x] 8.4 实现 `GetOperationHistoryAsync` 方法（查询操作历史）
- [ ] 8.5 实现操作历史记录和持久化逻辑
- [x] 8.6 实现并发控制（线程安全的状态转换）

## 9. 服务集成和初始化

- [x] 9.1 在 `DeviceManagerService.StartAsync()` 中调用配置验证器
- [x] 9.2 实现配置验证成功时启动 `GateIOStateService` 的逻辑
- [x] 9.3 实现配置验证失败时跳过初始化并显示错误的逻辑
- [x] 9.4 在依赖注入容器中注册 `GateIOStateService`
- [x] 9.5 在依赖注入容器中注册 `IGateIOController`（工厂模式）
- [x] 9.6 在依赖注入容器中注册 `GateIOConfigurationValidator`

## 10. 现有服务重构

- [ ] 10.1 重构 `LprGateIoControlService` 使用 `GateIOStateService`
- [ ] 10.2 在 `LprGateIoControlService` 中保留向后兼容的简单开闸逻辑
- [ ] 10.3 移除 `LprGateIoControlService` 中的直接 IO 控制代码（委托给 `GateIOStateService`）
- [ ] 10.4 更新 `LicensePlateRecognitionConfig` 添加配置验证属性（可选）
- [ ] 10.5 实现配置变更时重新验证和重启服务的逻辑（监听 SettingsSavedMessage）

## 11. UI 状态显示实现

- [ ] 11.1 在 `StatusViewModel` 中添加道闸 IO 状态属性（CurrentGateIOState、GateIOStateMessage）
- [ ] 11.2 在 `StatusViewModel` 中添加重置命令（ResetGateIOCommand）
- [ ] 11.3 在 `StatusViewModel` 中添加强制解锁命令（ForceUnlockGateIOCommand）
- [ ] 11.4 在 `StatusWindow.axaml` 中添加道闸状态显示控件
- [ ] 11.5 在 `StatusWindow.axaml` 中根据状态使用不同颜色显示（Idle=绿色、Locked=黄色、Error=红色）
- [ ] 11.6 在 `StatusWindow.axaml` 中添加重置按钮（仅在 Error 状态显示）
- [ ] 11.7 在 `StatusWindow.axaml` 中添加强制解锁按钮（仅在 Locked 状态显示，可选）
- [ ] 11.8 实现强制解锁确认对话框
- [ ] 11.9 实现操作结果显示提示（成功/失败消息）

## 12. 单元测试

- [ ] 12.1 创建 `GateIOConfigurationValidatorTests` 测试类
- [ ] 12.2 添加配置验证器的测试用例（进出口成对、单设备、IoChannel 必填）
- [ ] 12.3 创建 `GateIOStateServiceTests` 测试类
- [ ] 12.4 添加状态机转换逻辑的测试用例（合法转换、非法转换拒绝）
- [ ] 12.5 添加锁定状态控制逻辑的测试用例（定时器启动/停止、超时检测）
- [ ] 12.6 添加开闸逻辑的测试用例（方向识别、稳定后开闸）
- [ ] 12.7 添加人工干预接口的测试用例（重置、强制解锁）
- [ ] 12.8 创建 `VzLPRGateIOControllerTests` 测试类
- [ ] 12.9 添加 IO 控制器的 Mock 和测试用例
- [ ] 12.10 添加事件订阅和状态协调的测试用例

## 13. 集成测试

- [ ] 13.1 创建端到端测试场景（车辆上磅→锁定→稳定→开闸）
- [ ] 13.2 创建配置验证失败场景的集成测试
- [ ] 13.3 创建异常状态和人工重置场景的集成测试
- [ ] 13.4 创建并发场景的集成测试（自动状态变更与人工操作）
- [ ] 13.5 创建多识别事件的测试用例

## 14. 文档和部署准备

- [ ] 14.1 编写道闸 IO 配置指南（如何正确配置进出口道闸）
- [ ] 14.2 编写故障排查指南（常见错误和解决方案）
- [ ] 14.3 编写 API 文档（状态查询、重置、强制解锁接口）
- [ ] 14.4 准备配置迁移指南（现有用户如何升级配置）
- [ ] 14.5 准备回滚方案（功能开关配置）
- [ ] 14.6 在测试环境执行完整验证流程
- [ ] 14.7 准备灰度发布计划（选择试点用户）
- [ ] 14.8 准备监控和日志分析方案

## 15. 未来扩展：支持独立 IO 控制器（可选后续工作）

> **背景**：当前实现针对 Vzvision LPR 设备自带 IO 的场景。未来如果使用独立 IO 控制器（如海康 IO 模块），需要以下扩展工作。
>
> **参考**：详见 design.md 中的"架构扩展性：LPR 与独立 IO 控制器的协同"章节

- [ ] 15.1 创建 `GateIOConfig` 配置类（独立于 LPR 配置）
- [ ] 15.2 创建 `GateIOControllerType` 枚举（Vzvision/Hikvision/Custom）
- [ ] 15.3 在 `LicensePlateRecognitionConfig` 中添加可选的 `GateIOControllerId` 字段
- [ ] 15.4 修改 `GateIOControllerFactory.Create()` 方法支持 `GateIOControllerType` 参数
- [ ] 15.5 在 `GateIOStateService` 中实现 LPR 与 IO 控制器的关联逻辑
- [ ] 15.6 实现基于 ID 查找 IO 控制器的逻辑（`GetGateIOConfig(string id)`）
- [ ] 15.7 添加配置迁移逻辑（从 LPR 配置迁移到独立 `GateIOConfig`）
- [ ] 15.8 实现向后兼容：`GateIOControllerId` 为空时使用旧的 LPR DeviceType 逻辑
- [ ] 15.9 添加独立 IO 控制器的单元测试
- [ ] 15.10 更新文档说明如何配置独立 IO 控制器

### 备选方案：全局进出口配置（简化版）

- [ ] 15.A1 创建 `GlobalGateIOConfig` 配置类（EntryGate/ExitGate）
- [ ] 15.A2 实现通过方向自动关联 IO 控制器的逻辑
- [ ] 15.A3 移除 LPR 配置中对 IO 控制器的显式引用
- [ ] 15.A4 更新 UI 配置界面支持全局 IO 配置
