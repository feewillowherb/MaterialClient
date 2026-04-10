## 1. API 契约与路由接入

- [x] 1.1 在 `MinimalWebHostService` 定义测试车牌接口路由常量并加入根路由 `endpoints` 列表
- [x] 1.2 新增测试车牌请求 DTO，包含必填 `plateNumber` 与可选字段（`deviceType`、`deviceName`、`colorType`、`timestamp`）
- [x] 1.3 在 `ConfigureEndpoints` 中实现 POST 路由映射，完成请求反序列化与统一响应结构

## 2. 参数校验与消息发布

- [x] 2.1 实现 `plateNumber` 的必填校验（`null`/空/仅空白均返回 400）
- [x] 2.2 实现可选字段默认值策略并组装 `LicensePlateRecognizedMessage`
- [x] 2.3 调用 `MessageBus.Current.SendMessage(...)` 发布测试识别消息并记录可观测日志
- [x] 2.4 处理异常分支，返回 500 且保持日志可追踪

## 3. 验证与回归

- [ ] 3.1 补充接口行为测试：成功注入、必填校验失败、可选字段默认/透传映射
- [ ] 3.2 手动联调验证接口调用样例，确认消息消费链路可被触发
- [ ] 3.3 回归验证现有华夏智信回调接口与地磅测试接口行为未受影响
