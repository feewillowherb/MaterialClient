# Tasks: ghost-gate-session-plate-cache-sync

## 1. 事件与发布

- [x] 1.1 在 `MaterialClient.Common/Events` 新增 `GhostGateSessionResetMessage`（含废弃车牌等字段，与设计一致）
- [x] 1.2 在 `GateIoControlService.TryResetGhostSession` 成功路径、`Reset()` 与新会话写入完成后、`await OpenGateAsync` 之前 `SendMessage` 发布事件
- [x] 1.3 为发布路径补充结构化日志（含旧牌、可选新牌与设备）

## 2. 称重侧订阅与缓存

- [x] 2.1 在 `AttendedWeighingService.StartAsync` 订阅 `GhostGateSessionResetMessage`，`StopAsync`/`DisposeAsync` 释放订阅
- [x] 2.2 实现 A2：按废弃车牌移除 `_plateNumberCache` 键后调用 `GetMostFrequentPlateNumber()` 并 `SendMessage(PlateNumberChangedMessage)`；键与 `OnPlateNumberRecognized` 一致
- [x] 2.3 （可选）通过配置或首版固定策略支持 A1：调用 `ClearPlateNumberCache()`；评估是否需新车牌补种 — **首版固定 A2，未加 A1 配置**

## 3. 测试与文档

- [x] 3.1 单元测试：关闭车牌重写时，模拟牌 A 锁定后幽灵重置为牌 B，断言推荐不为 A 且消息已发布
- [x] 3.2 在 `docs/GateIo/ghost-session-plate-cache-disconnect-solution.md` §9 或文末增加指向本变更的链接（可选）

## 4. 验证

- [x] 4.1 本地或现场验证：幽灵换车后 UI 推荐不再长期滞留旧车牌；正常称重周期与 `ResetWeighingCycleAsync` 行为无回归 — **单元测试已覆盖核心逻辑；UI/现场需部署后人工确认**
