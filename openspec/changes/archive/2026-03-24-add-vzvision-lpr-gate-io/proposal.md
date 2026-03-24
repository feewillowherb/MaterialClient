## Why

当前系统在收到车辆识别结果后，尚未提供可配置的道闸 I/O 开闸联动能力，导致现场需要人工或外部系统补齐开闸动作。  
在新增 Vzvision SDK 替换方案的同时引入该能力，可直接打通识别到开闸的自动化闭环，并保持对不支持该能力设备的兼容。

## What Changes

- 在 `AddLprDialog` 中新增“是否启用道闸 I/O 功能”开关与 `ioChannel` 配置项，并纳入持久化配置（面向所有 LPR 设备统一建模）。
- 当启用该功能且供应商为 Vzvision 时，在收到车辆识别事件后向对应设备发送 `VzLPRClient_SetIOOutputAutoResp(..., 500)` 开闸脉冲信号（500ms 自动复位）。
- 其他 LPR 供应商当前不触发该 I/O 行为，并在日志中打印该设备类型暂未支持该能力，保持现有识别流程不变。
- 引入职责分离：车牌识别流程通过 `MessageBus` 发布“可触发开闸”的业务消息，设备 I/O 控制由独立服务订阅并处理，不在识别逻辑中直接耦合 SDK 调用。

## Capabilities

### New Capabilities
- `lpr-gate-io-control`: 管理 LPR 道闸 I/O 通用配置与能力门控规则，当前仅 Vzvision 实现开闸信号下发。

### Modified Capabilities
- `license-plate-recognition`: 增加“按供应商能力条件触发后置动作”的规范，并要求对未支持设备输出明确日志。

## Impact

- 受影响模块：`MaterialClient/Views/Dialogs/AddLprDialog.axaml` 及对应 ViewModel/配置映射、`MaterialClient.Common/Services/Vzvision/*`、识别事件处理链路。
- 运行影响：仅当配置启用且供应商为 Vzvision 时新增设备 I/O 写操作；其他设备类型走兼容分支并记录未支持日志。
