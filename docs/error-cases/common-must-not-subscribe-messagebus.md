# 错误样例：Common 层禁止订阅 ReactiveUI MessageBus

## 规则

`MaterialClient.Common` 项目中的服务、事件处理器等非 UI 组件**禁止**通过 `MessageBus.Current.Listen<T>()` 订阅消息，也**禁止**通过 `MessageBus.Current.SendMessage()` 发布需要跨 Service 传递的消息。

`MessageBus.Current` 是 ReactiveUI 的全局单例，设计用途是 **ViewModel 间通信**。Common 层的服务间通信应使用 **ABP `ILocalEventBus`**。

**为什么**：

- `MessageBus` 是静态全局总线，所有订阅者共享同一管道，无法按作用域隔离——测试中两个并行 Service 同时订阅同一消息类型会互相干扰
- `MessageBus` 订阅没有自动生命周期管理，Service 层订阅后必须手动 Dispose，遗漏即内存泄漏
- Common 层通过 `MessageBus` 直接发布消息给 UI，跳过了 ABP 事件系统，导致事件传播路径不透明——同一个业务事件可能同时走 `ILocalEventBus` 和 `MessageBus` 两条路径，难以追踪
- 违反分层架构——Common 层不应依赖 ReactiveUI 框架概念（`RxApp.MainThreadScheduler`、`ObserveOn` 等）

---

## 错误用例

### 1. GateIoControlService — 订阅 MessageBus

**文件**：`MaterialClient.Common/Services/GateIoControlService.cs`

```csharp
public async Task StartAsync()
{
    // ...
    _lprSubscription = MessageBus.Current
        .Listen<LicensePlateRecognizedMessage>()     // 违规：Common 层订阅 MessageBus
        .Subscribe(msg => _ = HandlePlateRecognizedAsync(msg));

    _statusSubscription = MessageBus.Current
        .Listen<StatusChangedMessage>()               // 违规
        .Subscribe(msg => OnStatusChanged(msg.Status));

    _settingsSavedSubscription = MessageBus.Current
        .Listen<SettingsSavedMessage>()               // 违规
        .Subscribe(_msg => { _ = RefreshRuntimeConfigAsync(); });
}
```

同时，该 Service 也在发布消息：

```csharp
// GateIoControlService.cs:274
MessageBus.Current.SendMessage(ghostMsg);  // 违规：通过 MessageBus 发布给 Common 层消费者
```

**问题清单**：

1. `GateIoControlService` 通过 `MessageBus` 订阅了 `LicensePlateRecognizedMessage`、`StatusChangedMessage`、`SettingsSavedMessage` 三种消息——这些消息的发布方也在 Common 层（LPR Service、AttendedWeighingService），形成了 Common→MessageBus→Common 的闭环
2. 三个订阅的生命周期靠手动 Dispose 管理，如果 `StopAsync` 未被调用则泄漏
3. `GhostGateSessionResetMessage` 由 `GateIoControlService` 发布、`AttendedWeighingService` 订阅——两者都是 Common 层 Service，完全应该走 `ILocalEventBus`

---

### 2. AttendedWeighingService — 订阅 + 发布 MessageBus

**文件**：`MaterialClient.Common/Services/AttendedWeighingService.cs`

```csharp
// 订阅（Common 层不应订阅 MessageBus）
_licensePlateSubscription = MessageBus.Current
    .Listen<LicensePlateRecognizedMessage>()         // 违规
    .Subscribe(msg => { OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType); });

_ghostGateSessionSubscription = MessageBus.Current
    .Listen<GhostGateSessionResetMessage>()          // 违规
    .Subscribe(msg => { /* ... */ });

_settingsSavedSubscription = MessageBus.Current
    .Listen<SettingsSavedMessage>()                  // 违规
    .Subscribe(_ => EnqueueAsyncOperation(UpdateRuntimeConfigurationAsync));
```

```csharp
// 发布（部分消息仅 UI 消费，可接受；但部分是 Service 间通信）
MessageBus.Current.SendMessage(new StatusChangedMessage(newStatus));     // :1013
MessageBus.Current.SendMessage(new PlateNumberChangedMessage(...));      // :240, :527, :1543
MessageBus.Current.SendMessage(new DeliveryTypeChangedMessage(...));     // :464
MessageBus.Current.SendMessage(new WeighingRecordCreatedMessage(...));   // :1367
MessageBus.Current.SendMessage(new UpdatePlateNumberMessage(...));       // :1466
```

**问题清单**：

1. `AttendedWeighingService` 虽然已注入 `ILocalEventBus`，但完全未使用它来替代 `MessageBus` 订阅
2. 订阅了 3 种消息，全部来自其他 Common 层 Service
3. 发布了 6 种消息——其中 `StatusChangedMessage`、`PlateNumberChangedMessage` 等被 `GateIoControlService` 等 Common 层 Service 消费，属于 Service 间通信

---

### 3. LPR Service — 通过 MessageBus 发布

**文件**：`MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs`

```csharp
// :357 — 海康 SDK 回调中直接发布
MessageBus.Current.SendMessage(message);
```

**文件**：`MaterialClient.Common/Services/Vzvision/VzvisionLprService.cs`

```csharp
// :356 — 御道 SDK 回调中直接发布
MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage { ... });
```

**问题**：LPR Service 是车牌识别的底层服务，其职责是识别车牌并向上层传递结果。当前通过 `MessageBus` 直接发布，导致所有消费者（包括 Common 层的 `AttendedWeighingService`、`GateIoControlService`）都依赖 ReactiveUI 全局总线。

> **注意**：SDK 回调中使用 `MessageBus.Current.SendMessage` 本身是线程安全的（参见 AGENTS.md），但发布目标应改为 `ILocalEventBus`，由 ABP 管理分发。

---

### 4. WeighingMatchingService — 通过 MessageBus 发布

**文件**：`MaterialClient.Common/Services/WeighingMatchingService.cs`

```csharp
// :580 — 手动匹配成功后通知
var message = new MatchSucceededMessage(waybill.Id, currentRecord.Id);
MessageBus.Current.SendMessage(message);
```

---

### 5. TryMatchEventHandler — 通过 MessageBus 发布

**文件**：`MaterialClient.Common/Events/TryMatchEventHandler.cs`

```csharp
// :44 — 自动匹配成功后通知
var message = new MatchSucceededMessage(weighingRecord.WaybillId.Value, eventData.WeighingRecordId);
MessageBus.Current.SendMessage(message);
```

**问题**：`TryMatchEventHandler` 本身就是 ABP 事件处理器，在处理完 `TryMatchEvent` 后又绕道 `MessageBus` 发送匹配结果，导致同一业务事件存在两条传播路径。

---

## 受影响的文件汇总

| 文件 | 违规类型 | 消息类型 |
|------|---------|---------|
| `Common/Services/GateIoControlService.cs` | 订阅 + 发布 | `LicensePlateRecognizedMessage`, `StatusChangedMessage`, `SettingsSavedMessage`, `GhostGateSessionResetMessage` |
| `Common/Services/AttendedWeighingService.cs` | 订阅 + 发布 | `LicensePlateRecognizedMessage`, `GhostGateSessionResetMessage`, `SettingsSavedMessage`, `StatusChangedMessage`, `PlateNumberChangedMessage`, `DeliveryTypeChangedMessage`, `WeighingRecordCreatedMessage`, `UpdatePlateNumberMessage` |
| `Common/Services/Hikvision/HikvisionLprService.cs` | 发布 | `LicensePlateRecognizedMessage` |
| `Common/Services/Vzvision/VzvisionLprService.cs` | 发布 | `LicensePlateRecognizedMessage` |
| `Common/Services/WeighingMatchingService.cs` | 发布 | `MatchSucceededMessage` |
| `Common/Events/TryMatchEventHandler.cs` | 发布 | `MatchSucceededMessage` |

---

## 正确用例

**文件**：`MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

```csharp
// ViewModel 中订阅 MessageBus — 正确：这是 UI 层
private void StartStatusChangedMessageBusSubscription()
{
    MessageBus.Current.Listen<StatusChangedMessage>()
        .ObserveOn(RxApp.MainThreadScheduler)   // 调度到 UI 线程
        .Subscribe(message =>
        {
            _currentWeighingStatus = message.Status;
            this.RaisePropertyChanged(nameof(CurrentWeighingStatusText));
        })
        .DisposeWith(_disposables);               // 视图模型生命周期管理
}
```

**正确之处**：

1. 订阅方是 ViewModel（UI 层），符合 `MessageBus` 的设计用途
2. 使用 `ObserveOn(RxApp.MainThreadScheduler)` 确保回调在 UI 线程执行
3. 使用 `DisposeWith(_disposables)` 绑定 ViewModel 生命周期，避免泄漏

---

## 修复方向

### 原则

- **Common 层 Service 间通信**：全部改用 ABP `ILocalEventBus` 发布/订阅自定义 `EventData`
- **Common → UI 通信**：Service 发布 `ILocalEventBus` 事件，由 ViewModel 中转或由 ABP EventHandler 发布到 `MessageBus`（保持 UI 层订阅方式不变）
- **LPR Service 发布**：从 `MessageBus.Current.SendMessage` 改为 `_localEventBus.PublishAsync`

### 具体步骤

1. 为 `LicensePlateRecognized`、`StatusChanged`、`PlateNumberChanged` 等创建对应的 ABP `EventData` 类（放在 `Common/Events/`）
2. LPR Service（Hikvision/Vzvision）注入 `ILocalEventBus`，将 `MessageBus.Current.SendMessage` 替换为 `_localEventBus.PublishAsync`
3. `AttendedWeighingService`、`GateIoControlService` 将 `MessageBus.Current.Listen<T>()` 订阅替换为 `ILocalEventBus.Subscribe<T>()`
4. `TryMatchEventHandler`、`WeighingMatchingService` 的 `MessageBus.Current.SendMessage` 替换为 `_localEventBus.PublishAsync`
5. 在 ViewModel 层或专门的 ABP EventHandler 中，将 `ILocalEventBus` 事件中转到 `MessageBus`（如需保持现有 ViewModel 订阅方式不变），或让 ViewModel 直接订阅 `ILocalEventBus`
