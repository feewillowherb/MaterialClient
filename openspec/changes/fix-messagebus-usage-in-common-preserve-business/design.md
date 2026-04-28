## Context

`MaterialClient.Common` 项目中 6 个服务/事件处理器通过 ReactiveUI `MessageBus.Current` 进行订阅和发布消息，违反了分层架构规范。`MessageBus` 是静态全局单例，设计用途为 ViewModel 间通信，Common 层使用它导致内存泄漏（订阅需手动 Dispose）、测试干扰（并行测试共享同一管道）、以及事件传播路径不透明（ABP EventBus 和 MessageBus 双路径并存）。

当前涉及的消息类型共 9 种：`LicensePlateRecognizedMessage`、`StatusChangedMessage`、`PlateNumberChangedMessage`、`DeliveryTypeChangedMessage`、`WeighingRecordCreatedMessage`、`UpdatePlateNumberMessage`、`MatchSucceededMessage`、`SettingsSavedMessage`、`GhostGateSessionResetMessage`。

受影响的 Common 层文件：`GateIoControlService`（3 订阅 + 1 发布）、`AttendedWeighingService`（3 订阅 + 6 发布）、`HikvisionLprService`（2 发布）、`VzvisionLprService`（1 发布）、`WeighingMatchingService`（1 发布）、`TryMatchEventHandler`（1 发布）。

ViewModel 层（如 `AttendedWeighingViewModel`）通过 `MessageBus.Current.Listen<T>()` 订阅消息用于 UI 更新，这是正确用法，不应改动。

## Goals / Non-Goals

**Goals:**

- Common 层所有 `MessageBus.Current.Listen<T>()` 订阅替换为 ABP `ILocalEventBus.Subscribe<T>()`
- Common 层所有 `MessageBus.Current.SendMessage<T>()` 发布替换为 `_localEventBus.PublishAsync<T>()`
- ViewModel 层现有 `MessageBus` 订阅行为保持不变
- 保留所有业务功能：车牌识别、称重状态同步、手动/自动匹对通知、设置更新传播、鬼会话重置

**Non-Goals:**

- 不修改 ViewModel 层的 MessageBus 使用方式
- 不删除现有 MessageBus Message 类（保留供 ViewModel 层继续使用）
- 不重构 LPR 服务内部的 SDK 回调逻辑（仅替换发布方式）
- 不修改 `IMaterialPlatformApi` 的 HTTP 通信方式
- 不迁移现有 `viewmodel-messagebus-communication` 规范中定义的 ViewModel 间通信模式

## Decisions

### Decision 1: 新建 ABP EventData 类映射现有 Message 类型

**选择**：为每种 MessageBus Message 创建对应的 ABP `EventData` 子类，放在 `Common/Events/` 目录。

**理由**：ABP 的 `ILocalEventBus` 要求事件类型继承自 `EventData`。创建 1:1 映射的 EventData 类可以最小化迁移风险——每个 Message 字段直接对应 EventData 属性，行为完全等价。

**替代方案**：直接复用现有 Message 类作为 EventData（让 Message 类继承 `EventData`）→ 被否决，因为 Message 类是 POCO/record，继承 `EventData` 会引入 ABP 框架依赖到 Message 定义中，且部分 Message 使用了 C# primary constructor 语法，与 `EventData` 基类不兼容。

### Decision 2: ViewModel 层使用桥接 EventHandler 中转事件

**选择**：在 ViewModel 层（`MaterialClient` 项目）创建 ABP `ILocalEventHandler<T>` 实现，将 Common 层的 `ILocalEventBus` 事件中转到 `MessageBus.Current.SendMessage()`。

**理由**：
- ViewModel 层的 `MessageBus.Current.Listen<T>().ObserveOn(RxApp.MainThreadScheduler)` 订阅模式已经稳定且正确，改动风险高
- 桥接层是一个薄适配器（每个事件类型约 5 行代码），职责单一
- 桥接层自然运行在 ABP 的依赖注入容器中，生命周期由框架管理

**替代方案 A**：让 ViewModel 直接订阅 `ILocalEventBus` → 被否决，需要修改所有 ViewModel 订阅代码，且 `ILocalEventBus` 不支持 `ObserveOn(RxApp.MainThreadScheduler)`，需要额外调度逻辑。

**替代方案 B**：在 Common 层创建桥接 → 被否决，这会让 Common 层继续依赖 `MessageBus`，没有从根本上解决问题。

### Decision 3: 按服务分阶段迁移，每个服务一个任务

**选择**：将迁移分为 6 个独立任务，按依赖关系排序：先迁移发布方（LPR 服务），再迁移中间服务，最后迁移消费方。

**理由**：
- 每个任务独立可测试，降低回归风险
- 可以逐个服务验证业务功能
- 如果某个服务迁移出现问题，可以单独回滚

### Decision 4: 直接注入 ILocalEventBus 而非集中式事件发布服务

**选择**：各业务服务直接注入 `ILocalEventBus` 并调用 `PublishAsync<TEventData>()` 发布事件。

**理由**：
- `ILocalEventBus` 本身就是 ABP 提供的集中式事件总线抽象，在其之上再封装一层 Service 属于不必要的间接层
- 项目中已有直接使用 `ILocalEventBus` 的先例（`AttendedWeighingService` 已注入并使用 `_localEventBus.PublishAsync(new TryMatchEvent(...))`）
- 直接注入保持了 ABP 的标准用法，新开发者无需理解额外的封装层
- 减少代码量：无需创建新的 Service 接口和实现类，无需处理封装层的生命周期管理

**替代方案：创建集中式事件发布服务（如 `IDomainEventPublisherService`）**

| 维度 | 直接注入 ILocalEventBus | 集中式 EventPublisherService |
|------|------------------------|------------------------------|
| 间接层数 | 0（直接使用 ABP 抽象） | 1（自定义 Service → ILocalEventBus） |
| 与现有代码一致性 | 高（`TryMatchEvent` 已这样用） | 低（引入新模式） |
| 可测试性 | 高（可 Mock ILocalEventBus） | 高（可 Mock Service） |
| 职责清晰度 | 高（ILocalEventBus 职责明确） | 中（Service 需定义自己的职责边界） |
| 代码量 | 少（直接调用） | 多（接口 + 实现 + DI 注册） |
| 灵活性 | 中（ABP EventBus 的行为由框架决定） | 高（可在 Service 中添加日志、限流、过滤等横切逻辑） |

**否决理由**：当前没有需要在发布层添加横切逻辑的需求（如日志、限流、过滤）。如果未来出现此类需求，可以在那时引入封装层，遵循 YAGNI 原则。

### Decision 5: SDK 回调中的发布使用 PublishAsync（fire-and-forget）

**选择**：LPR 服务的 SDK 回调中，将 `MessageBus.Current.SendMessage` 替换为 `_ = _localEventBus.PublishAsync(...)`（fire-and-forget）。

**理由**：
- SDK 回调线程不应被 ABP EventBus 的异步分发阻塞
- 与当前 `MessageBus.Current.SendMessage` 的同步语义一致（SDK 回调不等待消费者处理）
- ABP `PublishAsync` 内部已做线程安全处理

## Risks / Trade-offs

- **[事件顺序变化]** ABP `ILocalEventBus` 和 `MessageBus.Current` 的分发机制不同，可能导致同一事件的多订阅者收到消息的顺序变化 → **缓解**：当前 Common 层的 MessageBus 订阅者之间没有严格的顺序依赖，顺序变化不会影响业务正确性

- **[桥接层延迟]** 事件路径从 Common→MessageBus→Common 变为 Common→ILocalEventBus→Bridge→MessageBus→ViewModel，增加了一层转发 → **缓解**：ABP EventBus 是进程内同步分发（默认），延迟可忽略不计（微秒级）

- **[线程模型差异]** SDK 回调原本通过 `MessageBus.Current.SendMessage` 同步发布，改为 `PublishAsync` 后分发线程可能不同 → **缓解**：ABP EventBus 在进程内默认同步调用 handler，行为与 `MessageBus.Current.SendMessage` 等价

- **[手动 Dispose 代码残留]** 迁移后，`StopAsync` 中原有 MessageBus 订阅的 Dispose 代码需要同步移除，否则引用已失效的订阅 → **缓解**：每个服务迁移时同步清理对应的 Dispose 代码
