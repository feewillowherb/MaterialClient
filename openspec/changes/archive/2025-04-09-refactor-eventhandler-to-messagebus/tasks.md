## 1. 新增 Message 类型与枚举

- [x] 1.1 在 `MaterialClient.Common/Events/` 新增 `DetailOperationType` 枚举（Save, Abolish, Match, Complete）
- [x] 1.2 在 `MaterialClient.Common/Events/` 新增 `DetailOperationCompletedMessage` 类（class + primary constructor，字段：itemId, itemType, orderType, isCompleted, operationType）
- [x] 1.3 在 `MaterialClient.Common/Events/` 新增 `DetailCloseRequestedMessage` 类（无参数 class）
- [x] 1.4 在 `MaterialClient.Common/Events/` 新增 `ManualMatchSaveCompletedMessage` 类（class + primary constructor，字段：waybillId）

## 2. 改造发布方：AttendedWeighingDetailViewModelBase

- [x] 2.1 将 `SaveCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new DetailOperationCompletedMessage(..., DetailOperationType.Save))`
- [x] 2.2 将 `AbolishCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new DetailOperationCompletedMessage(..., DetailOperationType.Abolish))`
- [x] 2.3 将 `MatchCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new DetailOperationCompletedMessage(..., DetailOperationType.Match))`
- [x] 2.4 将 `CompleteCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new DetailOperationCompletedMessage(..., DetailOperationType.Complete))`
- [x] 2.5 将 `CloseRequested?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new DetailCloseRequestedMessage())`
- [x] 2.6 将 `ManualMatchSaveCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new ManualMatchSaveCompletedMessage(...))`（注意 ManualMatch 的 operationType 不再需要，独立 Message 即可）
- [x] 2.7 移除 AttendedWeighingDetailViewModelBase 中 6 个 `public event` 声明

## 3. 改造发布方：ManualMatchEditWindowViewModel

- [x] 3.1 将 `SaveCompleted?.Invoke(...)` 替换为 `MessageBus.Current.SendMessage(new ManualMatchSaveCompletedMessage(waybillId))`
- [x] 3.2 移除 `public event EventHandler<ManualMatchSaveCompletedEventArgs>? SaveCompleted` 声明

## 4. 改造发布方：SettingsWindowViewModel

- [x] 4.1 将 `CloseRequested?.Invoke(...)` 两处替换为 `MessageBus.Current.SendMessage(new DetailCloseRequestedMessage())`
- [x] 4.2 移除 `public event EventHandler? CloseRequested` 声明

## 5. 改造订阅方：AttendedWeighingViewModel

- [x] 5.1 移除 `OpenDetailAsync` 两处重载中 12 行 `+=` 订阅代码
- [x] 5.2 新增 `MessageBus.Current.Listen<DetailOperationCompletedMessage>()` 订阅，在 handler 中根据 `OperationType` 分发到对应逻辑（OnDetailSaveCompleted / OnDetailAbolishCompleted / OnDetailMatchCompleted / OnDetailCompleteCompleted），使用 `.DisposeWith(_disposables)`
- [x] 5.3 新增 `MessageBus.Current.Listen<DetailCloseRequestedMessage>()` 订阅，映射到 `OnDetailCloseRequested` 逻辑，使用 `.DisposeWith(_disposables)`
- [x] 5.4 新增 `MessageBus.Current.Listen<ManualMatchSaveCompletedMessage>()` 订阅，映射到 `OnDetailManualMatchSaveCompleted` 逻辑，使用 `.DisposeWith(_disposables)`
- [x] 5.5 移除 `BackToMain` 中 6 行 `-=` 取消订阅代码（由 DisposeWith 自动处理）
- [x] 5.6 移除 `OnDetailXxx` 方法签名中的 `object? sender, EventArgs e` 参数，适配 Message 参数

## 6. 改造订阅方：View code-behind

- [x] 6.1 在 `SettingsWindow.axaml.cs` 中移除 `CloseRequested += OnCloseRequested` 和 `-=` 取消订阅
- [x] 6.2 在 `SettingsWindow.axaml.cs` 中新增 `CompositeDisposable _disposables`，在构造函数中订阅 `MessageBus.Current.Listen<DetailCloseRequestedMessage>().ObserveOn(RxApp.MainThreadScheduler).Subscribe(...).DisposeWith(_disposables)`，在 `OnClosed` 中 Dispose
- [x] 6.3 在 `ManualMatchWindow.axaml.cs` 中移除 `SaveCompleted +=` 订阅
- [x] 6.4 在 `ManualMatchWindow.axaml.cs` 中新增 MessageBus 订阅 `ManualMatchSaveCompletedMessage`

## 7. 清理废弃代码

- [x] 7.1 删除 `MaterialClient.Common/Events/ItemOperationCompletedEventArgs.cs` 文件
- [x] 7.2 删除 `ManualMatchSaveCompletedEventArgs` 类（在 ManualMatchEditWindowViewModel.cs 文件底部）
- [x] 7.3 全局搜索确认无对已删除类型的残留引用

## 8. 更新项目规范

- [x] 8.1 在 `AGENTS.md` 编码规范章节新增 MessageBus 约定：ViewModel 间通信必须使用 ReactiveUI MessageBus，禁止新增 `public event` 声明；订阅必须使用 `DisposeWith` 管理生命周期

## 9. 编译验证

- [x] 9.1 执行 `dotnet build` 确保零编译错误
- [x] 9.2 执行已有测试确保无回归
