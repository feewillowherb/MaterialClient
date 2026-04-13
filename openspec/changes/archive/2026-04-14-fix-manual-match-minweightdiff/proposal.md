## Why

手动匹配（Manual Match）场景使用与自动匹配相同的 `_minWeightDiff` 阈值（默认 1 吨），导致重量差较小的记录对被过滤掉无法匹配。手动匹配由人工确认，应使用更宽松的固定阈值 0.1 吨，以覆盖更宽泛的重量差异范围，同时不影响自动匹配的判定标准。

## What Changes

- 在 `WeighingMatchingService` 中定义手动匹配专用常量 `ManualMatchMinWeightDiff = 0.1m`。
- `GetCandidateRecordsAsync` 方法增加可选参数 `decimal? minWeightDiffOverride`，手动匹配调用时传入 `0.1m`，自动匹配路径保持原行为。
- `ManualMatchAsync` 方法中 `TryMatch` 调用使用固定常量 `0.1m` 替代 `_minWeightDiff`。
- 自动匹配路径（`TryMatchWithDeliveryTypeAsync`）不受影响，继续使用 `_minWeightDiff`。

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

（无已有 spec 需要修改。本次变更仅调整手动匹配路径的阈值参数，属于实现层面的配置调整，不改变 spec 级别行为定义。）

## Impact

| File Path | Change Type | Change Reason | Impact Scope |
|-----------|-------------|---------------|--------------|
| `MaterialClient.Common/Services/WeighingMatchingService.cs` | Modify | 添加常量、修改 `GetCandidateRecordsAsync` 签名和两处 `TryMatch` 调用 | 仅影响手动匹配路径 |

- **自动匹配**：不受影响，仍使用 `_minWeightDiff` 配置值。
- **UI 层**：`ManualMatchWindowViewModel` 和 `ManualMatchEditWindowViewModel` 无需修改（服务层内部变更）。
- **配置**：设置界面的 `MinWeightDiff` 仅影响自动匹配，手动匹配固定为 0.1。
