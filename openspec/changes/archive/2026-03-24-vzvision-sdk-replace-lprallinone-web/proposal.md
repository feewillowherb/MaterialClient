# Proposal: Vzvision SDK 替换原 LprAllInOne Web 集成

## Why

当前 `LprDeviceType.LprAllInOne` 通过本机 HTTP（`MinimalWebHostService`）接收设备推送与 Comet 轮询触发，与同一硬件厂商提供的 **`VzLPRSDK.dll` 直连能力**重复，且增加端口、防火墙与双栈维护成本。迁移到 SDK 可在同一进程内完成识别与触发，并与已有 `VzvisionSdk` P/Invoke 对齐；同时按产品决策 **强制采用 `Vzvision` 前缀** 统一命名，避免 `LprAllInOne` 与 SDK 语义长期并存。

**依据文档**：[`docs/lpr-allinone-web-to-vz-sdk-migration-assessment.md`](../../../docs/lpr-allinone-web-to-vz-sdk-migration-assessment.md)。

## What Changes

- **BREAKING**：移除面向设备的 **`POST /api/CarLicense/CallDeviceMessage`** 与 **`GET|POST /api/CarLicense/CallDeviceStatus`** 中仅服务于原 LprAllInOne 的逻辑；现场设备须停止使用向 PC 推送/轮询的 Web 配置，改为由客户端通过 SDK 连接设备。
- **BREAKING**：`LprDeviceType` 中 **`LprAllInOne` 重命名为 `Vzvision`**（或项目选定的唯一最终名）；所有分支、持久化与 UI 文案同步迁移（含向后兼容策略）。
- **BREAKING**：`LprAllInOneColorType` 等公开类型重命名为 **`VzvisionColorType`**（或项目选定的唯一最终名）；`LicensePlateRecognizedMessage`、`appsettings` 中 `LowPriorityPlateColors` 等绑定需迁移或兼容反序列化。
- **BREAKING**：`ILprAllInOneService` / `LprAllInOneService` 由 **`IVzvisionLprService` / `VzvisionLprService`**（或项目选定名）替代；Comet 置位与「轮询即在线」模型删除，改为 SDK **`VzLPRClient_ForceTrigger`**（默认，非 `ForceTriggerEx`）、**`IsConnected`** 与/或业务层活跃度策略。
- **新增**：长驻 **Vz 运行时服务**（或等价模块）：`Setup`/`Cleanup`、按配置 `Open`、注册 **`SetPlateInfoCallBack`**、在回调中解析 `TH_PlateResult` 并发布 **`LicensePlateRecognizedMessage`**（`DeviceType = Vzvision`）；可选按头文件语义使用 **`VzClient_SetCommonResultCallBack` / `VZ_COMMON_RESULT_CALLBACK`**（`type` 含车牌/人脸等，见 `VzLPRClientSDK.h` 2829–2840）；多设备句柄管理与线程安全（回调线程 → MessageBus）。**不包含** **`VzLPRClient_StartRealPlay`** 及实时视频预览/浏览 UI。
- **保留**：`MinimalWebHostService` 中 **华夏智信**、**地磅测试** 等与非 Vz Web LPR 无关的路由（除非单独变更）；业务仍通过 **ReactiveUI MessageBus** 消费车牌事件。
- **配置**：`LicensePlateRecognitionConfig` 在 Vzvision 下需 **端口、用户名、密码**（等与 `VzLPRClient_Open` 对齐），与「仅海康使用 Port/UserName/Password」的注释与 UI 规则一并调整。

## Capabilities

### New Capabilities

- `vzvision-lpr-sdk`：描述通过 `VzLPRSDK` 建立连接、接收车牌回调、主动抓拍（`ForceTrigger*`）、在线判断、进程生命周期与线程约束；与 `VzvisionSdk` 封装的关系。

### Modified Capabilities

- `license-plate-recognition`：将场景中 **`LprAllInOne`** 统一为 **`Vzvision`**；增加/调整 **Vzvision 设备**在设置 UI 中的字段可见性（需 Port/UserName/Password 等）；持久化与向后兼容；移除对 HTTP 回调路径的需求描述（若有）。

## Impact

- **代码**：`MaterialClient/Services/MinimalWebHostService.cs`，`MaterialClient.Common/Services/LprAllInOne/**`（迁移至 `Vzvision`），`ILprDeviceResolver`，`LprDeviceOnlineStatusService`，`AttendedWeighingService`，设置与转换器，枚举 `LprDeviceType`，测试项目。
- **依赖**：`VzLPRSDK.dll` 及 `MaterialClient.Common` 已复制的 `VzSDK\**` 输出布局不变；需真机联调。
- **运维**：已部署设备若仍配置向 PC 的推送 URL，升级后不再生效；需升级说明与清单。
