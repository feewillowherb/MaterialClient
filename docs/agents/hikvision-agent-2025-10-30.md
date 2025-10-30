# Agent 会话归档（海康威视 HCNetSDK 接入）

日期：2025-10-30

## 变更摘要
- 新增 `HikvisionService`：登录/在线检查/抓拍；P/Invoke 封装 `NET_DVR_Init/Login_V40/CaptureJPEGPicture/Logout`，暴露错误码获取。
- 工程配置：三项目设为 `win-x64`，复制 `Native/HCNetSDK/win64/*.dll` 和 `HCNetSDKCom` 目录到输出。
- 单测：`HikvisionServiceTest.Capture_Jpeg_From_Hikvision`，尝试多通道候选；失败打印 HCNetSDK 错误码。现已标记为手动测试（Skip）。
- 文档：`docs/hikvision-integration.md`（集成指南）；补充组件化 SDK（107~114）必须携带 `HCNetSDKCom`。

## 关键路径
- 服务：`MaterialClient.Common/Services/Hikvision/HikvisionService.cs`
- SDK 本地目录：`MaterialClient.Common/Native/HCNetSDK/win64/`
- 单测：`MaterialClient.Common.UnitTest/HikvisionServiceTest.cs`
- 文档：`docs/hikvision-integration.md`

## 手动测试指引
1. 将 `HCNetSDK.dll`、`HCCore.dll`、`PlayCtrl.dll`、`hpr.dll`、`hlog.dll`、`zlib1.dll`、`libcrypto-1_1-x64.dll`、`libssl-1_1-x64.dll` 以及 `HCNetSDKCom` 目录拷贝到上述本地目录。
2. 构建：`dotnet build -c Debug`
3. 在 IDE 中运行被 Skip 的 `Capture_Jpeg_From_Hikvision`（或临时移除 Skip）。
4. 若失败，记录 `HCNetSDK error=xxx` 并排查：依赖、位数、权限、通道号。

## 已知问题
- 错误码 107~114：需将 `HCNetSDKCom` 与 `HCNetSDK.dll` 同级且文件夹名不可修改。
- 仅抓拍无需 `StreamType`；实时流另行实现。

## 后续建议
- 需要实时预览时，扩展 `TryOpenRealStream` 使用 `NET_DVR_RealPlay_V40`，支持回调/渲染与关闭流程。
