# Tasks: Vzvision SDK 替换 LprAllInOne Web

## 1. 枚举、类型重命名与配置兼容

- [x] 1.1 将 `LprDeviceType.LprAllInOne` 重命名为 `Vzvision`（或提案与设计选定的唯一名），并全库替换 `switch`/比较/转换器
- [x] 1.2 将 `LprAllInOneColorType` 重命名为 `VzvisionColorType`（或选定名），更新 `LicensePlateRecognizedMessage`、`LowPriorityPlateColors` 与相关测试
- [x] 1.3 实现设置 JSON 中对旧枚举名 `LprAllInOne` / 旧设备类型字符串的兼容反序列化或一次性迁移
- [x] 1.4 更新 `LicensePlateRecognitionConfig` 注释与校验：Vzvision 下 `Port`/`UserName`/`Password` 为 SDK 连接必填或可配置项

## 2. Vz 运行时服务与回调

- [x] 2.1 实现 `VzLPRClient_Setup`/`Cleanup` 与多设备 `Open`/`Close` 生命周期（应用启动/退出、配置变更重连）
- [x] 2.2 注册 `SetPlateInfoCallBack`，Marshal `TH_PlateResult`，映射 `nColor` → `VzvisionColorType`，组装 `LicensePlateRecognizedMessage`（`DeviceType = Vzvision`）
- [x] 2.3 非托管回调线程切换到 UI/安全上下文后再 `MessageBus.SendMessage`
- [x] 2.4 按 `design.md` Decisions 7：主路径 `SetPlateInfoCallBack`；**不**调用 `VzLPRClient_StartRealPlay`（无实时浏览）；可选 `VzClient_SetCommonResultCallBack`（`VZ_COMMON_RESULT_CALLBACK` 的 `type` 语义见 `VzLPRClientSDK.h` 2829–2840）

## 3. 主动抓拍与在线状态

- [x] 3.1 实现 `IVzvisionLprService`（替代 `ILprAllInOneService`）与 `ILprDevice`：`TriggerCaptureAsync` **默认**调用 `VzLPRClient_ForceTrigger(handle)`（非默认不调用 `ForceTriggerEx`，除非后续有明确设备/协议需求）
- [x] 3.2 删除 Comet 相关 `_triggerFlags`/`CheckAndClearTriggerFlag`/`RecordLastSeen`（HTTP 路径）；`IsOnline` 改为基于 `IsConnected` 与/或业务超时策略
- [x] 3.3 更新 `ILprDeviceResolver`、`LprDeviceOnlineStatusService` 注入与分支

## 4. MinimalWebHostService 精简

- [x] 4.1 移除 `POST /api/CarLicense/CallDeviceMessage` 与 `GET|POST /api/CarLicense/CallDeviceStatus` 的 LprAllInOne 实现及私有 DTO
- [x] 4.2 更新根路径 `endpoints` 列表与日志；回归华夏智信与 `POST /api/scale/weight`

## 5. UI 与设置

- [x] 5.1 设置窗口：`LprDeviceType = Vzvision` 时显示 UserName、Password、Port + 通用字段；隐藏海康 Channel（见 `specs/license-plate-recognition/spec.md`）
- [x] 5.2 更新本地化/文案与 `LprDeviceTypeConverter` 等显示名

## 6. 测试与文档

- [x] 6.1 更新 `AttendedWeighingServiceTests` 等引用新类型名；必要时增加 `IVzvisionLprService` mock
- [x] 6.2 扩展 `VzvisionIntegrationTests`（真机）覆盖：连接、回调收到车牌、ForceTrigger（若条件允许）
- [x] 6.3 发布说明：设备侧关闭 HTTP 推送/轮询、升级与回滚注意点
- [ ] 6.4 归档后同步 `openspec/specs/license-plate-recognition/spec.md` 主规范（若本变更流程要求）
