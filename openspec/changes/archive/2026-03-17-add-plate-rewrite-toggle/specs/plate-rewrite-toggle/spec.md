## ADDED Requirements

### Requirement: WeighingConfiguration 包含车牌重写开关
`WeighingConfiguration` SHALL 包含 `EnablePlateRewrite` 属性（`bool`），默认值为 `true`。该值 SHALL 通过 `SettingsEntity.WeighingConfiguration` 持久化到数据库。

#### Scenario: 默认值保持向后兼容
- **WHEN** 用户未修改过此设置
- **THEN** `EnablePlateRewrite` 的值 SHALL 为 `true`

#### Scenario: 设置值持久化
- **WHEN** 用户将 `EnablePlateRewrite` 设为 `false` 并保存设置
- **THEN** 重新加载设置后 `EnablePlateRewrite` 的值 SHALL 为 `false`

### Requirement: 设置界面展示车牌重写开关
`SettingsWindow` 的称重参数区域 SHALL 显示「启用车牌重写」`ToggleSwitch` 控件，位于现有称重参数之后。控件 SHALL 绑定到 ViewModel 的 `EnablePlateRewrite` 属性，并在保存时写入 `WeighingConfiguration`。

#### Scenario: 界面加载时反映当前设置
- **WHEN** 用户打开设置窗口
- **THEN** `ToggleSwitch` 的选中状态 SHALL 与 `WeighingConfiguration.EnablePlateRewrite` 一致

#### Scenario: 用户切换开关并保存
- **WHEN** 用户切换 `ToggleSwitch` 并点击保存
- **THEN** `WeighingConfiguration.EnablePlateRewrite` SHALL 被更新为新值

### Requirement: 下磅时根据开关决定是否重写车牌
`AttendedWeighingService.TryReWritePlateNumberAsync` SHALL 在执行车牌号重写逻辑前检查 `WeighingConfiguration.EnablePlateRewrite`。

#### Scenario: 开关启用时执行车牌重写
- **WHEN** `EnablePlateRewrite` 为 `true` 且车辆下磅
- **THEN** 系统 SHALL 使用识别频率最高的车牌号覆盖称重记录的车牌号（现有行为）

#### Scenario: 开关关闭时跳过车牌重写
- **WHEN** `EnablePlateRewrite` 为 `false` 且车辆下磅
- **THEN** 系统 SHALL 跳过车牌号重写，保留上磅时的原始车牌号

#### Scenario: 关闭时不影响 DeliveryType 重写和匹配事件
- **WHEN** `EnablePlateRewrite` 为 `false` 且车辆下磅
- **THEN** 系统 SHALL 仍执行 DeliveryType 重写逻辑，且 SHALL 发布 `TryMatchEvent`
