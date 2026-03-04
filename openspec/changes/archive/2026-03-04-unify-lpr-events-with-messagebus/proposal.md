# 变更：使用 MessageBus 统一 LPR 车牌识别事件

**变更 ID**：`unify-lpr-events-with-messagebus`
**状态**：草稿
**创建日期**：2026-01-29
**类型**：重构

---

## 原因

### 背景

MaterialClient 与多种车牌识别（LPR）硬件集成：
- **海康 LPR 设备** —— 使用 HCNetSDK（在变更 `hikvision-lpr-implementation` 中实现）
- **LprAllInOne 设备** —— 使用 HTTP 回调端点
- **华夏智信设备** —— 使用 HTTP 回调端点

当前这些设备识别到车牌时，`MinimalWebHostService` 中的回调处理会直接调用 `IAttendedWeighingService.OnPlateNumberRecognized()`，导致硬件集成层与业务逻辑层紧耦合。

### 问题

1. **紧耦合**：硬件回调处理（`MinimalWebHostService`）直接依赖业务服务（`IAttendedWeighingService`），违反依赖倒置原则

2. **扩展性有限**：无法在不改调用代码的情况下支持 LPR 事件的多订阅方（如日志、统计、告警）

3. **测试困难**：难以在脱离业务逻辑的情况下单独测试硬件回调逻辑

4. **架构不一致**：应用其余部分使用 ReactiveUI MessageBus 做跨组件通信（ADR-009），而 LPR 事件仍用直接方法调用

5. **事件模式混杂**：定义了 `LicensePlateRecognizedEvent`（ABP 事件）却未使用，内部又使用 `PlateNumberChangedMessage`（MessageBus），造成混淆

---

## 变更内容

### 概述

将 LPR 车牌识别事件投递重构为统一使用 ReactiveUI MessageBus。硬件回调处理改为向总线发布 `LicensePlateRecognizedMessage`，`AttendedWeighingService` 通过订阅接收并处理这些事件。从而解耦硬件集成与业务逻辑，并与 ADR-009 架构一致。

### 具体变更

#### 1. 创建统一 MessageBus 消息（新增）

创建 `LicensePlateRecognizedMessage` 类，承载完整 LPR 识别数据：

```csharp
namespace MaterialClient.Common.Events;

public class LicensePlateRecognizedMessage
{
    public string PlateNumber { get; set; }
    public LprAllInOneColorType? ColorType { get; set; }
    public LprDeviceType DeviceType { get; set; }
    public string DeviceName { get; set; }
    public DateTime Timestamp { get; set; }
}
```

用于替代当前未实际使用的 `LicensePlateRecognizedEvent`（ABP 事件）。

#### 2. 重构 MinimalWebHostService（修改）

移除对 `IAttendedWeighingService` 的直接依赖：

**当前代码**：
```csharp
// MaterialClient/Services/MinimalWebHostService.cs:188-200
var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();
weighingService.OnPlateNumberRecognized(license, colorType);
```

**新代码**：
```csharp
// Publish to MessageBus instead
var message = new LicensePlateRecognizedMessage
{
    PlateNumber = license,
    ColorType = colorType,
    DeviceType = LprDeviceType.LprAllInOne,
    DeviceName = deviceName,
    Timestamp = DateTime.Now
};
MessageBus.Current.SendMessage(message);
```

对海康与华夏智信的回调处理采用相同模式。

#### 3. 修改 AttendedWeighingService（修改）

增加 MessageBus 订阅以接收 LPR 事件：

```csharp
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService(/* existing deps */)
    {
        // Subscribe to LPR recognition events
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(msg => OnPlateNumberRecognized(
                msg.PlateNumber,
                msg.ColorType
            ));
    }

    public async ValueTask DisposeAsync()
    {
        _licensePlateSubscription?.Dispose();
        // ... existing disposal logic
    }
}
```

现有 `OnPlateNumberRecognized()` 的实现保持不变。

#### 4. 更新服务接口（修改）

从 `IAttendedWeighingService` 接口中移除：
```csharp
void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);
```

该方法变为私有/内部实现细节，不再属于公开接口，仅通过 MessageBus 订阅在内部调用。

#### 5. 文档更新（新增）

创建或更新文档，说明：
- 硬件事件使用 MessageBus 的指南
- MessageBus（实时 UI 事件）与 LocalEventBus（异步领域事件）的差异
- MessageBus 订阅的防内存泄漏要求

---

## 影响

### 预期收益

1. **松耦合**：硬件层不再依赖业务服务层，便于测试与模块化

2. **架构一致**：与 ADR-009（跨组件通信使用 MessageBus）及 `timer-to-rx-pattern.md` 中的响应式模式一致

3. **可扩展**：易于新增订阅方（日志、统计、监控）而无需修改现有代码

4. **测试简化**：可通过发送/接收 MessageBus 消息分别测试硬件回调和业务处理

5. **实时性能**：MessageBus 同步投递（<1ms 延迟），适合高频 LPR 事件

6. **清晰**：消除未使用的 `LicensePlateRecognizedEvent`（ABP 事件）与实际消息机制之间的混淆

### 风险与缓解

| 风险 | 影响 | 缓解 |
|------|--------|------------|
| 未释放订阅导致内存泄漏 | 高 | 采用 `DisposeWith()` 模式，在 `DisposeAsync()` 中释放订阅，增加内存泄漏测试 |
| 破坏现有集成 | 中 | 保持 `OnPlateNumberRecognized()` 方法签名不变，仅改变调用方式 |
| LPR 功能回归 | 中 | 对所有设备类型（海康、LprAllInOne、华夏智信）做完整集成测试 |
| MessageBus 性能开销 | 低 | MessageBus 同步且轻量，开销可忽略 |

---

## 成功标准

- [x] 创建 `LicensePlateRecognizedMessage` 类并包含全部所需属性
- [ ] `MinimalWebHostService` 的回调处理改为发布 `LicensePlateRecognizedMessage`，不再调用 `weighingService.OnPlateNumberRecognized()`
- [ ] `AttendedWeighingService` 在构造函数中订阅 `LicensePlateRecognizedMessage`
- [ ] 从 `IAttendedWeighingService` 接口中移除 `OnPlateNumberRecognized()` 方法（改为 private）
- [ ] 重构后所有 LPR 设备类型（海康、LprAllInOne、华夏智信）工作正常
- [ ] 在 `DisposeAsync()` 中正确释放 MessageBus 订阅
- [ ] 消息发布与订阅的单元测试通过
- [ ] 所有 LPR 设备类型的集成测试通过
- [ ] 内存泄漏测试未发现与订阅相关的泄漏
- [ ] 文档已更新 MessageBus 使用指南

---

## 后续步骤

1. **评审并批准提案**：与团队评审本提案，确认架构方案
2. **编写设计文档**：编写详细 `design.md`，说明重构方式与迁移策略
3. **编写规格增量**：在 `license-plate-recognition` 规格中补充修改后的需求
4. **实现消息类**：创建带完整 XML 文档的 `LicensePlateRecognizedMessage`
5. **重构回调处理**：修改 `MinimalWebHostService`，改为发布消息而非直接调用
6. **在 AttendedWeighingService 中增加订阅**：实现 MessageBus 订阅与释放
7. **更新服务接口**：从公开接口中移除 `OnPlateNumberRecognized()`
8. **编写测试**：为基于消息的 LPR 事件流编写单元与集成测试
9. **运行内存泄漏测试**：确认无订阅相关内存泄漏
10. **更新文档**：记录 MessageBus 使用模式与指南
11. **归档旧事件**：移除或弃用未使用的 `LicensePlateRecognizedEvent`（ABP 事件）

---

## 参考

- **ADR-009**：`docs/SDD.md:1654-1693` —— 跨组件通信使用 MessageBus
- **响应式模式**：`openspec/docs/timer-to-rx-pattern.md` —— 系统中的响应式编程模式
- **相关变更**：
  - `hikvision-lpr-implementation` —— 海康 LPR 服务实现
  - `hikvision-lpr-integration` —— 海康 LPR 配置与 UI
- **现有事件**：
  - `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs` —— 未使用的 ABP 事件（待弃用）
  - `MaterialClient.Common/Events/PlateNumberChangedMessage.cs` —— 内部 UI 通知消息
- **规格**：`openspec/specs/license-plate-recognition` —— 车牌识别需求
