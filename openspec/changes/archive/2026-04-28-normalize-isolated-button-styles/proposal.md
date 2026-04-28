## Why

当前系统中同时存在“全局 class 按钮样式”和“页面内联直写按钮样式”，导致同类主操作按钮在禁用态下出现不一致的文字颜色与交互反馈。该问题已在设置页复现，且具备扩散风险，需要通过统一规范和改造收敛按钮样式入口。

## What Changes

- 统一主操作按钮样式入口：主按钮默认使用 `primary-button`（或经审批的专用 class），禁止页面内联直写 `Background/Foreground` 作为主样式来源。
- 为确需品牌差异（如 `#4A85F9`）的按钮引入专用 class（如 `brand-primary-button`），并补齐 normal/disabled 状态规则。
- 对已识别的孤立按钮进行增量收敛，优先处理 `SettingsWindow` 内主流程按钮，再处理标题栏/工具栏透明按钮的 class 统一。
- 增加可执行的样式巡检规则（检索条件 + PR 自检项），防止新增孤立按钮。

## Capabilities

### New Capabilities
- `button-style-normalization`: 定义并落地按钮样式统一约束，确保主按钮及禁用态行为在全系统一致。

### Modified Capabilities
- （无）

## Impact

- Affected code:
  - `MaterialClient/App.axaml`（统一按钮 class 规则与 disabled 规则）
  - `MaterialClient/Views/SettingsWindow.axaml`（孤立主按钮收敛）
  - 可能波及 `MaterialClient/Views/ProjectInfoWindow.axaml`、`MaterialClient/Views/PrintPreviewWindow.axaml`、`MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml` 的透明按钮统一
- Affected docs:
  - `docs/evaluation-isolated-button-styles-2026-04-27.md`
- 风险与收益：
  - 收益：禁用态一致、维护成本降低、主题升级风险可控
  - 风险：局部视觉可能变化，需要通过回归和截图比对确认
