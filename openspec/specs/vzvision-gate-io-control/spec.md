## ADDED Requirements

### Requirement: LPR 道闸 I/O 通用配置项
系统 MUST 提供面向 LPR 设备的道闸 I/O 通用配置能力，包括是否启用开关与 `IoChannel` 通道号，并将其持久化到现有 LPR 配置存储中。

#### Scenario: 在 Vzvision 配置中展示并编辑 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置 `LprDeviceType = Vzvision`
- **THEN** 系统 MUST 显示“是否启用道闸 I/O 功能”开关与 `IoChannel` 输入项，并允许编辑

#### Scenario: 非 Vzvision 设备不暴露 I/O 配置
- **WHEN** 用户在 `AddLprDialog` 中配置非 Vzvision 设备类型
- **THEN** 系统 MUST 不显示或不允许编辑道闸 I/O 配置项

#### Scenario: 保存并加载 I/O 配置
- **WHEN** 用户保存并重新打开设置
- **THEN** 系统 MUST 正确序列化和反序列化 `EnableGateIo` 与 `IoChannel`，保持值不丢失

#### Scenario: 非 Vzvision 设备保留配置但当前不执行
- **WHEN** 非 Vzvision 设备存在 `EnableGateIo` 与 `IoChannel` 配置
- **THEN** 系统 MUST 保留并加载配置值，但在运行时按能力门控判定为当前不支持

### Requirement: 识别后触发开闸信号
系统 MUST 通过 MessageBus 驱动的后置动作流程，在满足条件时调用 Vzvision SDK 向指定通道下发 `500ms` 自动复位开闸脉冲。

#### Scenario: 启用配置后识别触发开闸
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = true`，且识别链路通过 MessageBus 收到有效车辆识别消息
- **THEN** 系统 MUST 调用 `VzLPRClient_SetIOOutputAutoResp(handle, ioChannel, 500)`（500ms 自动复位）

#### Scenario: 未启用配置时不触发开闸
- **WHEN** 设备类型为 Vzvision，`EnableGateIo = false`，且收到车辆识别事件
- **THEN** 系统 MUST 不调用 I/O 开闸接口

#### Scenario: 非 Vzvision 设备记录未支持日志
- **WHEN** 设备类型不是 Vzvision，且识别后进入道闸 I/O 后置动作评估
- **THEN** 系统 MUST 不调用 I/O 开闸接口，并 MUST 记录“当前设备类型未支持道闸 I/O 功能”的日志

### Requirement: I/O 控制职责分离
系统 MUST 将道闸 I/O 控制职责与车牌识别职责分离，识别服务不得直接承担硬件 I/O 下发编排责任。

#### Scenario: 识别服务与 I/O 服务解耦
- **WHEN** 发生识别事件处理流程
- **THEN** 系统 MUST 通过 MessageBus 发布/订阅与独立 I/O 控制服务执行开闸动作，而非在识别解析逻辑中直接调用 SDK 下发

#### Scenario: I/O 服务通过 MessageBus 订阅接收触发
- **WHEN** 识别服务发布车牌识别消息或专用 I/O 触发消息
- **THEN** I/O 控制服务 MUST 通过 MessageBus 订阅接收并执行业务门控判断
