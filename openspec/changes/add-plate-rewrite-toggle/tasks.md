## 1. Configuration

- [x] 1.1 在 `WeighingConfiguration` 中新增 `EnablePlateRewrite` 属性（`bool`，默认 `true`），更新 `IsValid()` 方法（无需额外验证，bool 始终有效）

## 2. Settings UI

- [x] 2.1 在 `SettingsWindowViewModel` 中新增 `[Reactive] private bool _enablePlateRewrite` 属性
- [x] 2.2 在 `SettingsWindowViewModel.LoadSettingsAsync` 中从 `WeighingConfiguration` 加载 `EnablePlateRewrite`
- [x] 2.3 在 `SettingsWindowViewModel.SaveSettingsAsync` 中将 `EnablePlateRewrite` 写回 `WeighingConfiguration`
- [x] 2.4 在 `SettingsWindow.axaml` 称重参数 Grid 中新增一行 `ToggleSwitch`，标签为「启用车牌重写」，绑定 `EnablePlateRewrite`

## 3. Service Logic

- [x] 3.1 修改 `AttendedWeighingService.TryReWritePlateNumberAsync`：方法开头读取 `WeighingConfiguration.EnablePlateRewrite`，若为 `false` 则跳过车牌号重写部分，但保留 DeliveryType 重写和 `TryMatchEvent` 发布
