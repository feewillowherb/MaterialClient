## Context

当前 Avalonia 客户端使用 `App.axaml` 管理全局按钮 class（例如 `primary-button`、`secondary-button`、`transparent-button`），但仍有部分页面直接在 `Button` 上内联定义 `Background/Foreground`。这导致：

- 可用态视觉看似一致，但禁用态易回落到主题默认（如 `#ff1c1f23`）；
- DevTools 中 `AccessText` 的前景色来源不稳定（class 覆盖与主题覆盖并存）；
- 后续主题升级、样式扩展时维护成本上升。

本变更面向 UI 样式一致性，涉及系统设置页主按钮和若干透明按钮收敛策略。

## Goals / Non-Goals

**Goals:**
- 建立按钮样式的单一入口原则：主操作按钮必须通过 class 驱动样式。
- 确保主按钮禁用态文字表现可预测且一致。
- 收敛已识别孤立按钮，并提供可持续巡检规则。

**Non-Goals:**
- 不重构所有历史页面布局与结构。
- 不引入新的主题系统或替换 Fluent/Semi 主题。
- 不调整业务命令逻辑，仅处理样式来源与状态呈现。

## Decisions

1. **主按钮统一使用 class，而非页面内联颜色**
   - 决策：主按钮默认使用 `primary-button`；若需品牌蓝差异，新增专用 class（如 `brand-primary-button`）。
   - 备选方案：继续允许页面内联颜色并逐页维护 disabled 规则。
   - 选择理由：class 方式可统一 normal/disabled 行为，降低遗漏风险。

2. **禁用态同时覆盖控件层与模板文本层**
   - 决策：在 `Button.<class>:disabled` 设置 `Foreground`，必要时在 `... /template/ ContentPresenter#PART_ContentPresenter` 设置 `TextElement.Foreground`。
   - 备选方案：仅设置按钮 `Foreground`。
   - 选择理由：主题模板可能在内容呈现层覆盖文本颜色；双层覆盖更稳健。

3. **按优先级增量整改**
   - 决策：P0 先改 `SettingsWindow` 主流程按钮；P1 再改透明按钮聚集页面。
   - 备选方案：全量一次性改造。
   - 选择理由：降低回归面，便于快速验证禁用态一致性。

## Risks / Trade-offs

- **[风险] 局部视觉回归** → **缓解**：对改造页面做前后截图对比，重点校验 normal/disabled/hover。
- **[风险] 某些页面依赖内联样式的隐式行为** → **缓解**：保留特例 class，而非强制全改为 `primary-button`。
- **[权衡] 统一性 vs 灵活性** → **结论**：允许专用 class，但禁止无规则内联主按钮颜色。

## Migration Plan

1. 在 `App.axaml` 固化主按钮与（必要时）模板层禁用态规则。
2. 改造 `SettingsWindow` 的孤立主按钮为 class 驱动。
3. 逐步改造透明按钮聚集页（`ProjectInfoWindow`、`PrintPreviewWindow`、`AttendedWeighingWindow`）。
4. 增加 PR 自检项与检索脚本说明，作为常规门禁。

回滚策略：若出现视觉不可接受回归，可按文件回滚 class 替换提交，不影响业务命令逻辑。

## Open Questions

- `#4A85F9` 是否作为“品牌主按钮”长期保留？若保留，建议正式引入 `brand-primary-button`。
- 透明按钮是否需要统一 hover/pressed 反馈标准（目前部分页面仍用局部 `Button.Styles`）？
