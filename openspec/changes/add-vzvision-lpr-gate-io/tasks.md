## 1. 配置模型与持久化

- [x] 1.1 在 LPR 配置实体与 DTO 中新增 `EnableGateIo`、`IoChannel` 字段，并保证默认值与向后兼容
- [x] 1.2 更新设置序列化/反序列化流程，确保新字段可正确保存与恢复

## 2. UI 配置入口（AddLprDialog）

- [x] 2.1 在 `AddLprDialog.axaml` 新增“是否启用道闸 I/O 功能”与 `IoChannel` 输入控件
- [x] 2.2 在对应 ViewModel 增加绑定属性与校验规则，并实现“配置可统一建模、当前仅 Vzvision 可执行”能力门控

## 3. 识别后开闸编排与职责分离

- [x] 3.1 定义并接入 MessageBus 的 I/O 触发消息（可复用识别消息或新增专用消息），明确必要字段（设备类型、设备标识、车牌、时间等）
- [x] 3.2 抽取或新增独立 I/O 控制服务接口，订阅 MessageBus 并封装 `VzLPRClient_SetIOOutputAutoResp(..., 500)` 调用
- [x] 3.3 在消息处理编排中实现能力门控：仅当 `LprDeviceType = Vzvision` 且 `EnableGateIo = true` 时触发 I/O，其余记录未支持日志
- [x] 3.4 管理 MessageBus 订阅生命周期（启动订阅/停止释放），避免重复订阅导致重复开闸

## 4. 功能联调与运行确认

- [ ] 4.1 本地联调验证：`LprDeviceType = Vzvision` 且 `EnableGateIo = true` 时，识别后向指定 `ioChannel` 下发 `VzLPRClient_SetIOOutputAutoResp(..., 500)` 开闸脉冲
- [ ] 4.2 本地联调验证：`EnableGateIo = false` 或非 Vzvision 时不触发 I/O，下发路径输出未支持日志
- [ ] 4.3 完成一次设置保存/重启/重载的端到端检查，确认配置持久化与既有识别流程无明显回退
