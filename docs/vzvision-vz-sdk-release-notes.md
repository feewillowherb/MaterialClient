# 臻识 Vzvision（Vz SDK）替换原 HTTP 集成：升级说明

## 现场设备

- **停止使用** 向本机 PC 的 **HTTP 推送**（原 `POST /api/CarLicense/CallDeviceMessage`）与 **Comet 轮询**（原 `GET|POST /api/CarLicense/CallDeviceStatus`）。请在设备或上级系统中关闭相关「上报地址 / 轮询 URL」配置。
- **改为** 由 MaterialClient 通过 **`VzLPRSDK.dll`** 使用配置中的 **IP、端口（常见 80）、用户名、密码** 直连设备；识别结果经 **`LicensePlateRecognizedMessage`** 进入现有业务。

## 客户端

- **设备类型**：设置中 **`LprAllInOne` 已更名为 `Vzvision`（臻识车牌识别）**；数据库 JSON 中若曾保存数值 `1`，仍对应同一类型，无需手工改库。
- **主动抓拍**：称重流程与设置中的「测试抓拍」使用 SDK **`VzLPRClient_ForceTrigger`**（默认，非 `ForceTriggerEx`）。
- **回滚**：若需退回旧版客户端，请同时在设备侧 **恢复** 原 HTTP 推送与 Comet 地址（仅当旧版本仍提供对应接口时可行）。

## 开发与测试

- 集成测试见 `MaterialClient.Common.Tests/Tests/VzvisionIntegrationTests.cs`（默认 Skip，需真机与 DLL）。
