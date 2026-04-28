## Why

Common 层（`MaterialClient.Common`）中多个服务违反架构规范，通过 ReactiveUI `MessageBus.Current` 进行订阅和发布消息。`MessageBus` 是静态全局单例，设计用于 ViewModel 间通信，Common 层使用它会导致内存泄漏、测试干扰、事件传播路径不透明，并违反分层架构。需要将 Common 层的 `MessageBus` 使用全部迁移到 ABP `ILocalEventBus`，同时保留所有现有业务功能。

## What Changes

- **新建 ABP EventData 类**：为当前通过 MessageBus 传递的所有消息类型创建对应的 ABP `EventData` 子类（`LicensePlateRecognizedEventData`、`StatusChangedEventData`、`PlateNumberChangedEventData`、`DeliveryTypeChangedEventData`、`WeighingRecordCreatedEventData`、`UpdatePlateNumberEventData`、`MatchSucceededEventData`、`SettingsSavedEventData`、`GhostGateSessionResetEventData`）
- **重构 LPR 服务发布**：`HikvisionLprService` 和 `VzvisionLprService` 注入 `ILocalEventBus`，将 `MessageBus.Current.SendMessage<LicensePlateRecognizedMessage>` 替换为 `_localEventBus.PublishAsync<LicensePlateRecognizedEventData>`
- **重构 AttendedWeighingService**：移除 3 个 MessageBus 订阅（`LicensePlateRecognizedMessage`、`GhostGateSessionResetMessage`、`SettingsSavedMessage`），改为通过 `ILocalEventBus.Subscribe` 接收；将 6 处 `MessageBus.Current.SendMessage` 改为 `_localEventBus.PublishAsync`
- **重构 GateIoControlService**：注入 `ILocalEventBus`，移除 3 个 MessageBus 订阅，1 处发布改为 `ILocalEventBus`
- **重构 WeighingMatchingService**：注入 `ILocalEventBus`，1 处 `MessageBus.Current.SendMessage<MatchSucceededMessage>` 改为 `_localEventBus.PublishAsync`
- **重构 TryMatchEventHandler**：1 处 `MessageBus.Current.SendMessage<MatchSucceededMessage>` 改为 `_localEventBus.PublishAsync`
- **新建 ViewModel 层桥接**：创建 ABP EventHandler 将 Common 层的 `ILocalEventBus` 事件中转到 `MessageBus`，使现有 ViewModel 订阅方式无需改动
- **清理旧 Message 类型**：所有旧 MessageBus Message 类在 Common→Common 通信路径被完全替代后，评估是否可以移除

## Capabilities

### New Capabilities

- `common-eventbus-migration`: 将 Common 层的 MessageBus 订阅和发布迁移到 ABP ILocalEventBus，包括新建 EventData 类、重构 6 个 Common 层文件的 MessageBus 使用、以及在 ViewModel 层建立 EventData→MessageBus 桥接

### Modified Capabilities

## Impact

- **受影响文件（Common 层）**：
  - `Common/Services/GateIoControlService.cs` — 移除 3 个订阅 + 1 个发布，注入 ILocalEventBus
  - `Common/Services/AttendedWeighingService.cs` — 移除 3 个订阅 + 6 个发布，改为 ILocalEventBus（已注入）
  - `Common/Services/Hikvision/HikvisionLprService.cs` — 移除 2 个发布，注入 ILocalEventBus
  - `Common/Services/Vzvision/VzvisionLprService.cs` — 移除 1 个发布，注入 ILocalEventBus
  - `Common/Services/WeighingMatchingService.cs` — 移除 1 个发布，注入 ILocalEventBus
  - `Common/Events/TryMatchEventHandler.cs` — 移除 1 个发布，注入 ILocalEventBus
- **新建文件**：
  - `Common/Events/LicensePlateRecognizedEventData.cs`
  - `Common/Events/StatusChangedEventData.cs`
  - `Common/Events/PlateNumberChangedEventData.cs`
  - `Common/Events/DeliveryTypeChangedEventData.cs`
  - `Common/Events/WeighingRecordCreatedEventData.cs`
  - `Common/Events/UpdatePlateNumberEventData.cs`
  - `Common/Events/MatchSucceededEventData.cs`
  - `Common/Events/SettingsSavedEventData.cs`
  - `Common/Events/GhostGateSessionResetEventData.cs`
  - ViewModel 层桥接 EventHandler（1-2 个文件）
- **不影响文件**：ViewModel 层的 `MessageBus` 订阅代码保持不变，通过桥接层保持现有行为
- **依赖**：ABP `ILocalEventBus`（已在项目中使用，无新增外部依赖）
- **业务功能**：车牌识别、称重状态同步、手动/自动匹对通知、设置更新传播等全部保留
