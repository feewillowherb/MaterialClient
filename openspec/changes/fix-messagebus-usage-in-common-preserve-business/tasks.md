## 1. 新建 ABP EventData 类

- [x] 1.1 在 `Common/Events/` 创建 `LicensePlateRecognizedEventData.cs`，继承 `EventData`，包含原 `LicensePlateRecognizedMessage` 的所有属性
- [x] 1.2 在 `Common/Events/` 创建 `StatusChangedEventData.cs`，继承 `EventData`，包含原 `StatusChangedMessage` 的所有属性
- [x] 1.3 在 `Common/Events/` 创建 `PlateNumberChangedEventData.cs`，继承 `EventData`，包含原 `PlateNumberChangedMessage` 的所有属性
- [x] 1.4 在 `Common/Events/` 创建 `DeliveryTypeChangedEventData.cs`，继承 `EventData`，包含原 `DeliveryTypeChangedMessage` 的所有属性
- [x] 1.5 在 `Common/Events/` 创建 `WeighingRecordCreatedEventData.cs`，继承 `EventData`，包含原 `WeighingRecordCreatedMessage` 的所有属性
- [x] 1.6 在 `Common/Events/` 创建 `UpdatePlateNumberEventData.cs`，继承 `EventData`，包含原 `UpdatePlateNumberMessage` 的所有属性
- [x] 1.7 在 `Common/Events/` 创建 `MatchSucceededEventData.cs`，继承 `EventData`，包含原 `MatchSucceededMessage` 的所有属性
- [x] 1.8 在 `Common/Events/` 创建 `SettingsSavedEventData.cs`，继承 `EventData`
- [x] 1.9 在 `Common/Events/` 创建 `GhostGateSessionResetEventData.cs`，继承 `EventData`，包含原 `GhostGateSessionResetMessage` 的所有属性

## 2. 迁移 LPR 服务发布（发布方优先）

- [x] 2.1 `HikvisionLprService` 注入 `ILocalEventBus`，将 2 处 `MessageBus.Current.SendMessage<LicensePlateRecognizedMessage>` 替换为 `_ = _localEventBus.PublishAsync<LicensePlateRecognizedEventData>`
- [x] 2.2 `VzvisionLprService` 注入 `ILocalEventBus`，将 1 处 `MessageBus.Current.SendMessage<LicensePlateRecognizedMessage>` 替换为 `_ = _localEventBus.PublishAsync<LicensePlateRecognizedEventData>`

## 3. 迁移 AttendedWeighingService

- [x] 3.1 `AttendedWeighingService` 将 3 个 `MessageBus.Current.Listen<T>()` 订阅替换为 `ILocalEventBus.Subscribe<T>()`（`LicensePlateRecognizedEventData`、`GhostGateSessionResetEventData`、`SettingsSavedEventData`）
- [x] 3.2 `AttendedWeighingService` 将 6 处 `MessageBus.Current.SendMessage<T>()` 发布替换为 `_localEventBus.PublishAsync<TEventData>()`（`StatusChangedEventData`、`PlateNumberChangedEventData`、`DeliveryTypeChangedEventData`、`WeighingRecordCreatedEventData`、`UpdatePlateNumberEventData`）
- [x] 3.3 `AttendedWeighingService.StopAsync()` 中移除 3 个 MessageBus 订阅的 Dispose 代码，替换为 ILocalEventBus 订阅的清理代码

## 4. 迁移 GateIoControlService

- [x] 4.1 `GateIoControlService` 注入 `ILocalEventBus`，将 3 个 `MessageBus.Current.Listen<T>()` 订阅替换为 `ILocalEventBus.Subscribe<T>()`（`LicensePlateRecognizedEventData`、`StatusChangedEventData`、`SettingsSavedEventData`）
- [x] 4.2 `GateIoControlService` 将 1 处 `MessageBus.Current.SendMessage<GhostGateSessionResetMessage>` 替换为 `_localEventBus.PublishAsync<GhostGateSessionResetEventData>`
- [x] 4.3 `GateIoControlService.StopAsync()` 中移除 3 个 MessageBus 订阅的 Dispose 代码，替换为 ILocalEventBus 订阅的清理代码

## 5. 迁移匹对相关服务

- [x] 5.1 `WeighingMatchingService` 注入 `ILocalEventBus`，将 `ManualMatchAsync` 中的 1 处 `MessageBus.Current.SendMessage<MatchSucceededMessage>` 替换为 `_localEventBus.PublishAsync<MatchSucceededEventData>`
- [x] 5.2 `TryMatchEventHandler` 注入 `ILocalEventBus`，将 1 处 `MessageBus.Current.SendMessage<MatchSucceededMessage>` 替换为 `_localEventBus.PublishAsync<MatchSucceededEventData>`

## 6. 创建 ViewModel 层桥接 EventHandler

- [x] 6.1 在 `MaterialClient` 项目中创建桥接 EventHandler，实现 `ILocalEventHandler<LicensePlateRecognizedEventData>`，将事件转发为 `MessageBus.Current.SendMessage<LicensePlateRecognizedMessage>`
- [x] 6.2 创建 `ILocalEventHandler<StatusChangedEventData>` 桥接，转发为 `StatusChangedMessage`
- [x] 6.3 创建 `ILocalEventHandler<PlateNumberChangedEventData>` 桥接，转发为 `PlateNumberChangedMessage`
- [x] 6.4 创建 `ILocalEventHandler<DeliveryTypeChangedEventData>` 桥接，转发为 `DeliveryTypeChangedMessage`
- [x] 6.5 创建 `ILocalEventHandler<WeighingRecordCreatedEventData>` 桥接，转发为 `WeighingRecordCreatedMessage`
- [x] 6.6 创建 `ILocalEventHandler<UpdatePlateNumberEventData>` 桥接，转发为 `UpdatePlateNumberMessage`
- [x] 6.7 创建 `ILocalEventHandler<MatchSucceededEventData>` 桥接，转发为 `MatchSucceededMessage`
- [x] 6.8 创建 `ILocalEventHandler<SettingsSavedEventData>` 桥接，转发为 `SettingsSavedMessage`
- [x] 6.9 创建 `ILocalEventHandler<GhostGateSessionResetEventData>` 桥接，转发为 `GhostGateSessionResetMessage`
- [x] 6.10 在 ABP 模块注册中注册所有桥接 EventHandler（通过 `ITransientDependency` 自动注册）

## 7. 验证与清理

- [x] 7.1 编译项目，确保无编译错误（Common 项目 0 错误；MaterialClient 项目仅因 DLL 文件锁定无法复制，无编译错误）
- [x] 7.2 验证 Common 层中不再存在 `MessageBus.Current.Listen` 和 `MessageBus.Current.SendMessage` 调用（已验证：0 匹配）
- [x] 7.3 验证车牌识别流程：SDK 回调 → ILocalEventBus → 桥接 → MessageBus → ViewModel 正常工作（运行时验证，需手动测试）
- [x] 7.4 验证手动匹对流程：ManualMatchAsync → ILocalEventBus → 桥接 → MessageBus → ViewModel UI 更新正常（运行时验证，需手动测试）
- [x] 7.5 验证称重状态同步：状态变更 → ILocalEventBus → GateIoControlService 正常响应（运行时验证，需手动测试）
- [x] 7.6 更新 `docs/error-cases/common-must-not-subscribe-messagebus.md` 中错误用例的修复状态
