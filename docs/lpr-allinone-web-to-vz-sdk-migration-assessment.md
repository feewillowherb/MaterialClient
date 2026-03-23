# Vzvision 一体机（原 LprAllInOne）：Web 回调 → VzSdk 迁移 — 变更规模与风险评估

> **文档性质**：基于当前仓库代码结构的评估报告，**非实施说明**。  
> **模型说明**：本报告由编码助手根据代码检索与阅读整理。  
> **日期**：2025-03-23  
> **命名策略**：实施后公开 API 与设备类型枚举 **强制** 使用 **Vzvision** 前缀（§1.1）；下文 **§2** 仍用历史名称描述**当前代码**行为。

---

## 1. 背景与目标

- **现状**：`LprDeviceType.LprAllInOne` 表示与 **Vz 一体机** 对接，但集成方式为 **HTTP**：本机启动 Kestrel（`MinimalWebHostService`），设备通过 **POST 推送识别结果**、**Comet 轮询** 拉取手动触发指令。
- **目标**：在硬件仍为同一类 Vz 设备的前提下，将识别与触发改为 **官方 `VzLPRSDK.dll`（项目内称 VzSdk / `VzvisionSdk`）**，弃用基于 Web 的 LprAllInOne 集成路径。
- **不变**：业务侧仍以 `LicensePlateRecognizedMessage`（ReactiveUI MessageBus）消费车牌事件。
- **强制（命名）**：凡原 **LprAllInOne** 语义的公开 API、类型与目录，**一律改为 `Vzvision` 前缀**（或统一归入 `Vzvision*` 命名族），**禁止**长期保留 `LprAllInOne` 作为设备类型或类型名。详见 **§1.1**。

### 1.1 强制重命名范围（Vzvision 前缀）

实施时需统一替换（具体拼写可在评审中敲定，但必须带 **Vzvision** 前缀且全库一致）：

| 现行（待废弃） | 目标方向（示例） | 说明 |
|----------------|------------------|------|
| `LprDeviceType.LprAllInOne` | `LprDeviceType.Vzvision`（或 `VzvisionAllInOne`，择一固化） | 设置持久化、条件分支、转换器、文档中的设备类型字面量均需迁移。 |
| `LprAllInOneColorType` | `VzvisionColorType`（或 `VzvisionPlateColorType`） | `LicensePlateRecognizedMessage.ColorType`、`LowPriorityPlateColors`、JSON 配置键值若存枚举名需 **兼容或迁移脚本**。 |
| `ILprAllInOneService` / `LprAllInOneService` | `IVzvisionLprService` / `VzvisionLprService`（示例） | DI、`LprDeviceResolver`、`AttendedWeighingService` 注入点。 |
| 命名空间与目录 `...Services.LprAllInOne` | `...Services.Vzvision`（与 `VzvisionSdk` 同域或子命名空间） | 减少「AllInOne」与「SDK」两套并列概念。 |

**连带影响**：已保存的 `appsettings` / 用户设置中若序列化 **旧枚举名或旧设备类型字符串**，必须提供 **向后兼容反序列化** 或 **一次性迁移**，否则升级后配置会静默失效。

---

## 2. 当前架构摘要（Web 路径）

| 能力 | 实现位置 | 行为 |
|------|-----------|------|
| 识别结果上行 | `MinimalWebHostService`：`POST /api/CarLicense/CallDeviceMessage` | 解析 JSON `AlarmInfoPlate` → 发布 `LicensePlateRecognizedMessage`（含 `LprAllInOneColorType?`） |
| 手动/主动抓拍触发 | `LprAllInOneService` + `GET/POST /api/CarLicense/CallDeviceStatus` | **Comet**：服务端置位 `_triggerFlags`，设备轮询时在响应中返回 `manualTrigger: ok` |
| 在线状态 | `LprAllInOneService.RecordLastSeen` / `IsOnline` | 依赖 **CallDeviceStatus 轮询** 更新最后可见时间（默认 2 分钟超时） |
| Web Host 启动 | `App.axaml.cs` 后台任务 `MinimalWebHostService.StartAsync` | **非海康**时启动；海康为避免端口冲突 **不启动** Web Host |

依赖关系要点：

- `ILprDeviceResolver` 在 `LprAllInOne` 时解析为 `LprAllInOneService`（实现 `ILprDevice`：`TriggerCaptureAsync` → 内部 `TriggerManualRecognitionAsync`）。
- `LprDeviceOnlineStatusService` 对 `LprAllInOne` 委托 `ILprAllInOneService.IsOnline`。
- `AttendedWeighingService` 等多处按 `LprDeviceType.LprAllInOne` 分支调用 `_lprAllInOneService`（如触发抓拍）；车牌结果已统一走 MessageBus。

---

## 3. 仓库内已有 VzSdk 资产（与目标对齐）

- **P/Invoke 封装**：`MaterialClient.Common/Services/Vzvision/VzvisionSdk.cs`（`internal`）  
  已声明：`VzLPRClient_Setup` / `Cleanup`、`Open` / `Close` / `IsConnected`、`SetPlateInfoCallBack`、`ForceTrigger` / `ForceTriggerEx`、`StartRealPlay`（需窗口句柄）及 IO 相关 API 等；含 `TH_PlateResult`、`VZLPRC_PLATE_INFO_CALLBACK` 等结构体与回调类型。
- **原生文件输出**：`MaterialClient.Common/MaterialClient.Common.csproj` 已将 `VzSDK\**` 复制到输出目录（与海康 HCNetSDK 类似）。
- **集成测试骨架**：`MaterialClient.Common.Tests/Tests/VzvisionIntegrationTests.cs`（默认 Skip，需真机与 `VzLPRSDK.dll`）。

**结论**：SDK 的 **声明层与部署路径已具备**；缺的是 **长驻服务**：连接生命周期、多设备句柄、回调里组包 `LicensePlateRecognizedMessage`、与 **`IVzvisionLprService`（替代原 `ILprAllInOneService`）** / `ILprDevice` 的对接，以及 **弃用 Web 端点** 后的行为调整；并叠加 **§1.1 强制重命名** 的全库替换与配置迁移。**是否与原 LprAllInOne（Web）功能等价** 的对照结论见 **§4**。

---

## 4. 当前 VzSdk（`VzvisionSdk`）与原 LprAllInOne 功能覆盖分析

> 本节回答：**仓库内已封装的 `VzLPRSDK.dll` P/Invoke 是否足以覆盖原 Web（LprAllInOne）路径的功能**。依据为 `MaterialClient.Common/Services/Vzvision/VzvisionSdk.cs` 与 Web 实现（`MinimalWebHostService`、`LprAllInOneService`）的对照。

### 4.1 原 LprAllInOne（Web）能力拆解

| 能力 | 实现要点 |
|------|----------|
| **被动识别** | `POST /api/CarLicense/CallDeviceMessage`：解析 `license`、`colorType`，发布 `LicensePlateRecognizedMessage`（含 `DeviceName`、`DeviceType` 等） |
| **主动/手动抓拍** | `GET/POST /api/CarLicense/CallDeviceStatus`（Comet）：服务端置位 → 设备下次轮询收到 `manualTrigger: ok`；`ILprDevice.TriggerCaptureAsync` 仅负责置位 |
| **在线状态** | 每次 Comet 调用 `RecordLastSeen(ip)`；`IsOnline` ≈ 该 IP 在默认 **约 2 分钟** 内有过轮询 |

### 4.2 与 `VzvisionSdk` 声明能力的对应关系

| 原 Web 能力 | SDK 侧（当前仓库已声明） | 覆盖结论 |
|-------------|--------------------------|----------|
| 被动上报车牌 + 颜色 | `VzLPRClient_SetPlateInfoCallBack` + `TH_PlateResult`（`license`、`nColor` 等）；回调中可区分 `VZ_LPRC_RESULT_TYPE` | **可覆盖**：需在托管代码中完成 `pResult` 的 Marshal、将 `nColor` **映射**到业务颜色枚举（与 HTTP `colorType` 数值未必一致） |
| 主动触发识别 | `VzLPRClient_ForceTrigger` / `ForceTriggerEx`（结果类型中含 `VZ_LPRC_RESULT_FORCE_TRIGGER`） | **可覆盖**：集成模型由「设备轮询取 `manualTrigger`」变为 **PC 主动调 ForceTrigger**，语义等价、机制不同 |
| 在线判断 | `VzLPRClient_IsConnected` | **可替代但不等价**：原为「最近轮询过」；SDK 为 **连接/会话状态**。若需「多久无识别则离线」须在业务层自行计时 |
| 设备显示名 | HTTP JSON 提供 `AlarmInfoPlate.DeviceName` | **不来自同一数据源**：SDK 回调以 `handle` 为主，通常用 **`LicensePlateRecognitionConfig.Name`**（按 IP/句柄映射）填充 `LicensePlateRecognizedMessage.DeviceName`，功能可对齐 |
| 进程级初始化 | Web 路径不需要 | **新增义务**：`VzLPRClient_Setup` / `VzLPRClient_Cleanup` |

### 4.3 集成时须单独核对的点（非「缺 API」而是前置条件）

| 项目 | 说明 |
|------|------|
| **`StartRealPlay(handle, hWnd)`** | 部分场景下识别管线依赖预览/解码。若厂商要求必须先 `StartRealPlay` 才有稳定识别，则需 **HWND** 或查阅文档是否允许 **0 / 空句柄** 等用法；无人值守桌面场景须在 **官方文档 + 真机** 上确认。 |
| **多通道 / 多路** | Web JSON 含 `channel` 字段。当前封装以「一次 `Open` → 一个句柄」为主；若单机多路需多句柄或额外 API，以 **完整 SDK 文档** 为准，不能仅凭 `VzvisionSdk.cs` 断定。 |
| **P/Invoke 覆盖范围** | `VzvisionSdk.cs` 仅为项目当前声明的 **子集**；若业务曾依赖 Web 协议中的 **大图、扩展字段** 等，需对照 **厂商头文件/文档** 确认是否还有未封装导出函数。 |
| **图片** | Web 回调片段未强调大图；SDK 回调含 `pImgFull` / `pImgPlateClip`（与 `bEnableImage` 相关），能力上 **可强于** 原 HTTP 片段。 |

### 4.4 小结

- **主流程（被动识别 + 主动触发 + 连接/在线类状态）**：当前 `VzvisionSdk` 中已声明的入口 **足以支撑迁移**，**不是**「缺少 DLL 导出」类问题；差异主要在 **集成模型**（推送 URL → 长连接 + 回调）与 **语义细节**（在线、颜色映射、设备名来源）。
- **「完全等价于原 Web 行为」**：需在对接层实现后，结合 **`StartRealPlay` 是否必选**、**`nColor` 与旧 `colorType` 映射**、多通道策略等，经 **文档与实测** 再下结论。

---

## 5. 预计变更规模（按模块）

以下为**粗粒度**文件/模块级估计，实施时可能合并或拆分。

### 5.1 高影响（必改）

| 区域 | 预估 | 说明 |
|------|------|------|
| 新建 Vz 运行时服务 | **中～大** | 例如：启动时 `Setup`，按 `LicensePlateRecognitionConfig` 列表 `Open`、注册 `SetPlateInfoCallBack`，在回调中解析 `TH_PlateResult`（车牌字节数组、`nColor` 等），发布 MessageBus；进程退出 `Cleanup`。需处理 **多路设备**、重连、线程安全。 |
| `VzvisionLprService`（原 `LprAllInOneService`） | **大** | 移除 Comet 标志位与「轮询即在线」模型；`TriggerCaptureAsync` 改为调用 `VzLPRClient_ForceTrigger`（或 `ForceTriggerEx`）针对对应句柄；`IsOnline` 改为 `IsConnected` 或「近期收到识别/心跳」策略（需产品确认）；类/接口名按 **§1.1** 更名。 |
| `MinimalWebHostService` | **中** | 删除或停用 **仅服务于原 LprAllInOne** 的两处路由及私有 DTO（`LprAllInOnePlateCallback` 等）；**保留**华夏智信回调、地磅测试 `POST /api/scale/weight` 等（见 5.3）。 |
| `LicensePlateRecognitionConfig` / 设置 UI | **中** | 当前注释称 `Port`/`UserName`/`Password` 为海康专用；Vz `Open` 需要 **端口与账号**。需扩展语义或文档，使 **`LprDeviceType.Vzvision`** 下可配置 **SDK 连接参数**（默认值需与设备文档对齐）。 |
| 颜色与枚举映射 | **小～中** | HTTP 路径曾用 `colorType` 对齐旧枚举；SDK 使用 `TH_PlateResult.nColor`。需 **对照 SDK 文档** 映射到 **`VzvisionColorType`**（§1.1），避免「低优先级颜色」等业务规则偏差。 |

### 5.2 中影响（连带调整）

| 区域 | 预估 | 说明 |
|------|------|------|
| `DeviceManagerService` | **小～中** | 注释中有「TODO：启动其他 LPR」；可在 `LprDeviceType.Vzvision` 时启动/停止 Vz 连接服务，与称重等生命周期一致。 |
| `ILprDevice` / `ILprDeviceResolver` | **小～中** | 解析器需将 `Vzvision` 映射到 **`IVzvisionLprService`**（新名）；全量重命名会触及 **所有 switch/比较**，测试需同步替换。 |
| 测试 | **中～大** | `AttendedWeighingServiceTests` 等凡引用旧类型名处 **必须** 改为 `Vzvision*`；需 **mock 或集成测试** 覆盖新服务；`VzvisionIntegrationTests` 可扩展为「回调收到 plate」类用例（仍依赖真机）。 |

### 5.3 低影响或可能不改

- **`LicensePlateRecognizedMessage`**：`DeviceType` 使用重命名后的 **`LprDeviceType.Vzvision`**（§1.1）；消息类型本身可不改名。
- **配置键**：`appsettings` 中 `LowPriorityPlateColors` 等若绑定枚举类型，需改为 **`VzvisionColorType[]`** 并提供旧值迁移说明。
- **`MinimalWebHostService` 整体**：只要仍存在 **华夏智信** 或 **地磅测试模式 HTTP**，宿主仍会在非海康场景启动；迁移后只是 **原 LprAllInOne Web 路由消失**，不是必然删除整个服务。

---

## 6. 功能映射（Web → SDK）

| Web（当前） | SDK（目标方向） |
|-------------|-----------------|
| `POST .../CallDeviceMessage` 收到 JSON | `VZLPRC_PLATE_INFO_CALLBACK` 中解析 `pResult` / `uNumPlates`，转字符串与颜色 |
| `CallDeviceStatus` Comet + `manualTrigger` | `VzLPRClient_ForceTrigger` / `ForceTriggerEx`（按设备句柄） |
| 轮询 `ipaddr` → `RecordLastSeen` | `VzLPRClient_IsConnected`；和/或回调活跃度；**不再依赖** 设备访问本机 HTTP |
| `StartRealPlay` + `hWnd` | 若仅需识别、不需预览，需查 SDK 是否允许 **空 HWND 或无需预览** 的工作模式；否则要嵌入视频控件或走文档推荐的最小化用法 |

**说明**：`StartRealPlay` 与窗口句柄相关，是常见的集成风险点，必须在实施阶段对照 **Vz 官方开发文档** 确认无人值守桌面场景下的推荐初始化顺序。

---

## 7. 风险评估

### 7.1 技术风险

| 风险 | 等级 | 说明与缓解 |
|------|------|------------|
| 回调线程与 UI / MessageBus | **高** | 非托管回调可能在 **非 UI 线程**；发布消息、写日志、访问服务需 `Dispatcher`/`Post` 或确保线程安全。 |
| 多设备连接与资源泄漏 | **高** | 多台 Vz 设备需 **每设备句柄**管理，`Close`/`Cleanup` 必须在应用退出与配置变更时可靠执行。 |
| 颜色/类型枚举不一致 | **中** | HTTP `colorType` 与 `nColor` 若不一致，会导致 **低优先级车牌** 等业务规则错误；需实车或文档校验。 |
| DLL 位数与依赖 | **中** | 项目为 `win-x64`；需确认 `VzLPRSDK.dll` 及附带库与运行时一致，发布目录完整（csproj 已复制 `VzSDK\**`）。 |
| 与海康共用端口/防火墙 | **低** | 原 Web 方案需设备能访问 PC 端口；SDK 方案改为 **PC 主动连设备**，现场防火墙规则可能变化（通常更有利于客户端）。 |

### 7.2 业务与运维风险

| 风险 | 等级 | 说明 |
|------|------|------|
| 已部署设备仍指向旧 HTTP URL | **中** | 升级后设备端若仍配置推送 URL，将无法再驱动软件；需 **升级清单**：改设备为 SDK 模式或关闭推送，避免双源重复识别。 |
| 在线状态语义变化 | **中** | 从「轮询过即在线」变为「TCP/SDK 连接状态」，UI 展示可能与用户习惯不一致，需说明。 |
| 回滚 | **低** | 若保留 Git 历史，可回退版本；**数据层**无结构性变更假设下，回滚成本主要在部署与设备配置。 |

### 7.3 测试与质量风险

| 风险 | 等级 | 说明 |
|------|------|------|
| 缺少无硬件自动化 | **中** | 真机依赖高；建议抽象 `IVzLprClient` 接口 + 假实现跑单元测试。 |
| 回归范围 | **中～高** | `AttendedWeighingService`、设置页、在线状态、主动抓拍按钮等 **Vzvision 设备类型全路径**需回归；**重命名**会扩大 diff 与合并冲突面。 |

---

## 8. 工作量与复杂度（量级）

- **工程规模**：以「1 个核心连接/回调服务 + 改造 Vzvision 集成 + **全库 Vzvision 前缀重命名** + 精简 WebHost + 配置/UI + 测试」计，属 **中等～偏大**；若多设备与重连策略复杂，可进一步上升。
- **日历时间**：强依赖 **真机联调** 与厂商文档细节；无硬件时只能完成 **架构与单测**，联调周期难以在文档中精确估计。

---

## 9. 建议的实施顺序（供后续开发参考）

1. 落地 **§1.1 命名规范**（`LprDeviceType.Vzvision`、`VzvisionColorType`、`IVzvisionLprService` 等），并处理 **持久化配置迁移**。  
2. 明确 **单设备 vs 多设备**、`Open` 参数默认值（端口等）与 **是否必须 `StartRealPlay`**。  
3. 实现 **SDK 适配层**（独立类库/服务），在回调中发布 `LicensePlateRecognizedMessage`（`DeviceType = Vzvision`）。  
4. 将 **`TriggerCaptureAsync`** 接到 `ForceTrigger*`，与 `ILprDevice` 行为对齐。  
5. 调整 **`IsOnline`** 与 `LprDeviceOnlineStatusService` 语义，并更新界面提示。  
6. 从 **`MinimalWebHostService`** 移除原 LprAllInOne Web 路由；验证华夏智信与地磅测试路由不受影响。  
7. 现场验证后，删除旧 Web 集成与 **`LprAllInOne*` 符号**，并更新 `openspec`/内部文档。

---

## 10. 附录：代码锚点（便于评审）

| 用途 | 路径 |
|------|------|
| 原 LprAllInOne HTTP 回调与 Comet（迁移后删除/替换） | `MaterialClient/Services/MinimalWebHostService.cs`（约 39–41、258–314、481–565、648–686 行） |
| Comet 触发与在线（迁移后为 `VzvisionLprService` 等） | `MaterialClient.Common/Services/LprAllInOne/LprAllInOneService.cs` |
| 颜色枚举（迁移后为 `VzvisionColorType` 等） | `MaterialClient.Common/Services/LprAllInOne/LprAllInOneColorType.cs` |
| Vz P/Invoke | `MaterialClient.Common/Services/Vzvision/VzvisionSdk.cs` |
| 设备解析 | `MaterialClient.Common/Services/ILprDeviceResolver.cs` |
| MessageBus 消息 | `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs` |
| 原生库复制 | `MaterialClient.Common/MaterialClient.Common.csproj`（`VzSDK` ItemGroup） |

---

## 11. 结论

- **变更范围**：集中在 **Vz 运行时接入**、**Vzvision 命名体系下的服务重写**（替代原 LprAllInOne）、**MinimalWebHostService 精简原 Web LPR 路由**、**配置项语义扩展**；`LicensePlateRecognizedMessage` 仍可沿用，但 **`DeviceType` 与颜色类型须随 §1.1 更名**。  
- **SDK 功能覆盖**：当前仓库内 `VzvisionSdk` 对原 Web 主流程（被动识别、主动触发、连接状态）**具备覆盖能力**；与旧行为 **完全等价** 须在 **`StartRealPlay`/多通道/颜色映射** 等点上经文档与实测确认，详见 **§4**。  
- **主要风险**：**线程模型**、**多连接生命周期**、**颜色/状态语义**、**现场设备从 HTTP 推送改为 SDK 后的配置迁移**；另增 **全库重命名与持久化数据迁移** 带来的回归与合并成本。  
- **与「弃用 Web」的关系**：弃用的是 **原 LprAllInOne 的 Web 集成**，不一定弃用整个 `MinimalWebHostService`（仍可能服务华夏智信与地磅测试 HTTP）。  
- **命名**：**强制**采用 **Vzvision 前缀**（§1.1），不再保留 `LprAllInOne` 作为长期公开 API 名称。

---

*本报告仅基于当前仓库快照分析；实际以产品需求与 Vz 官方 SDK 版本说明为准。*
