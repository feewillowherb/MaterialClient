## ADDED Requirements

### Requirement: ViewModel 间通信必须使用 MessageBus

ViewModel 之间的所有事件通信 SHALL 通过 ReactiveUI `MessageBus.Current` 进行。禁止在 ViewModel 中声明 `public event` 或使用委托订阅模式。

#### Scenario: Detail 操作完成通知

- **WHEN** `AttendedWeighingDetailViewModelBase` 完成保存、作废、匹配、完成等操作
- **THEN** 必须通过 `MessageBus.Current.SendMessage(new DetailOperationCompletedMessage(...))` 发送消息，而非调用 `EventHandler.Invoke`

#### Scenario: 关闭请求通知

- **WHEN** `AttendedWeighingDetailViewModelBase` 或 `SettingsWindowViewModel` 需要请求关闭
- **THEN** 必须通过 `MessageBus.Current.SendMessage(new DetailCloseRequestedMessage())` 或对应的关闭消息发送

### Requirement: Message 类型必须遵循项目约定

所有新增 Message 类型 SHALL 放置于 `MaterialClient.Common.Events` 命名空间，使用 class + primary constructor 模式（与现有 `StatusChangedMessage`、`SaveCompletedMessage` 等一致），属性 MUST 为只读。

#### Scenario: DetailOperationCompletedMessage 结构

- **WHEN** 定义 `DetailOperationCompletedMessage`
- **THEN** 必须包含 `ItemId`（long）、`ItemType`（WeighingListItemType）、`OrderType`（OrderTypeEnum?）、`IsCompleted`（bool）、`OperationType`（DetailOperationType）五个只读属性

#### Scenario: ManualMatchSaveCompletedMessage 结构

- **WHEN** 定义 `ManualMatchSaveCompletedMessage`
- **THEN** 必须包含 `WaybillId`（long?）只读属性

### Requirement: 订阅必须使用 DisposeWith 自动清理

所有 `MessageBus.Current.Listen<T>()` 订阅 SHALL 使用 `.DisposeWith(Disposables)` 模式，禁止手动 `-=` 取消订阅。

#### Scenario: AttendedWeighingViewModel 订阅 Detail 操作消息

- **WHEN** `AttendedWeighingViewModel` 创建 DetailViewModel 并进入详情视图
- **THEN** 必须通过 `MessageBus.Current.Listen<DetailOperationCompletedMessage>().ObserveOn(RxApp.MainThreadScheduler).Subscribe(...).DisposeWith(_disposables)` 订阅，并在 `BackToMain` 时由 `Disposables` 自动清理

#### Scenario: SettingsWindow View 订阅关闭消息

- **WHEN** `SettingsWindow.axaml.cs` 初始化时订阅 `SettingsWindowViewModel` 的关闭请求
- **THEN** 必须通过 `MessageBus.Current.Listen<DetailCloseRequestedMessage>()` 或对应消息类型订阅，使用 `.DisposeWith(_disposables)` 管理

### Requirement: DetailOperationType 枚举定义操作类型

必须定义 `DetailOperationType` 枚举，包含 `Save`、`Abolish`、`Match`、`Complete` 四个值，用于在 `DetailOperationCompletedMessage` 中区分操作类型。

#### Scenario: 枚举值与原 event 的映射

- **WHEN** `OperationType` 为 `Save`
- **THEN** 等价于原 `SaveCompleted` event
- **WHEN** `OperationType` 为 `Abolish`
- **THEN** 等价于原 `AbolishCompleted` event
- **WHEN** `OperationType` 为 `Match`
- **THEN** 等价于原 `MatchCompleted` event
- **WHEN** `OperationType` 为 `Complete`
- **THEN** 等价于原 `CompleteCompleted` event

### Requirement: 废弃的 EventArgs 类必须移除

迁移完成后 MUST 删除 `ItemOperationCompletedEventArgs` 类和 `ManualMatchSaveCompletedEventArgs` 类，且不得有任何残留引用。

#### Scenario: 编译验证

- **WHEN** 完成所有迁移步骤
- **THEN** 项目编译必须通过，无任何对已删除 EventArgs 类的引用

### Requirement: AGENTS.md 必须新增 MessageBus 编码约定

在 `AGENTS.md` 的编码规范章节中 MUST 新增约定：ViewModel 间通信优先使用 ReactiveUI MessageBus，禁止新增 `public event` 声明。

#### Scenario: 约定可被引用

- **WHEN** 开发者查看 `AGENTS.md`
- **THEN** 能找到明确要求 ViewModel 间通信使用 MessageBus 的规范条目
