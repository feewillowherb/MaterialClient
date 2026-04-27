## 1. 领域层变更

- [x] 1.1 在 `MaterialClient.Common/Entities/Enums/WaybillVoidScope.cs` 中创建 `WaybillVoidScope` 枚举，包含值 `JoinOnly = 0`、`OutOnly = 1`、`Both = 2`
- [x] 1.2 在 `WeighingRecord` 实体中新增 `Unmatch()` 方法，将 `MatchedId`、`WaybillId` 和 `MatchedType` 清除为 null
- [x] 1.3 在 `Waybill` 实体中新增 `AbortWaybill(string reason)` 方法，设置 `OrderType = Esc` 和 `AbortReason = reason`

## 2. 领域服务

- [x] 2.1 在 `MaterialClient.Common/Services/WaybillVoidService.cs` 中创建 `IWaybillVoidService` 接口，包含 `Task VoidWaybillAsync(long waybillId, WaybillVoidScope scope, string reason)` 方法
- [x] 2.2 实现 `WaybillVoidService` 类（实现 `IWaybillVoidService, ITransientDependency`，使用 `[AutoConstructor]`）：
  - 注入 `IRepository<Waybill, long>`、`IRepository<WeighingRecord, long>`、`ILogger<WaybillVoidService>`
  - 按 ID 加载运单，按 `WaybillId` 加载关联的 WeighingRecords
  - 通过 `MatchedType` 识别进场/出场记录
  - 根据范围：软删除选中的记录，对保留记录调用 `Unmatch()`
  - 对运单调用 `AbortWaybill(reason)`
  - 对运单调用 `SetPendingSync()` 标记待同步（由现有轮询服务通过 `SynchronizationModifyOrderAsync` 推送）
  - 使用 `[UnitOfWork]` 装饰

## 3. UI - 废除范围选择对话框

- [x] 3.1 在 `MaterialClient/ViewModels/` 中创建 `WaybillVoidScopeSelectionViewModel.cs`：
  - 暴露 `SelectedScope`（Reactive 属性，默认 `null`，无预选）
  - 暴露计算属性 `HasSelection`（`SelectedScope != null`），用于控制"确认"按钮启用状态
  - 暴露 `ConfirmCommand` 和 `CancelCommand`（`ConfirmCommand` 通过 `CanExecute` 绑定 `HasSelection`）
  - 实现 `IDialogContext`，包含 `RequestClose` 事件
  - 使用 ReactiveUI 源生成器（`[Reactive]`、`[ReactiveCommand]`）
- [x] 3.2 在 `MaterialClient/Views/Dialogs/` 中创建 `WaybillVoidScopeSelectionDialog.axaml`：
  - UserControl，包含三个选项的 RadioButton 组（进场称重记录、出场称重记录、全部废除）
  - 每个选项显示简要描述
  - 底部为取消和确认按钮
  - 使用 Ursa.Avalonia `u:FormItem` 或标准 Avalonia `RadioButton` 控件
- [x] 3.3 创建 `WaybillVoidScopeSelectionDialog.axaml.cs` code-behind

## 4. ViewModel 集成

- [x] 4.1 在 `AttendedWeighingDetailViewModelBase` 中新增 `IsAbolishButtonVisible` reactive 属性（与现有的 `IsMatchButtonVisible` 和 `IsCompleteButtonVisible` 并列）
- [x] 4.2 在 `InitializeAsync` 方法中设置 `IsAbolishButtonVisible`：WeighingRecord 和 FirstWeight 运单为 `true`，已完成运单为 `false`（`_listItem.ItemType == WeighingListItemType.Waybill && _listItem.OrderType == OrderTypeEnum.Completed`）
- [x] 4.3 在 `AttendedWeighingDetailView.axaml` 中将"废单"按钮的 `IsVisible` 绑定到 `IsAbolishButtonVisible`
- [x] 4.4 在 `AttendedWeighingDetailViewModelBase` 构造函数中注入 `IRepository<Waybill, long>`
- [x] 4.5 重写 `AttendedWeighingDetailViewModelBase` 中的 `AbolishAsync()`：
  - 根据 `_listItem.ItemType` 分支：
    - `WeighingRecord`：现有 MessageBox 确认 + `_weighingRecordRepository.DeleteAsync`（行为不变）
    - `Waybill`（由按钮可见性保证为 FirstWeight）：调用 `OverlayDialog.ShowCustomAsync<WaybillVoidScopeSelectionDialog, WaybillVoidScopeSelectionViewModel, WaybillVoidScope?>`
  - 对话框确认后，按范围调用 `_waybillVoidService.VoidWaybillAsync(waybillId, scope, reason)`
  - 成功后发送 `DetailOperationCompletedMessage`，`OperationType = Abolish`
  - 失败时处理并记录错误日志

## 5. 验证

- [ ] 5.1 验证：打开未匹配 WeighingRecord 的详情视图，点击"废单"显示简单 MessageBox 确认（无范围对话框）
- [ ] 5.2 验证：打开 FirstWeight 运单的详情视图，点击"废单"显示包含三个选项的范围选择对话框，默认无选中项，"确认"按钮禁用
- [ ] 5.3 验证：打开 Completed 运单的详情视图 —— "废单"按钮不可见
- [ ] 5.4 验证：选择"全部废除"软删除两条 WeighingRecords 并将运单 OrderType 设为 Esc
- [ ] 5.5 验证：选择"进场称重记录"软删除进场记录，出场记录重新出现在未匹配列表中
- [ ] 5.6 验证：选择"出场称重记录"软删除出场记录，进场记录重新出现在未匹配列表中
- [ ] 5.7 验证：部分废除后保留的记录可通过现有 ManualMatch 流程手动匹配
- [ ] 5.8 验证：废除后运单 `IsPendingSync` 为 `true`，下一轮轮询可同步到平台
- [x] 5.9 验证：构建成功，无警告或错误
