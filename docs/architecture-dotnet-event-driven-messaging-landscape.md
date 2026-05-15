# .NET 进程内消息与事件驱动基础设施概览

本文不绑定本仓库 `AGENTS.md` 的约定，从一般技术视角说明：为何存在多种「事件 / 消息」库与实现、各自特点与痛点，以及是否存在 **UI 与 Common（或非 UI 层）共用** 的单一方案。

与仅对比 ABP 与 ReactiveUI 的说明可对照阅读：[architecture-abp-local-eventbus-vs-reactiveui-messagebus.md](./architecture-abp-local-eventbus-vs-reactiveui-messagebus.md)。

---

## 1. 为什么会区分这么多库和实现？

本质不是「.NET 缺少官方总线」，而是 **问题域不同、历史生态不同**，各层演化出了最顺手的工具。

### 1.1 常见类别与对照

| 类别 | 代表 | 特点 | 主要解决的痛点 |
|------|------|------|----------------|
| **UI / MVVM 消息** | ReactiveUI `MessageBus`、Prism `IEventAggregator`、部分 MVVM Toolkit 的 Messenger | 常与 ViewModel 生命周期、Rx 管线配合；不少实现偏向 **静态入口** 或 **弱引用** | View 与 VM、VM 与 VM 之间 **松耦合通知**，避免层层传递委托 |
| **应用框架内本地事件** | ABP `ILocalEventBus`、`IEventBus` | 与 **DI、模块化** 绑定；多 `ILocalEventHandler<T>`；可与横切关注点（如工作单元）协同 | **基础设施 ↔ 应用服务** 之间广播，且希望与容器、模块边界一致 |
| **CQRS / 用例内通知** | MediatR `INotification` / `IPublisher` | **一次发布、多处理器**；与命令/查询模型统一 | 减少服务间直接引用，用 **中介** 编排业务步骤；偏用例级而非纯 UI 闪烁 |
| **高吞吐、背压、管道** | `System.Threading.Channels`、`System.IO.Pipelines`、TPL Dataflow | **有界队列、异步读写、可 await** | 「事件风暴」、生产者快于消费者，需要 **限流与背压**，而不是无界内存多播 |
| **分布式 / 集成** | MassTransit、各类云消息服务等 | 跨进程、持久化、重试、拓扑 | **进程外**可靠投递与系统集成；与进程内总线是不同问题 |
| **语言级 / BCL** | `event` + `EventHandler<T>` | 简单、零额外依赖 | 组件级回调；**不适合**大范围跨层广播（耦合强、多订阅者扩展与测试较差） |

### 1.2 「多」背后的几条轴线

1. **耦合位置不同**：有的挂在静态入口（便于 ViewModel），有的挂在 **DI**（便于服务与单测替换）。
2. **线程与生命周期不同**：UI 常要主线程与可预测的 **Dispose**；服务层要 **作用域**、异步完成语义、与宿主生命周期对齐。
3. **表达能力不同**：有人需要 **`IObservable` 组合**；有人需要 **发布即忘 + 多个独立 Handler**；有人需要 **有界队列** 而不是无限 Subject。

这些能力 **有重叠**，但很少有单一库在「UI 人体工学 + 框架级横切 + 背压管道 + 分布式」上同时做到最优，因此生态中并存多种实现。

---

## 2. 是否存在「既可在 UI 使用又可在 Common 使用」的单一方案？

**有**，但通常是 **选定一个主轴 + 团队纪律**，而不是等待一个「万能官方库」。

### 2.1 思路 A：单一进程内总线，一律经 DI（最常作为「统一方案」讨论）

- 定义薄抽象，例如 `IAppEventPublisher` / `IAppEventSubscriber`（或统一的 `IEventBus`），**实现**可选用其一：
  - **MediatR** 的 `INotification`（服务层自然；ViewModel 注入 `IMediator`/`IPublisher` 同样可发布；处理侧用 `INotificationHandler<T>` 或在外层包成 `IObservable`）；
  - 或 **ABP `ILocalEventBus`**（若全栈已依赖 ABP，UI 项目解析同一实例即可）；
  - 或 **Rx `Subject` + 薄封装**（统一暴露 `IObservable<T>` 与 `Publish`，对 ReactiveUI 友好，Common 也可注入同一单例）。
- **UI 与 Common 的边界** 用 **事件类型所在程序集**（如 `*.Contracts`）划分，而不是用两套总线划分。

**优点**：一处实现、两处注入；测试可替换为 fake。  
**代价**：需约定 **禁止第二套静态总线**，否则仍会分裂为双通道。

### 2.2 思路 B：双通道但职责硬切（工程上常见的折中）

- **进程内业务 / 集成事件**：DI 总线（ABP / MediatR / 自研）。
- **纯 UI 瞬时信号**（例如「关闭某对话框」）：仍用 UI 框架自带消息或极薄的 UI 专用总线。

这不是「一个库」，而是 **两个抽象层级**；优点是 UI 不必背负全部应用框架概念，缺点是类型与文档要明确边界。

### 2.3 思路 C：`Channel` / `IAsyncEnumerable` 作为「事件流」而非「总线」

- 适合 **单主题、高吞吐、消费者明确** 的数据流（例如传感器读数）。
- 与「广播总线」 **并存** 更合理：总线负责 **多订阅者、多类型松耦合通知**；Channel 负责 **管道与背压**。

---

## 3. 与 .NET 10 的关系

**.NET 10** 提供的是 **Channels、Hosting、异步与可观测性基座** 等通用能力；**应用内松耦合广播** 仍属于 **选型与薄封装** 问题，运行时并不会单独再提供一个「官方 MessageBus」。

---

## 4. 小结

1. **为何区分这么多库**：历史分层（UI 框架、应用框架、中间件、BCL）不同，各自优化了耦合方式、DI、Rx、背压或分布式中的一部分，没有单点在全部场景下无痛最优。
2. **UI + Common 能否统一**：**能**——以 **DI 注册的单一进程内事件抽象**（MediatR、ABP Local、或 Rx 封装等）为主轴最干净；是否与 Channels 等并存，取决于是否有 **流式 + 背压** 需求。

---

*文档用途：架构讨论与选型参考；与 OpenSpec 无强制绑定。*
