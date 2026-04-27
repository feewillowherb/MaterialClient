## Why

当前运单废单流程是全有或全无的：废除运单时总是同时丢弃进场（Join）和出场（Out）两条称重记录。在实际业务场景中，操作员有时只需要废除其中一侧——例如，某一条称重记录是在误操作下产生的，而另一条仍然有效，应当保留以供后续重新匹配。没有选择性废除的情况下，唯一的变通方法是废除整个运单后手动重建有效记录，既容易出错又浪费时间。

## What Changes

- 新增废除范围选择对话框（Ursa.Avalonia OverlayDialog），提供三个选项：仅废除进场称重记录、仅废除出场称重记录、或全部废除（现有的一刀切行为）。该对话框仅在 `FirstWeight` 状态的运单上可用；已完成运单（`OrderType == Completed`）不可废除。
- 引入 `WaybillVoidScope` 枚举（`JoinOnly`、`OutOnly`、`Both`）表示用户的选择。
- 在领域服务层新增 `VoidWaybillAsync(WaybillVoidScope scope)` 方法：
  - 将选中的 WeighingRecord 标记为软删除（`IsDeleted = true`）并清除其 `WaybillId` / `MatchedId` / `MatchedType` 引用。
  - 对于部分废除（JoinOnly 或 OutOnly）：清除保留记录上的匹配引用，使其进入未匹配/待匹配状态，支持后续通过现有 `ManualMatchAsync` 流程（使用 `minWeightDiff = 0.1`）手动重新匹配。
  - 将运单本身标记为已取消（`OrderType = Esc`）并记录 `AbortReason`。
- 通过新的 ReactiveUI `Interaction` 将对话框和服务方法接入现有的详情 ViewModel。
- 废除运单后通过 `IsPendingSync = true` 标记待同步，由现有轮询服务通过 `SynchronizationModifyOrderAsync` 推送到平台，无需新增 API 端点。

## Capabilities

### New Capabilities

- `waybill-selective-void`: 端到端的运单选择性废除——涵盖对话框 UI、部分记录解除匹配的废除领域逻辑，以及平台同步通知。

### Modified Capabilities

_未识别到需要修改的现有能力。现有的 `manual-match-threshold` 规范（手动匹配 minWeightDiff = 0.1）被消费但未改变。现有 WeighingRecord 废除行为保持不变。_

## Impact

- **领域实体**：`Waybill` 新增 `AbortWaybill(string reason)` 命令方法。`WeighingRecord` 新增 `Unmatch()` 方法以清除匹配引用。
- **服务层**：新增领域服务方法（或扩展现有服务）以编排选择性废除逻辑。
- **API 层**：无需新增端点，废除运单通过现有 `SynchronizationModifyOrderAsync` 由轮询服务同步。
- **UI 层**：新增 `WaybillVoidScopeSelectionDialog` 视图/视图模型对；现有详情 ViewModel 暴露新的 `Interaction` 用于废除范围选择流程。"废单" 按钮可见性更新为对已完成运单隐藏（`IsAbolishButtonVisible` 绑定）。
- **数据**：软删除的称重记录保留审计轨迹。保留的记录可参与手动重新匹配。
- **向后兼容性**：现有的 WeighingRecord 级别废除流程不受影响。"Both" 范围选项完全复制当前行为。
