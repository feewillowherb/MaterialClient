## ADDED Requirements

### Requirement: LPR 主动抓拍功能开关

系统 MUST 在 `SystemSettings` 中提供 `EnableTriggerLprCapture` 布尔配置项，作为通用 LPR 主动抓拍功能的全局总开关。`AttendedWeighingService.TriggerLprCaptureForAllAsync` 方法 MUST 在方法体最前面检查该配置，当配置为 `false` 时 MUST NOT 执行任何抓拍逻辑，仅记录信息级日志并返回。默认值 MUST 为 `false`。

#### Scenario: 功能开关启用时正常执行抓拍

- **WHEN** `SystemSettings.EnableTriggerLprCapture` 为 `true`
- **AND** 调用 `TriggerLprCaptureForAllAsync(phase)`
- **THEN** 系统 MUST 继续执行后续守卫检查（设备类型、服务注入、设备配置）及抓拍逻辑

#### Scenario: 功能开关禁用时跳过抓拍

- **WHEN** `SystemSettings.EnableTriggerLprCapture` 为 `false`
- **AND** 调用 `TriggerLprCaptureForAllAsync(phase)`
- **THEN** 系统 MUST 记录信息级日志（包含 phase 参数）
- **AND** 系统 MUST NOT 执行任何后续抓拍逻辑
- **AND** 方法 MUST 正常返回

#### Scenario: 配置文件缺少该字段时使用默认值

- **WHEN** 配置文件中未包含 `EnableTriggerLprCapture` 字段
- **THEN** 系统 MUST 将该值解析为 `false`
- **AND** 行为 MUST 与显式设置为 `false` 一致

## MODIFIED Requirements

### Requirement: 主动抓拍（软件触发）

系统 MUST 在用户或业务触发 `ILprDevice.TriggerCaptureAsync`（Vzvision 实现）时，对目标设备句柄调用 **`VzLPRClient_ForceTrigger`**（与 `public static extern int VzLPRClient_ForceTrigger(int handle);` 一致），且 MUST NOT 默认使用 `VzLPRClient_ForceTriggerEx`，除非经文档或联调确认必须采用 TCP 扩展触发；且 MUST NOT 再依赖 HTTP Comet 或 `CallDeviceStatus` 响应携带 `manualTrigger`。在执行上述逻辑前，系统 MUST 先检查 `SystemSettings.EnableTriggerLprCapture` 功能开关，仅当其为 `true` 时才执行抓拍。

#### Scenario: 触发抓拍成功路径

- **WHEN** `EnableTriggerLprCapture` 为 `true`
- **AND** 配置有效且设备已连接
- **THEN** 系统 MUST 返回已完成的触发调用（或按 SDK 契约可判定为已下发触发），识别结果 MUST 仍仅通过 `LicensePlateRecognizedMessage` 传递

#### Scenario: 功能开关禁用时跳过触发

- **WHEN** `EnableTriggerLprCapture` 为 `false`
- **THEN** 系统 MUST NOT 调用 `VzLPRClient_ForceTrigger`
- **AND** 系统 MUST NOT 调用 `VzLPRClient_ForceTriggerEx`
