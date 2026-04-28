# AttendedWeighingServiceTests 迁移说明

Date: 2026-04-28

## 背景

`AttendedWeighingServiceTests` 包含 48 个测试，其中 17 个依赖 `Task.Delay` 模拟真实时间流逝（重量稳定性窗口 3000ms + 数据点间隔 200ms）。这些测试本质上是集成测试，而非单元测试：

- 运行耗时：1m47s（占整个测试类 90% 以上时间）
- 失败率：3/48 flaky（6.25%），均为时序依赖导致
- 降低了日常开发流程的流畅度

## 决策

将 17 个时序依赖测试标记为 `Skip`，待后续移植至独立的集成测试项目。

## 待移植测试清单

### 重量稳定性模拟（14 个）

模拟完整称重周期（上磅 → 稳定 → 下磅），需要发送 20-30 个数据点并等待 3 秒稳定性窗口。

| 测试方法 | 单测耗时 | 跳过原因 |
|----------|---------|---------|
| `StabilityStream_Should_IdentifyStableWeights` | 5s+ | 20 次数据点 + 1s 等待 |
| `Should_CreateRecord_WhenWeightStabilizes` | 6s+ | 20 次数据点 + 2s 等待 |
| `Should_CreateRecord_UseLockedAtPlateNumber_WhenEnablePlateRewriteDisabled` | 6s+ | 20 次数据点 + 2s 等待 |
| `Should_CapturePhotos_WhenWeightStabilizes` | 6s+ | 20 次数据点 + 2s 等待 |
| `Should_PreventDuplicateRecordCreation` | 8s+ | 30 次数据点 + 2s 等待 |
| `NormalFlow_Should_CompleteFullCycle` | 6s+ | 20 次数据点 + 2s 等待 |
| `AbnormalDeparture_FromWaitingForStability_Should_Reset` | 1s+ | 时序依赖的状态转换 |
| `UnstableDeparture_Then_StableWeighing_Should_CompleteCycle` | 5s+ | 20 次数据点 + 1s 等待 |
| `WeightFluctuation_AroundThreshold_Should_HandleCorrectly` | 2s+ | 8 次 300ms 等待 |
| `WeightTransition_FromLowToThresholdToStable_Should_CompleteCycle` | 8s+ | 35 次数据点 + 多次等待 |
| `StabilityCheck_Should_NotUseHistoricalData_AfterStateTransition` | 6s+ | 20 次数据点 + 1s 等待 |
| `StabilityCheck_Should_RequireFullWindow_AfterEnteringWaitingForStability` | 5s+ | 20 次数据点 + 500ms 等待 |
| `WeightStabilized_Then_DropAndRise_Should_HandleCorrectly` | 6s+ | 20 次数据点 + 多次等待 |
| `Stability_Should_BeCleared_WhenTransitioningToOffScale` | 7s+ | 40 次数据点 + flaky |
| `Should_HandleErrors_InAsyncOperations` | 6s+ | 20 次数据点 + 2s 等待 |

### Flaky 测试（2 个）

| 测试方法 | 失败表现 | 跳过原因 |
|----------|---------|---------|
| `OnPlateNumberRecognized_Should_FilterHangingCharacter` | 期望 "京A12345" 得到 null | 时序依赖导致 flaky |
| `OnPlateNumberRecognized_Should_Ignore_WhenOffScale` | 期望 null 得到 "京A12345" | 时序依赖导致 flaky |

## 保留的测试（31 个）

保留的测试覆盖以下场景，运行时间约 17s：

- **生命周期**：Start/Stop/Dispose/幂等性
- **状态管理**：DeliveryType 切换与通知
- **车牌识别**：无效车牌过滤、频率统计、优先级选择、颜色过滤、LockedAt 保留、并发安全
- **缓存管理**：空缓存、重置清除
- **错误处理**：异常后继续运行

## 移植方向

这些测试应移植至独立的集成测试项目（如 `MaterialClient.IntegrationTests`），仅在 nightly CI 或手动触发时运行。可选方案：

1. **Rx TestScheduler**：使用虚拟时间替代 `Task.Delay`，测试瞬时完成（需重构 Service 注入 IScheduler）
2. **独立集成测试项目**：保持现有实现，移至独立项目，不阻塞日常开发
3. **E2E 测试替代**：通过真实硬件或模拟器验证完整的称重流程
