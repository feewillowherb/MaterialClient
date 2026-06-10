# OpenSpec 提案：海康威视采集设备迁移

## 提案摘要

迁移和重构海康威视（Hikvision）采集设备集成到目标代码库。**范围内**：仅限手动抓拍（主动抓拍）和被动抓拍（被动抓拍）。**范围外**：实时预览。两种抓拍类型都通过单个监听通道接收结果数据（图像、车牌信息）：`NET_DVR_StartListen_V30` 回调。

---

## 1. 问题 / 背景

- 现有的海康威视采集逻辑位于 `CaptureDevice.cs` (BLL)，使用 CHCNetSDK（原生 SDK）和可能使用的 AlarmCSharpDemo。
- 当前代码存在正确性和生命周期问题：监听句柄从未使用 `NET_DVR_StopListen_V30` 停止；回调委托可能被垃圾回收（非托管 SDK 仅保存函数指针）。
- 配置混淆了"监听本地 IP/端口"与"设备 IP/端口"；错误处理不一致。
- 迁移目标可能与 .NET Web 应用程序一起运行；监听端口必须不与 Web 应用端口冲突。

---

## 2. 目标

1. **保持行为**：手动抓拍（应用触发的抓拍）和被动抓拍（设备推送的抓拍）使用相同的接收路径。
2. **修复正确性**：正确的停止序列（`NET_DVR_StopListen_V30` 用于监听句柄）；为监听生命周期固定回调委托 (GCHandle)。
3. **改进结构**：集中化 P/Invoke 和 SDK 类型；清晰的配置（监听 vs 设备）；一致的错误处理和日志记录。
4. **记录约束**：专用监听端口（不与 Web 应用共享）；使用 NET_DVR_StartListen_V30（当前 SDK 中没有 V40 Listen）。

---

## 3. 功能需求

### 3.1 手动抓拍（主动抓拍）

- 应用程序可以在设备上触发单次抓拍。
- 流程：如果尚未初始化则初始化 SDK；如果尚未开始则开始监听（参见 3.3）；登录设备；使用有效的 `NET_DVR_SNAPCFG` 调用 `NET_DVR_ContinuousShoot`；结果（图像、车牌信息）通过与被动抓拍相同的 `NET_DVR_StartListen_V30` 回调传递。
- 没有实时预览；没有 `NET_DVR_RealPlay_V40` 或 `NET_DVR_StopRealPlay`。

### 3.2 被动抓拍（被动抓拍）

- 设备在不触发应用程序的情况下将抓拍数据推送到客户端。
- 流程：客户端已通过 `NET_DVR_StartListen_V30` 启动监听；设备发送告警/抓拍；回调 `MSGCallBack` 接收 COMM_UPLOAD_PLATE_RESULT (0x2800) 或 COMM_ITS_PLATE_RESULT (0x3050)；应用程序解析 `NET_DVR_PLATE_RESULT` 或 `NET_ITS_PLATE_RESULT` 并根据需要持久化图像 / 车牌信息。

### 3.3 统一接收路径

- **一个监听，一个回调**：手动和被动抓拍结果都通过 `NET_DVR_StartListen_V30(sLocalIP, wLocalPort, DataCallback, pUserData)` 接收。回调必须处理：
  - `COMM_UPLOAD_PLATE_RESULT` → 解析 `NET_DVR_PLATE_RESULT`（例如 `dwFarCarPicLen`、`pBuffer5`、`byAbsTime`、`struPlateInfo`、`struVehicleInfo`）。
  - `COMM_ITS_PLATE_RESULT` → 解析 `NET_ITS_PLATE_RESULT`（例如 `struPicInfo[]`、`struSnapFirstPicTime`、`struPlateInfo`、`struVehicleInfo`、`dwPicNum`）。
- 回调在 SDK 线程上运行；实现不得阻塞（编组到应用程序线程或排队处理）。

### 3.4 生命周期

- **启动**：`NET_DVR_Init`；然后使用固定的回调委托 (GCHandle) `NET_DVR_StartListen_V30`。存储监听句柄。
- **停止**：当监听已启动时调用 `NET_DVR_StopListen_V30(listenHandle)`；然后如果已登录手动抓拍则 `NET_DVR_Logout(userId)`；然后 `NET_DVR_Cleanup`。**不要**调用 `NET_DVR_StopRealPlay`（预览超出范围）。
- **手动抓拍会话**：当用户触发手动抓拍时登录 (`NET_DVR_Login_V40`)；在 `NET_DVR_ContinuousShoot` 之后，可选地注销以释放设备会话。监听保持活动以接收结果。

---

## 4. 非功能需求

### 4.1 正确性（关键）

- **回调生命周期**：在整个 `NET_DVR_StartListen_V30` 和 `NET_DVR_StopListen_V30` 期间使用 `GCHandle` 固定 `MSGCallBack` 委托。在代码中记录：*"关键：使用 GCHandle 防止委托被垃圾回收。非托管 SDK 仅存储函数指针；GC 不知道它仍在使用。"*

- **停止序列**：当监听已启动时始终调用 `NET_DVR_StopListen_V30(listenHandle)`；从不将监听句柄传递给 `NET_DVR_StopRealPlay`。

### 4.2 配置

- **监听**：用于 `NET_DVR_StartListen_V30` 的客户端监听端点的显式设置，例如 `ListenLocalIP`、`ListenLocalPort`。此端口必须是专用的（不供 .NET Web 应用程序使用）。
- **设备**：用于登录和手动抓拍的设备连接的单独设置，例如设备 IP、端口、用户名、密码（VideoIP、VideoPort、VideoUserName、VideoPwd 或等效项）。

### 4.3 端口使用

- 监听端口 (`ListenLocalPort`) 必须不与 Web 应用程序共享。一个进程不能两次绑定同一端口；同一台机器上的不同进程不能绑定同一端口。为海康威视监听使用专用端口。

### 4.4 错误处理和日志记录

- 在每个可能失败的 SDK 调用之后，调用 `NET_DVR_GetLastError`（并可选地映射到字符串）。传播或记录错误；避免空的 catch 块。

### 4.5 编码

- 对车牌文本使用一致的编码（例如中文使用 GBK）。集中在一个助手中以避免漂移。

---

## 5. 技术范围

### 5.1 使用的 API（海康威视 SDK）

| API | 用途 |
|-----|-----|
| `NET_DVR_Init` | SDK 初始化 |
| `NET_DVR_StartListen_V30` | 启动监听；通过回调接收抓拍数据（手动 + 被动） |
| `NET_DVR_StopListen_V30` | 停止监听；必须在停止时调用 |
| `NET_DVR_Login_V40` | 用于手动抓拍的设备登录 |
| `NET_DVR_Logout` | 手动抓拍会话后注销 |
| `NET_DVR_ContinuousShoot` | 触发手动抓拍 |
| `NET_DVR_Cleanup` | SDK 清理 |
| `NET_DVR_GetLastError` | 失败调用后的错误代码 |

**不要使用**：`NET_DVR_RealPlay_V40`、`NET_DVR_StopRealPlay`、`NET_DVR_PREVIEWINFO`（预览超出范围）。

### 5.2 回调和类型

- **MSGCallBack**：`(LONG lCommand, NET_DVR_ALARMER *pAlarmer, char *pAlarmInfo, DWORD dwBufLen, void* pUser)`。
- **结构 (P/Invoke)**：`NET_DVR_USER_LOGIN_INFO`、`NET_DVR_DEVICEINFO_V40`、`NET_DVR_SNAPCFG`、`NET_DVR_JPEGPARA`、`NET_DVR_ALARMER`、`NET_DVR_PLATE_RESULT`、`NET_ITS_PLATE_RESULT`。布局和编组必须与 HCNetSDK.h (CH-HCNetSDKV6.1.9.48) 匹配。集中在一个模块中（例如 `HikvisionSdk.cs` 或专用适配器程序集）。

### 5.3 监听 API 版本

- 仅使用 **NET_DVR_StartListen_V30** 和 **NET_DVR_StopListen_V30**。当前 SDK 中没有 `NET_DVR_StartListen_V40`。

---

## 6. 范围外

- 实时预览；任何 RealPlay/Preview API。
- 其他设备品牌（例如 臻识）；此提案仅限海康威视。
- 更改手动与被动抓拍的语义；仅迁移、正确性修复和重构在范围内。

---

## 7. 验收标准

1. **手动抓拍**：用户可以触发抓拍；结果（图像、车牌信息）通过相同的监听回调接收并正确处理。
2. **被动抓拍**：设备推送的抓拍通过监听回调接收并正确处理（COMM_UPLOAD_PLATE_RESULT 和 COMM_ITS_PLATE_RESULT）。
3. **停止**：停止时，当监听已启动时调用 `NET_DVR_StopListen_V30(listenHandle)`；监听句柄不使用 `NET_DVR_StopRealPlay`。
4. **回调生命周期**：MSGCallBack 委托在监听会话期间被固定 (GCHandle)；SDK 调用回调时没有 GC 相关的崩溃。
5. **配置**：监听本地 IP/端口和设备 IP/端口（以及凭据）清晰分离；监听端口记录为专用（不与 Web 应用共享）。
6. **错误**：失败的 SDK 调用后进行错误检索和日志记录或传播。
7. **P/Invoke**：海康威视 SDK 调用和结构集中化；布局与 SDK 头文件匹配。

---

## 8. 参考

- 源代码：`Fdsoft.Weight.GovClient/BLL/CaptureDevice.cs`
- SDK 头文件：HCNetSDK.h (CH-HCNetSDKV6.1.9.48)
- 评估文档：`agents/海康威视抓拍机迁移评估文档.md`
