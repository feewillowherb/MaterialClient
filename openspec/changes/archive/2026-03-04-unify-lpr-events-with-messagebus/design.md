# 设计：使用 MessageBus 统一 LPR 事件

**变更 ID**：`unify-lpr-events-with-messagebus`
**作者**：Claude (AI Assistant)
**日期**：2026-01-29
**状态**：草稿

---

## 概述

本文档描述将车牌识别（LPR）事件从直接方法调用重构为 ReactiveUI MessageBus 的架构设计。该重构旨在提高解耦、可测试性，并与现有架构模式保持一致。

---

## 当前架构

### 事件流（重构前）

```
┌─────────────────────────┐
│  Hardware Device        │
│  (Hikvision/LprAllInOne)│
└───────────┬─────────────┘
            │ 1. HTTP Callback / SDK Event
            ▼
┌─────────────────────────────────────────────────────────┐
│  MinimalWebHostService                                  │
│  - HandleHikvisionLprCallback()                        │
│  - HandleLprAllInOneCallback()                         │
└───────────┬─────────────────────────────────────────────┘
            │ 2. Direct Service Call
            │    weighingService.OnPlateNumberRecognized()
            ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - OnPlateNumberRecognized()                           │
│  - License plate caching logic                         │
│  - Publishes PlateNumberChangedMessage via MessageBus  │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus Message
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - Update display       │
└─────────────────────────┘
```

### 问题

1. **紧耦合**：`MinimalWebHostService`（硬件层）直接依赖 `IAttendedWeighingService`（业务层）
2. **难以测试**：无法在脱离业务逻辑的情况下单独测试硬件回调逻辑
3. **扩展性有限**：新增订阅方（日志、监控）需修改回调处理代码
4. **架构不一致**：系统其余部分使用 MessageBus 做跨组件通信（ADR-009）

---

## 拟议架构

### 事件流（重构后）

```
┌─────────────────────────┐
│  Hardware Device        │
│  (Hikvision/LprAllInOne)│
└───────────┬─────────────┘
            │ 1. HTTP Callback / SDK Event
            ▼
┌─────────────────────────────────────────────────────────┐
│  MinimalWebHostService                                  │
│  - HandleHikvisionLprCallback()                        │
│  - HandleLprAllInOneCallback()                         │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ MessageBus.Current.SendMessage(                  │  │
│  │   new LicensePlateRecognizedMessage {           │  │
│  │     PlateNumber, ColorType, DeviceType, ... })   │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────┘
                             │ 2. MessageBus Message
                             │    (Decoupled communication)
                             ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - Subscribes to LicensePlateRecognizedMessage        │
│  - OnPlateNumberRecognized() [private method]         │
│  - License plate caching logic                         │
│  - Publishes PlateNumberChangedMessage via MessageBus  │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus Message
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - Update display       │
└─────────────────────────┘
```

### 收益

1. **松耦合**：硬件层仅依赖 MessageBus 消息，不依赖业务服务
2. **易测试**：可通过验证 MessageBus 消息来测试回调处理逻辑
3. **可扩展**：新增订阅方无需修改发布方
4. **一致**：与 ADR-009 及响应式编程模式一致

---

## 组件设计

### 1. LicensePlateRecognizedMessage

**用途**：统一承载所有 LPR 设备类型的识别数据

**位置**：`MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

**设计**：
```csharp
namespace MaterialClient.Common.Events;

/// <summary>
///     Message published when a license plate is recognized by any LPR device.
///     Sent via ReactiveUI MessageBus for decoupled event delivery.
/// </summary>
public class LicensePlateRecognizedMessage
{
    /// <summary>
    ///     The recognized license plate number (e.g., "京A12345")
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     Optional plate color type (e.g., 蓝色, 黄色, 绿色)
    /// </summary>
    public LprAllInOneColorType? ColorType { get; set; }

    /// <summary>
    ///     Device type that recognized the plate
    /// </summary>
    public LprDeviceType DeviceType { get; set; }

    /// <summary>
    ///     Human-readable device name (from configuration)
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp when recognition occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

**理由**：
- 包含硬件回调中的全部相关数据
- 设备类型与名称便于日志与诊断
- 时间戳保证事件顺序
- 颜色类型可选（非所有设备都支持）

---

### 2. 回调处理重构

**重构前**（海康示例）：
```csharp
// MaterialClient/Services/MinimalWebHostService.cs:188-200
private IActionResult HandleHikvisionLprCallback(LprCallbackData callback)
{
    var weighingService = _sharedServiceProvider
        .GetRequiredService<IAttendedWeighingService>();

    var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
    var colorType = callback?.AlarmInfoPlate?.Result?.PlateResult?.ColorType;

    if (!string.IsNullOrWhiteSpace(license))
    {
        // ❌ Direct dependency on business service
        weighingService.OnPlateNumberRecognized(license, colorType);
        return Results.Ok(new { result = 1 });
    }
}
```

**重构后**：
```csharp
private IActionResult HandleHikvisionLprCallback(LprCallbackData callback)
{
    // ❌ Remove: var weighingService = ...

    var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
    var colorType = callback?.AlarmInfoPlate?.Result?.PlateResult?.ColorType;

    if (!string.IsNullOrWhiteSpace(license))
    {
        // ✅ Publish decoupled message
        var message = new LicensePlateRecognizedMessage
        {
            PlateNumber = license,
            ColorType = colorType.HasValue
                ? (LprAllInOneColorType?)colorType.Value
                : null,
            DeviceType = LprDeviceType.Hikvision,
            DeviceName = callback?.AlarmInfoPlate?.DeviceName ?? "Unknown",
            Timestamp = DateTime.Now
        };

        MessageBus.Current.SendMessage(message);
        _logger.LogInformation("Hikvision LPR: {Plate}", license);

        return Results.Ok(new { result = 1 });
    }
}
```

**变更要点**：
- 移除对 `IAttendedWeighingService` 的依赖
- 构造并填充 `LicensePlateRecognizedMessage`
- 通过 `MessageBus.Current.SendMessage()` 发布
- 保留诊断用日志

---

### 3. 服务订阅模式

**设计**：
```csharp
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService(/* existing dependencies */)
    {
        // Subscribe to LPR recognition events from MessageBus
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(msg =>
            {
                _logger?.LogInformation(
                    "Received LPR event: {Plate} from {Device}",
                    msg.PlateNumber, msg.DeviceName);

                // Invoke existing processing logic
                OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
            });
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose subscription to prevent memory leaks
        _licensePlateSubscription?.Dispose();

        // ... existing disposal logic
    }

    // Changed from public to private (no longer part of interface)
    private void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)
    {
        // Existing implementation unchanged
        // License plate caching, recommendation logic, etc.
    }
}
```

**要点**：
- 在构造函数中创建订阅（单例服务）
- 复用现有 `OnPlateNumberRecognized()` 逻辑
- 方法可见性由 `public` 改为 `private`
- 在 `DisposeAsync()` 中释放订阅以防内存泄漏
- 增加诊断用日志

---

## 内存管理

### 订阅释放

**关键**：所有 MessageBus 订阅必须被释放，以防内存泄漏。

**模式**：
```csharp
public class AttendedWeighingService : IAsyncDisposable
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService()
    {
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(/* handler */);
    }

    public async ValueTask DisposeAsync()
    {
        // ✅ Always dispose subscriptions
        _licensePlateSubscription?.Dispose();
    }
}
```

**防内存泄漏**：
1. 将订阅引用保存在字段中
2. 在 `DisposeAsync()` 或 `Dispose()` 中释放
3. ViewModel 订阅使用 `DisposeWith()` 模式
4. 在长运行场景（1000+ 次循环）下测试

---

## 错误处理

### 回调处理错误

**策略**：记录日志并继续，不让单条错误消息影响整体

```csharp
try
{
    var message = new LicensePlateRecognizedMessage { /* ... */ };
    MessageBus.Current.SendMessage(message);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process LPR callback from {Device}", deviceName);
    // Return success to hardware device (don't retry)
    return Results.Ok(new { result = 0, error = "Processing failed" });
}
```

### 订阅端错误

**策略**：在订阅处理中记录日志但不抛出

```csharp
MessageBus.Current
    .Listen<LicensePlateRecognizedMessage>()
    .Subscribe(msg =>
    {
        try
        {
            OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process LPR message: {Plate}",
                msg.PlateNumber);
        }
    });
```

---

## 测试策略

### 单元测试

**1. 消息发布**：
```csharp
[Fact]
public void HandleHikvisionLprCallback_ShouldPublishCorrectMessage()
{
    // Arrange
    var messages = new List<LicensePlateRecognizedMessage>();
    using var subscription = MessageBus.Current
        .Listen<LicensePlateRecognizedMessage>()
        .Subscribe(messages.Add);

    var callback = new LprCallbackData
    {
        AlarmInfoPlate = new AlarmInfoPlate
        {
            Result = new PlateResult { License = "京A12345" },
            DeviceName = "Hikvision-LPR-1"
        }
    };

    // Act
    var result = _service.HandleHikvisionLprCallback(callback);

    // Assert
    Assert.Single(messages);
    Assert.Equal("京A12345", messages[0].PlateNumber);
    Assert.Equal(LprDeviceType.Hikvision, messages[0].DeviceType);
}
```

**2. 消息订阅**：
```csharp
[Fact]
public void AttendedWeighingService_ShouldSubscribeToLprMessages()
{
    // Arrange
    var service = new AttendedWeighingService(/* mocks */);
    var plateNumbers = new List<string?>();

    // Act
    MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
    {
        PlateNumber = "京A12345"
    });

    var mostFrequent = service.GetMostFrequentPlateNumber();

    // Assert
    Assert.Equal("京A12345", mostFrequent);
}
```

### 集成测试

**端到端流程**：
```csharp
[Fact]
public async Task LprEventFlow_ShouldUpdateUiCorrectly()
{
    // Arrange: Setup complete system with mocked hardware
    var (service, viewModel) = CreateSystem();

    // Act: Simulate hardware callback
    SimulateHikvisionCallback("京A12345");

    // Assert: Verify service state
    Assert.Equal("京A12345", service.GetMostFrequentPlateNumber());

    // Assert: Verify UI updated
    Assert.Equal("京A12345", viewModel.MostFrequentPlateNumber);
}
```

### 内存泄漏测试

**长运行场景**：
```csharp
[Fact]
public void RepeatedLprMessages_ShouldNotLeakMemory()
{
    // Arrange
    var service = new AttendedWeighingService(/* mocks */);
    var initialMemory = GC.GetTotalMemory(true);

    // Act: Send 1000 messages
    for (int i = 0; i < 1000; i++)
    {
        MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
        {
            PlateNumber = $"京A{i:00000}"
        });
    }

    // Cleanup
    service.Dispose();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // Assert: Memory growth should be minimal
    var finalMemory = GC.GetTotalMemory(true);
    var growth = finalMemory - initialMemory;
    Assert.True(growth < 1024 * 1024, $"Memory grew {growth} bytes");
}
```

---

## 迁移策略

### 阶段 1：准备（无破坏性变更）
1. 创建 `LicensePlateRecognizedMessage` 类
2. 在 `AttendedWeighingService` 中增加 MessageBus 订阅，与现有方法并存
3. 保留接口中的 `OnPlateNumberRecognized()`（双模式）

### 阶段 2：迁移发布方
4. 将 `MinimalWebHostService` 的回调处理重构为使用 MessageBus
5. 可选：增加特性开关在直接调用与 MessageBus 间切换
6. 并行测试两种模式

### 阶段 3：移除旧路径
7. 从回调处理中移除直接服务调用
8. 从公开接口中移除 `OnPlateNumberRecognized()`
9. 在实现中将该方法改为 private
10. 更新测试与文档

### 回滚计划

若出现问题：
1. 将回调处理回退为直接调用
2. 保留 MessageBus 订阅作为额外监听（无害）
3. 排查并修复
4. 再次尝试迁移

---

## 权衡与替代方案

### 替代 1：使用 ABP LocalEventBus

**未采用原因**：
- 相比 MessageBus 增加 5–10ms 延迟（MessageBus <1ms）
- 对简单 UI 通知过重
- 需要额外 `IEventHandler` 类
- 对高频事件（10–20 次/分钟 LPR）不如 MessageBus 合适

**适用场景**：更适合异步领域事件（如涉及数据库的 `TryMatchEvent`）

### 替代 2：保留直接调用

**未采用原因**：
- 层间紧耦合
- 难以单独测试
- 与其余架构不一致
- 无法支持多订阅方

### 替代 3：在服务中使用 Rx Observable

**未采用原因**：
- `IHikvisionLprService` 已有 `IObservable<LicensePlateRecognizedEvent>`
- 但 `MinimalWebHostService` 位于 SDK 与服务之间
- 需让 `MinimalWebHostService` 实现 Observable 模式
- 对本用例 MessageBus 更简单

---

## 影响分析

### 破坏性变更

**公开接口**：
- 移除 `IAttendedWeighingService.OnPlateNumberRecognized()`
- 影响：低（预计无外部调用方）

**内部实现**：
- 回调处理签名不变
- `OnPlateNumberRecognized()` 逻辑不变
- 影响：无

### 性能

- **重构前**：直接方法调用（约 0.1ms）
- **重构后**：MessageBus 发布 + 订阅（约 0.5ms）
- **影响**：对 LPR 频率（10–20 次/分钟）可忽略

### 兼容性

- 若暂时保留 `OnPlateNumberRecognized()` 则向后兼容
- 与新的订阅方模式向前兼容
- 无需数据库或 API 变更

---

## 待决问题

1. **是否弃用 `LicensePlateRecognizedEvent`（ABP 事件）？**
   - 当前未使用
   - 建议：标记 `[Obsolete]`，后续清理时移除

2. **过渡期是否同时支持 MessageBus 与直接调用？**
   - 取决于部署风险偏好
   - 建议：充分测试后一次性切换（更简单）

3. **是否增加按设备区分的消息类？**
   - 当前设计：单一 `LicensePlateRecognizedMessage`，用 `DeviceType` 区分
   - 替代：`HikvisionLprMessage`、`LprAllInOneMessage` 等
   - 建议：单一消息即可，减少重复

---

## 参考

- **ADR-009**：跨组件通信使用 MessageBus
- **响应式模式**：`openspec/docs/timer-to-rx-pattern.md`
- **相关变更**：`hikvision-lpr-implementation`、`hikvision-lpr-integration`
- **规格**：`openspec/specs/license-plate-recognition`
