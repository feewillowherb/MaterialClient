# Evaluation: 孤立按钮样式分析（2026-04-27）

## 背景

本次排查来自一个真实问题：按钮在可点击时文本为白色，但禁用后文本显示为主题默认深色（`#ff1c1f23`），与预期不一致。  
根因不是“某个按钮异常”，而是项目内存在“局部直写样式按钮（孤立样式）”与“全局 class 样式按钮”并存，导致禁用态行为不一致。

---

## 现象与结论

- 使用 `Classes="primary-button"` 的按钮，会命中 `App.axaml` 中的 `Button.primary-button`（以及其 `:disabled` 扩展规则）。
- 未使用 `primary-button`，而是在控件上直接写 `Background/Foreground` 的按钮，禁用时会被主题 `Button:disabled` 规则接管文本呈现。
- 结果是：同样看起来“蓝底白字”的按钮，在禁用态会出现不同文字颜色，造成视觉不一致。

---

## 已识别的孤立按钮样式（当前代码）

以下按钮未复用 `primary-button`，而是本地硬编码了蓝色按钮外观：

1. `MaterialClient/Views/SettingsWindow.axaml`  
   - 摄像头设置区「增加」按钮（`AddCameraCommand`）
   - 车牌识别设置区「增加」按钮（`AddLicensePlateRecognitionCommand`）
   - 底部「确认保存」按钮（`SaveCommand`）

这些按钮都采用了类似组合：

- `Background="#4A85F9"`
- `Foreground="White"`
- `FontWeight="Bold"`

但没有对应 `Classes="primary-button"`，因此不会自动继承 `primary-button:disabled` 的定制行为。

---

## 技术原因（为什么会这样）

1. **样式匹配范围不同**  
   `Button.primary-button:disabled` 只匹配带 `primary-button` class 的按钮。

2. **禁用态伪类触发后，主题规则参与竞争**  
   未绑定 class 的按钮，进入 `:disabled` 后，文本颜色通常来自 Fluent/Semi 主题默认规则（如 `#ff1c1f23`）。

3. **AccessText 颜色来源于按钮 Foreground 链路**  
   按钮内容最终由模板中的 `ContentPresenter`/`AccessText` 渲染。  
   如果没有对“该按钮类型 + disabled”做显式覆盖，主题会对最终显示色产生主导影响。

---

## 风险评估

- **视觉一致性风险**：同为主操作按钮，禁用态视觉不统一。
- **维护风险**：每个页面局部写颜色，后续改主题或做统一优化成本高。
- **回归风险**：新增页面继续复制“蓝底白字直写按钮”，问题会扩散。

---

## 建议整改策略

### 方案 A（推荐）：统一走 class 样式

- 主操作按钮统一使用 `Classes="primary-button"`。
- 尽量移除本地 `Background/Foreground` 直写（除非是业务特例）。
- 在 `App.axaml` 统一维护：
  - `Button.primary-button`
  - `Button.primary-button:disabled`
  - 必要时补充模板层 `ContentPresenter` 文本前景覆盖。

优点：行为统一、维护成本低、后续主题升级可控。

### 方案 B：保留特例，但必须建立专用 class

- 对确实需要不同蓝色（如 `#4A85F9`）的按钮，定义如 `brand-primary-button`。
- 在全局样式里补齐其 normal/disabled 状态，而不是在页面内直写颜色。

优点：允许差异化视觉，同时保持状态行为可预测。

---

## 建议的排查清单（后续持续使用）

- 是否存在 `Button` 直接写 `Background` + `Foreground`，但没有 `Classes`？
- 是否存在主按钮外观但未命中 `primary-button`？
- 是否为每种按钮 class 定义了 `:disabled` 规则？
- DevTools 中 `AccessText` 的 `Foreground` 来源是否符合预期（项目样式 vs 主题默认）？

---

## 本次结论摘要

“禁用后字体变成 `#ff1c1f23`”并非随机问题，而是**孤立按钮样式未接入全局主按钮 class 体系**导致。  
只要继续存在“局部直写蓝底白字按钮”，就会持续出现同类问题。应优先推进按钮样式收敛到 class 体系。

---

## 可执行整改清单（按文件）

> 目标：先解决“主按钮禁用态不一致”，再治理其它孤立按钮。

### P0（立即处理，直接影响主流程一致性）

1. `MaterialClient/Views/SettingsWindow.axaml`
   - [ ] 摄像头设置「增加」按钮：改为 `Classes="primary-button"`（或 `brand-primary-button`），移除本地 `Background/Foreground`。
   - [ ] 车牌识别设置「增加」按钮：同上。
   - [ ] 「确认保存」按钮：同上；若需保留 `#4A85F9`，改为专用 class，不再局部直写颜色。

2. `MaterialClient/App.axaml`
   - [ ] 保持并确认 `Button.primary-button:disabled` 规则存在。
   - [ ] 若主题仍覆盖文本，补齐模板层规则：`Button.primary-button:disabled /template/ ContentPresenter#PART_ContentPresenter`。

### P1（高优先级，避免继续扩散）

3. `MaterialClient/Views/ProjectInfoWindow.axaml`
   - [x] 关闭按钮当前为局部 `Background="Transparent"` + `Foreground="White"`；统一改为标题栏专用 class（`titlebar-close-button`）。

4. `MaterialClient/Views/PrintPreviewWindow.axaml`
   - [x] 关闭按钮同上，收敛到专用 class（`titlebar-close-button`）。

5. `MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml`
   - [x] 顶部和菜单区存在多处局部透明按钮样式，已收敛到统一 class（`titlebar-minimize-button`、`titlebar-close-button`、`popup-menu-item-button`）。
   - [x] 已移除多处内联 `Button.Styles`，可复用规则上移到 `App.axaml`。

### P2（持续治理，防回归）

6. 规范与检查
   - [x] 新增约束：主操作按钮禁止局部直写 `Background/Foreground`，必须使用 class。
   - [ ] PR 自检项增加：是否覆盖了 `:disabled` 状态（含 DevTools `AccessText` 验证）。
   - [ ] 每月一次样式巡检：检索局部直写按钮并归类处理。

---

## 建议执行顺序（最小风险）

1. 先完成 `SettingsWindow` 的 3 个主按钮收敛（P0）。  
2. 验证禁用态文本颜色一致后，再处理 `ProjectInfoWindow`/`PrintPreviewWindow`（P1）。  
3. 最后统一 `AttendedWeighingWindow` 的局部按钮样式并固化规范（P1/P2）。

---

## 本轮实施进度（2026-04-27）

### 已完成

- `App.axaml`
  - 新增 `brand-primary-button` 及其 `:disabled` 与模板层文本规则。
  - 新增可复用 class：`titlebar-close-button`、`titlebar-minimize-button`、`popup-menu-item-button`。
- `SettingsWindow.axaml`
  - 「增加」（摄像头）按钮改为 `Classes="brand-primary-button"`。
  - 「增加」（车牌识别）按钮改为 `Classes="brand-primary-button"`。
  - 「确认保存」按钮改为 `Classes="brand-primary-button"`（禁用态待运行时回归确认）。
- `ProjectInfoWindow.axaml`
  - 标题栏关闭按钮改为 `Classes="titlebar-close-button"`，移除局部 hover 样式。
- `PrintPreviewWindow.axaml`
  - 标题栏关闭按钮改为 `Classes="titlebar-close-button"`。
- `AttendedWeighingWindow.axaml`
  - 标题栏最小化/关闭按钮改为全局 class。
  - 数据管理弹出菜单三项按钮改为 `Classes="popup-menu-item-button"`，移除重复内联 `Button.Styles`。

### 待完成

- 运行态 DevTools 回归（normal/disabled），确认 `AccessText` 颜色来源和显示完全符合预期。
