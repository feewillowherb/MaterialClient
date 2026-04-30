## 1. 配置层变更

- [x] 1.1 在 `SystemSettings` 类中新增 `EnableTriggerLprCapture` 布尔属性，默认值为 `false`

## 2. 业务逻辑变更

- [x] 2.1 将 `TriggerVzvisionCaptureForAllAsync` 重命名为 `TriggerLprCaptureForAllAsync`（同时更新三个调用方：`TriggerCaptureOnWaitingForStabilityAsync`、`TriggerCaptureOnWeightStabilizedAsync`、`TriggerCaptureOnOffScaleAsync`）
- [x] 2.2 从 `TriggerLprCaptureForAllAsync` 方法中移除硬编码限制代码（`_logger.LogWarning(...)` 和 `return;` 两行）
- [x] 2.3 在 `TriggerLprCaptureForAllAsync` 方法体最前面添加 `EnableTriggerLprCapture` 配置守卫：读取设置，若为 `false` 则记录 `LogInformation` 并返回

## 3. UI 变更

- [x] 3.1 在设置窗口 ViewModel 中暴露 `EnableTriggerLprCapture` 绑定属性
- [x] 3.2 在 `SettingsWindow.axaml` 的"车牌识别设置"区域（`LprDeviceType` 下拉框下方）添加"启用 LPR 主动抓拍"复选框，绑定到新属性
