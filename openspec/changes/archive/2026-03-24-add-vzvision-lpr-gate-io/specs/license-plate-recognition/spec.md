## ADDED Requirements

### Requirement: 识别后动作按供应商能力门控
系统 MUST 在 MessageBus 驱动的车牌识别后置动作中按设备类型进行能力门控，未声明支持某能力的供应商不得触发该能力。

#### Scenario: Vzvision 可触发道闸 I/O 后置动作
- **WHEN** `LprDeviceType = Vzvision` 且识别消息到达 MessageBus 后置动作编排
- **THEN** 系统 MAY 进入道闸 I/O 执行分支（仍受 `EnableGateIo` 配置约束）

#### Scenario: 非 Vzvision 不触发道闸 I/O 后置动作
- **WHEN** `LprDeviceType != Vzvision` 且识别消息到达 MessageBus 后置动作编排
- **THEN** 系统 MUST 跳过道闸 I/O 执行分支，并继续其他不依赖该能力的识别流程

#### Scenario: 非支持设备输出可观测日志
- **WHEN** `LprDeviceType != Vzvision` 且道闸 I/O 功能被启用或进入评估流程
- **THEN** 系统 MUST 输出“当前设备类型暂未支持道闸 I/O 功能”的日志，帮助定位能力差异
