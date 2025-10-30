# 海康威视 HCNetSDK 集成指南

本文档说明如何在本项目中集成并使用海康威视 HCNetSDK，包含本地 DLL 布局、工程设置、服务使用方式、以及单元测试验收方法。

## 目录结构与本地 DLL

请将 SDK 的 x64 动态库拷贝到以下目录（已在工程中配置自动复制到输出目录）：

- 目标：`MaterialClient.Common/Native/HCNetSDK/win64/`
- 推荐最小集（登录/抓拍通常需要）：
  - `HCNetSDK.dll`
  - `HCCore.dll`
  - `PlayCtrl.dll`
  - `hpr.dll`
  - `hlog.dll`
  - `zlib1.dll`
  - `libcrypto-1_1-x64.dll`
  - `libssl-1_1-x64.dll`
- 可选（视功能而定）：`GdiPlus.dll`, `SuperRender.dll`, `YUVProcess.dll`, `AudioRender.dll`, `NPQos.dll`, `HXVA.dll`, `MP_Render.dll`

源 SDK 目录示例：
- `E:\SDK\CH-HCNetSDKV6.1.9.48_build20230410_win64\库文件\`

> 说明：本仓库已在以下项目中启用 x64 RID 与本地 DLL 复制规则：
> - `MaterialClient.Common`
> - `MaterialClient`（UI）
> - `MaterialClient.Common.UnitTest`

## 工程设置（已配置）

- `RuntimeIdentifier` 统一为 `win-x64`，确保加载 x64 原生库。
- 三个工程均通过 `None Include="..\\MaterialClient.Common\\Native\\HCNetSDK\\win64\\*.dll" CopyToOutputDirectory="PreserveNewest"`（或等效路径）复制 DLL 至输出目录。

## 服务与使用

服务位置：`MaterialClient.Common/Services/Hikvision/HikvisionService.cs`

已实现能力：
- 检查设备是否在线：`IsOnline(config)`
- 指定通道拍照：`CaptureJpeg(config, channel, saveFullPath, quality)`
- 管理多设备：`AddOrUpdateDevice(config)`
- 预留实时视频流入口：`TryOpenRealStream(config, channel)`（后续可扩展为回调/句柄）

示例（C#）：
```csharp
var service = new HikvisionService();
var config = new HikvisionDeviceConfig
{
    Ip = "192.168.1.2",
    Username = "admin",
    Password = "admin",
    Port = 8050,
    StreamType = 0,
    Channels = new[] { 1 }
};

service.AddOrUpdateDevice(config);
bool online = service.IsOnline(config);

var path = Path.Combine(AppContext.BaseDirectory, "captures", $"hik_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
bool ok = service.CaptureJpeg(config, 1, path, quality: 90);
```

## 单元测试验收

测试文件：`MaterialClient.Common.UnitTest/UnitTest1.cs`
- 用实际参数连接摄像头。
- 执行一次抓拍，保存到 `captures/`。
- 断言：文件存在且大小大于 0。

运行命令：
```bash
# 在仓库根目录
 dotnet test .\MaterialClient.sln -c Debug
```

常见问题：
- “找不到 HCNetSDK.dll”/依赖缺失：确认上述最小集 DLL 已放置到 `Native/HCNetSDK/win64/`。
- 登录失败：检查 IP/端口、账号口令与网络连通性（VPN/防火墙/子网）。
- 抓拍失败但能登录：确认通道号是否有效、权限是否允许、设备是否有图像源。

## 实时视频流（后续拓展）

若需实时预览或码流回调：
- 使用 `NET_DVR_RealPlay_V40` 打开实时流，获取预览句柄。
- 选择回调模式（裸码流）或结合 `PlayCtrl.dll` 渲染。
- 提供关闭句柄与释放资源的方法。

如需我完善该接口，请确认：
- 期望输出（窗口渲染/码流回调/文件落盘）。
- UI 集成（Avalonia）或后台服务化。

## 安全与合规

- 不要提交账号、口令与证书到仓库。
- 使用环境变量或 Secret 管理敏感信息。
- DLL 为第三方二进制，请遵循供应商许可协议；必要时可使用 Git LFS 追踪大文件。

## 提交与 CI 建议

- 提交本地 DLL 后，可在 PR 中附上：
  - 变更说明（接入 HCNetSDK、拍照单测）。
  - 如何在本地运行测试。
- 建议在 GitHub Actions 中增加 `dotnet build`/`dotnet test`，并添加分支保护。

---
维护者：请在变更 SDK 版本或接口时同步更新本文档与示例。
