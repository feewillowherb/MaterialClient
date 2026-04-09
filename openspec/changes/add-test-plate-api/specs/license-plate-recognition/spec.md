## ADDED Requirements

### Requirement: 支持通过测试接口注入识别事件
系统 SHALL 支持通过本地测试接口触发识别事件，以在无真实抓拍回调时驱动车牌识别后续流程。

#### Scenario: 测试接口发布的识别事件进入统一事件通道
- **WHEN** 测试车牌注入接口接收到合法请求
- **THEN** 系统 SHALL 通过 `MessageBus.Current` 发布 `LicensePlateRecognizedMessage`
- **AND** 该消息 SHALL 与真实设备识别消息共享同一消费通道

#### Scenario: 测试接口事件在来源标识上可区分
- **WHEN** 系统根据测试接口请求构造 `LicensePlateRecognizedMessage`
- **THEN** 系统 SHALL 为来源相关字段赋予可识别值（由请求提供或由默认策略补齐）
- **AND** 下游日志或联调输出 SHALL 能区分“测试注入”与“设备回调”来源
