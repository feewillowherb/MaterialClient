## Why

当前 `MinimalWebHostService` 仅提供地磅测试注入与设备回调接口，缺少可控的“测试车牌识别事件注入”入口，导致在无真实抓拍设备或联调窗口受限时难以稳定验证车牌识别后续流程。需要新增一个明确约束的测试接口，支持通过 HTTP 快速向消息总线发送测试识别结果。

## What Changes

- 在 `MinimalWebHostService` 新增测试车牌注入接口（POST）。
- 请求体中 `plateNumber` 为必填；其余字段（如 `deviceName`、`deviceType`、`colorType`、`timestamp`）为可选。
- 参数校验失败时返回 400（例如缺少车牌号或仅空白）。
- 校验通过后组装 `LicensePlateRecognizedMessage` 并发布到 `MessageBus.Current`。
- 返回统一成功响应，包含实际发送的关键字段，便于联调确认。

## Capabilities

### New Capabilities
- `lpr-test-injection-api`: 提供测试车牌识别结果注入接口，用于在不依赖真实设备时触发识别事件链路。

### Modified Capabilities
- `license-plate-recognition`: 增加“通过本地测试 API 注入识别消息”的行为要求与参数约束。

## Impact

- 主要影响代码：`MaterialClient/Services/MinimalWebHostService.cs`。
- 影响事件链路：`MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs` 的生产来源新增“测试注入 API”。
- 对外 API 影响：新增本地 HTTP 测试端点（非设备回调端点）。
- 测试影响：需要为请求校验与消息发布行为补充单元/集成验证。
