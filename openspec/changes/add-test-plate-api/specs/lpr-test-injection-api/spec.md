## ADDED Requirements

### Requirement: 测试车牌注入接口
系统 SHALL 提供一个测试车牌注入接口，用于通过 HTTP 请求向消息总线发送模拟的车牌识别结果。

#### Scenario: 请求包含必填车牌号时可成功注入
- **WHEN** 调用方以 POST 请求提交非空 `plateNumber`（Trim 后非空）
- **THEN** 系统 SHALL 发布一条 `LicensePlateRecognizedMessage` 到 `MessageBus.Current`
- **AND** 响应状态码 SHALL 为 200，响应体包含成功标记与实际发送的车牌号

#### Scenario: 请求缺少车牌号时拒绝
- **WHEN** 调用方提交的请求体中 `plateNumber` 缺失、为 `null`、为空字符串或仅空白
- **THEN** 系统 SHALL 返回 400
- **AND** 系统 SHALL 不发布任何 `LicensePlateRecognizedMessage`

### Requirement: 可选字段映射与默认值
系统 SHALL 支持可选字段输入，并在缺失时使用默认值构造可消费的识别消息。

#### Scenario: 可选字段全部缺失时使用默认值
- **WHEN** 调用方仅提交必填 `plateNumber`
- **THEN** 系统 SHALL 使用默认值补齐可选字段并发布消息
- **AND** 发布消息中的 `PlateNumber` SHALL 等于请求中的 `plateNumber`（去除首尾空白后）

#### Scenario: 可选字段提供时按请求值映射
- **WHEN** 调用方提交 `deviceName`、`deviceType`、`colorType` 或 `timestamp` 中的任意字段
- **THEN** 系统 SHALL 将已提供字段映射到发布的 `LicensePlateRecognizedMessage`
- **AND** 未提供的字段 SHALL 使用默认值
