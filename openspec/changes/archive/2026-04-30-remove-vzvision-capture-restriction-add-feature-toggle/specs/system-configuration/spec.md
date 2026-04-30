## ADDED Requirements

### Requirement: LPR 主动抓拍启用配置

系统 MUST 在 `SystemSettings` 类中提供 `EnableTriggerLprCapture` 布尔属性，用于控制是否启用通用 LPR 主动抓拍功能。该属性 MUST 通过 JSON 序列化持久化到配置存储中，MUST 在设置窗口的"车牌识别设置"区域提供对应的 UI 控件供用户修改。

#### Scenario: 属性默认值

- **WHEN** 创建新的 `SystemSettings` 实例且未显式设置 `EnableTriggerLprCapture`
- **THEN** `EnableTriggerLprCapture` MUST 为 `false`

#### Scenario: 通过设置窗口修改

- **WHEN** 用户在设置窗口的"车牌识别设置"区域勾选"启用 LPR 主动抓拍"复选框
- **AND** 用户点击"保存"
- **THEN** 系统 MUST 将 `EnableTriggerLprCapture = true` 持久化到配置存储

#### Scenario: 通过设置窗口禁用

- **WHEN** 用户在设置窗口的"车牌识别设置"区域取消"启用 LPR 主动抓拍"复选框
- **AND** 用户点击"保存"
- **THEN** 系统 MUST 将 `EnableTriggerLprCapture = false` 持久化到配置存储
