## Why

称重服务在下磅（车辆离开磅台）时会调用 `TryReWritePlateNumberAsync`，用称重周期内识别频率最高的车牌号覆盖上磅时记录的车牌。这在大多数场景下能提升车牌准确率，但部分用户反馈希望保留上磅时的原始车牌，不做覆盖。当前该行为是硬编码的，无法通过设置关闭。

## What Changes

- 在 `WeighingConfiguration` 中新增 `EnablePlateRewrite` 布尔属性（默认 `true`，保持现有行为）
- 在 `SettingsWindow.axaml` 的称重参数区域新增「启用车牌重写」开关（CheckBox / ToggleSwitch）
- 在 `AttendedWeighingService.TryReWritePlateNumberAsync` 中读取该设置，关闭时跳过车牌重写逻辑

## Capabilities

### New Capabilities
- `plate-rewrite-toggle`: 称重参数中的车牌重写开关，控制下磅时是否用最佳车牌覆盖原始车牌

### Modified Capabilities

## Impact

- `MaterialClient.Common/Configuration/WeighingConfiguration.cs` — 新增 `EnablePlateRewrite` 属性
- `MaterialClient.Common/Services/AttendedWeighingService.cs` — `TryReWritePlateNumberAsync` 增加开关判断
- `MaterialClient/Views/SettingsWindow.axaml` — 新增 UI 开关
- `MaterialClient/ViewModels/SettingsWindowViewModel.cs` — 新增绑定属性和持久化逻辑
