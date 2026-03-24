# Design: Vzvision SDK 替换 LprAllInOne Web

## Context

- **当前状态**：Vz 一体机以 `LprDeviceType.LprAllInOne` 接入，`MinimalWebHostService` 提供 `CallDeviceMessage`（识别 JSON 上行）与 `CallDeviceStatus`（Comet 心跳 + 手动触发）；`LprAllInOneService` 维护触发标志与「轮询即在线」。业务通过 `LicensePlateRecognizedMessage` 消费结果。
- **已有资产**：`VzvisionSdk`（`internal`）已声明 `VzLPRClient_*` 与 `TH_PlateResult`；`MaterialClient.Common` 已复制 `VzSDK\**` 至输出目录。
- **约束**：桌面应用 `win-x64`；ABP + Autofac DI；ReactiveUI MessageBus；评估文档要求 **Vzvision 前缀命名** 与弃用长期 `LprAllInOne` 公开 API。

## Goals / Non-Goals

**Goals:**

- 用 **SDK 长连接 + 回调** 替代设备侧对本机 HTTP 的依赖；主动抓拍用 **`ForceTrigger*`** 替代 Comet 响应。
- 发布与现网一致的 **`LicensePlateRecognizedMessage`**（`DeviceType` 为新的 `Vzvision` 枚举值）。
- 完成 **类型/命名空间重命名** 与 **设置 JSON 向后兼容**（或一次性迁移策略）。
- 从 `MinimalWebHostService` **移除** 仅服务原 LprAllInOne 的路由与 DTO，**不破坏** 华夏智信与地磅测试路由。

**Non-Goals:**

- 不改变海康、华夏智信既有集成契约（除共用宿主与全局设置结构中的枚举值）。
- **不实现** **`VzLPRClient_StartRealPlay`** 及任何依赖 **实时视频 HWND 预览/浏览** 的能力；本变更仅保留识别回调与业务所需 API（`Open`、`SetPlateInfoCallBack`、`ForceTrigger` 等），**不**做机内画面预览窗口。可选 **`VzClient_SetCommonResultCallBack`** 仍按 Decision 7 评估，与预览无关。

## Decisions

1. **设备类型枚举最终名**  
   - **决策**：采用单一最终名（建议 **`Vzvision`**），删除 `LprAllInOne`。  
   - **理由**：与评估文档「强制 Vzvision 前缀」一致；减少双枚举别名。  
   - **备选**：保留 `[Obsolete]` 别名一个版本周期 — 若团队希望渐进迁移可追加，但评估文档倾向不长期保留。

2. **服务边界**  
   - **决策**：新增 **`IVzvisionLprService`**（实现 `ILprDevice`）+ 内部 **`VzvisionLprConnectionManager`**（或同类）负责 `Setup`/`Open`/`SetPlateInfoCallBack`/`Close`/`Cleanup` 与按 IP/句柄映射。  
   - **理由**：分离「业务触发/在线查询」与「原生回调与线程」。

3. **回调线程 → MessageBus**  
   - **决策**：在非托管回调中 **仅做最小工作**（解析 plate、入队）；切换到 **UI 或专用同步上下文**（如 `Dispatcher.UIThread.Post` 或 channel）再 `MessageBus.SendMessage`。  
   - **理由**：避免与 Avalonia/服务定位器线程假设冲突（评估文档高风险项）。

4. **在线状态**  
   - **决策**：以 **`VzLPRClient_IsConnected`** 为主；可选叠加「最近一次识别时间」超时（业务配置）。  
   - **理由**：与原「Comet 2 分钟内即在线」语义不同，需在 UI 或发行说明中披露。

5. **配置字段**  
   - **决策**：复用 `LicensePlateRecognitionConfig` 的 `Port`/`UserName`/`Password` 字段服务于 **`VzLPRClient_Open`**（当 `LprDeviceType == Vzvision`），与海康在 **设备类型维度**互斥展示。  
   - **理由**：避免重复字段；需更新 XML 注释与设置界面可见性规则。

6. **MinimalWebHostService**  
   - **决策**：删除 LprAllInOne 相关 `MapPost`/`MapMethods` 及私有 record；根路径 `endpoints` 数组同步删减。  
   - **理由**：设备不再调用 PC HTTP；降低攻击面与维护成本。

7. **通用识别结果回调 `VZ_COMMON_RESULT_CALLBACK`（`VzLPRClientSDK.h` 约 2829–2840）**  
   - **依据**：SDK 注释规定 `type` 表示结果类别：**0** 车道监控、**1** 三地车牌识别、**2** x3 相机识别、**3** 车牌识别（支持二值化图像输出）、**4** S5L 人脸抓拍；`pResultInfo` 为 **JSON** 字符串；`len` 为长度；`imgs` / `count` 为图片信息；`pUserData` 为用户数据。具体内容以协议文档为准。  
   - **决策**：本变更以 **`VzLPRClient_SetPlateInfoCallBack` + `TH_PlateResult`** 为主路径实现车牌业务；**可选** 注册 **`VzClient_SetCommonResultCallBack`**，在实现层按 `type` 过滤（例如 **`type == 3`** 时解析 JSON 与图像），与 MessageBus 模型对齐。**不**将「多路/多类型结果」直接等同于「多个 `Open` 句柄」——多句柄仍以多设备/多连接需求为准，与 `type` 分路正交。  
   - **`StartRealPlay`（实时浏览）**：**明确不在本变更范围内**——实现 **不得** 调用 **`VzLPRClient_StartRealPlay`** 用于视频预览/浏览；识别与触发不依赖实时画面窗口。若未来单独产品需求要做预览，另起变更。

8. **主动抓拍 API 选择**  
   - **决策**：业务默认调用 **`VzLPRClient_ForceTrigger(handle)`**（与 `VzvisionSdk` 中 `public static extern int VzLPRClient_ForceTrigger(int handle);` 一致），**不**默认使用 **`VzLPRClient_ForceTriggerEx`**。若后续协议或设备要求 TCP 扩展触发，再单独启用 `ForceTriggerEx` 并文档化。

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| 设备固件强制要求预览管道才能出识别 | 本变更假设 **无需** `StartRealPlay`；若实机异常，再评估设备文档或例外补丁（超出当前 Non-Goals） |
| `nColor` 与旧 HTTP `colorType` 不一致 | 建立显式映射表 + 实车抽检；单元测试覆盖映射 |
| 多设备句柄泄漏 | `IDisposable` 或应用关闭路径统一 `Close`/`Cleanup`；配置变更时重连 |
| 持久化枚举名断裂 | JSON 自定义转换器或启动时迁移 `LprAllInOne` → `Vzvision` |
| 现场仍配置设备推送 URL | 发布说明与升级清单，明确关闭设备侧 HTTP 推送 |

## Migration Plan

1. 实现 SDK 服务与重命名（特性开关可选，若团队需要分阶段）。  
2. 迁移设置与 `appsettings` 枚举绑定；验证旧数据库 JSON 加载。  
3. 移除 Web 路由；回归华夏智信、地磅测试、有人值守称重。  
4. 现场试点：关设备推送、仅 SDK；收集日志与识别率。  
5. **回滚**：还原发布包与设备若仍保留 Web 配置则需旧版客户端 — 数据层无强制 schema 变更前提下以版本回退为主。

## Open Questions

（原列问题已按 `VzLPRClientSDK.h` 与默认 API 在 **Decisions 7、8** 中收敛；若联调发现与设备固件不一致，再更新本变更与实现。）
