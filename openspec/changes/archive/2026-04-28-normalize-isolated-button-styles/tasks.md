## 1. 样式基线收敛

- [x] 1.1 审核并固化 `App.axaml` 中 `primary-button` 与 `primary-button:disabled` 规则，确认禁用态文本颜色来源可控
- [x] 1.2 如存在模板层覆盖，补充 `ContentPresenter` 文本前景规则并验证 `AccessText` 实际生效
- [x] 1.3 如需保留品牌蓝主按钮（如 `#4A85F9`），新增专用 class 并定义 normal/disabled 完整规则

## 2. 孤立主按钮改造

- [x] 2.1 改造 `SettingsWindow.axaml` 中摄像头设置「增加」按钮为 class 驱动样式
- [x] 2.2 改造 `SettingsWindow.axaml` 中车牌识别设置「增加」按钮为 class 驱动样式
- [ ] 2.3 改造 `SettingsWindow.axaml` 中「确认保存」按钮为 class 驱动样式并完成禁用态验证

## 3. 扩展治理与验证

- [x] 3.1 梳理 `ProjectInfoWindow.axaml` 与 `PrintPreviewWindow.axaml` 的孤立透明按钮并统一到 class 体系
- [x] 3.2 梳理 `AttendedWeighingWindow.axaml` 中局部按钮样式，迁移可复用规则到全局样式
- [x] 3.3 更新 `docs/evaluation-isolated-button-styles-2026-04-27.md` 的整改进度与执行结果
- [ ] 3.4 使用 DevTools 对关键页面进行 normal/disabled 状态回归，确认文本颜色与来源一致
