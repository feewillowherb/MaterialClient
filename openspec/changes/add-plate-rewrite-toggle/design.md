## Context

`AttendedWeighingService` 在车辆下磅（`ResetWeighingCycleAsync`）时调用 `TryReWritePlateNumberAsync`，用称重周期内识别频率最高的车牌覆盖上磅时记录的车牌号。该行为当前为硬编码，不可配置。

称重相关参数存储在 `WeighingConfiguration`（POCO），通过 `SettingsEntity.WeighingConfiguration` 持久化到 SQLite，ViewModel 通过 `ISettingsService` 读写。

`SettingsWindow.axaml` 的称重参数区域使用 `Grid` 布局（`ColumnDefinitions="Auto,*"`，行内 `TextBlock` + `TextBox`），当前有 6 个参数。

## Goals / Non-Goals

**Goals:**
- 在 `WeighingConfiguration` 中新增 `EnablePlateRewrite` 开关
- 在设置界面提供 UI 控件，让用户可切换此行为
- `TryReWritePlateNumberAsync` 根据开关决定是否执行车牌重写

**Non-Goals:**
- 不修改 `TryReWritePlateNumberAsync` 的重写算法本身
- 不影响 DeliveryType 的重写逻辑（始终执行）
- 不改变 `TryMatchEvent` 的发布逻辑

## Decisions

### Decision 1：配置位置

**方案**: 在 `WeighingConfiguration` 中新增 `EnablePlateRewrite` 属性（`bool`，默认 `true`）。

**理由**: 车牌重写是称重流程的一部分，放在 `WeighingConfiguration` 与其他称重参数一起管理，语义清晰。默认 `true` 保持向后兼容。

**替代方案**: 放在 `SystemSettings` —— 但 `SystemSettings` 已用于系统级配置（URL、打印机、模式），称重行为参数应归属 `WeighingConfiguration`。

### Decision 2：UI 控件

**方案**: 在设置界面称重参数 Grid 中新增一行，使用 `ToggleSwitch`（Avalonia 内置），标签为「启用车牌重写」。

**理由**: `ToggleSwitch` 语义明确（开/关），比 `CheckBox` 更适合功能启用类设置。放在现有称重参数 Grid 的最后一行，保持布局一致。

### Decision 3：关闭时的行为

**方案**: 关闭时 `TryReWritePlateNumberAsync` 跳过**车牌号重写**部分，但仍执行 DeliveryType 重写和 `TryMatchEvent` 发布。

**理由**: 车牌重写和 DeliveryType 重写是两个独立逻辑，开关仅控制车牌部分。匹配事件始终需要触发，否则运单无法自动匹配。

## Risks / Trade-offs

- **[风险] 关闭后车牌可能不准确** → 这是用户主动选择，UI 可考虑 tooltip 提示
- **[取舍] 默认开启** → 保持向后兼容，已有用户不受影响
