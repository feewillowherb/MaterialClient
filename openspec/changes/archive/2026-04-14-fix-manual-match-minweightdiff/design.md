## Context

`WeighingMatchingService` 中的 `_minWeightDiff`（默认 1 吨，可通过设置配置）同时用于自动匹配和手动匹配。手动匹配由人工确认，需要更宽松的阈值以覆盖重量差较小的记录对。

当前调用链：

```
自动匹配路径:
  TryMatchWithDeliveryTypeAsync()
    → GetCandidateRecordsAsync(record, deliveryType)
      → TryMatch(..., _minWeightDiff)
    → TryMatch(..., _minWeightDiff)

手动匹配路径:
  ManualMatchWindowViewModel.LoadCandidateRecordsAsync()
    → GetCandidateRecordsAsync(record, deliveryType)
      → TryMatch(..., _minWeightDiff)        ← 需改为 0.1m
  ManualMatchEditWindowViewModel.SaveAsync()
    → ManualMatchAsync(current, matched, deliveryType)
      → TryMatch(..., _minWeightDiff)        ← 需改为 0.1m
```

## Goals / Non-Goals

**Goals:**
- 手动匹配的两处 `TryMatch` 调用使用固定阈值 0.1 吨
- 自动匹配路径保持原有行为不变

**Non-Goals:**
- 不修改 `TryMatch` 方法本身
- 不修改设置界面的 `MinWeightDiff` 配置项
- 不修改 UI 层代码

## Decisions

### Decision 1: 通过可选参数扩展 `GetCandidateRecordsAsync`

**选择**: 添加 `decimal? minWeightDiffOverride = null` 参数。为 `null` 时使用 `_minWeightDiff`（自动匹配行为不变），有值时使用传入值。

**替代方案**: 拆分为两个方法 `GetCandidateRecordsAsync` / `GetCandidateRecordsForManualAsync`。

**理由**: 变更范围最小，自动匹配调用方无需修改，方法签名向后兼容。

### Decision 2: 在 `ManualMatchAsync` 中直接使用常量

**选择**: 定义 `private const decimal ManualMatchMinWeightDiff = 0.1m;`，在 `ManualMatchAsync` 的 `TryMatch` 调用中直接引用。

**理由**: `ManualMatchAsync` 仅被手动匹配调用，无需参数化，常量语义最清晰。

### Decision 3: `ManualMatchWindowViewModel` 传入常量

`ManualMatchWindowViewModel.LoadCandidateRecordsAsync` 调用 `GetCandidateRecordsAsync` 时需传入 `minWeightDiffOverride: 0.1m`。但 0.1m 的值应来源于 service 层常量而非 ViewModel 硬编码。

**选择**: 在 `IWeighingMatchingService` 接口中暴露常量，或在 `GetCandidateRecordsAsync` 的手动匹配调用处直接使用 service 内部常量。

**实际方案**: `ManualMatchWindowViewModel` 无需修改——由 `WeighingMatchingService` 内部在手动匹配方法中直接使用常量。但 `GetCandidateRecordsAsync` 是共用方法，ViewModel 调用时无法区分。因此 ViewModel 需要传入 `minWeightDiffOverride: 0.1m`，该值定义为 `WeighingMatchingService` 上的 `public const`。

## Risks / Trade-offs

- **[常量散布]** → `0.1m` 常量需在 service 接口/实现和 ViewModel 间共享。通过 `public const` 在接口上定义，避免魔法数字。
- **[自动匹配误用]** → `GetCandidateRecordsAsync` 新增参数有默认值 `null`，现有调用方不受影响。
- **[阈值合理性]** → 0.1 吨是业务要求的固定值，不随配置变化，无动态调整风险。
