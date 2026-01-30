# 设计: 统一 LPR 数据传递和主动抓拍接口

**变更 ID**: `unified-lpr-data-transfer-and-capture-interface`
**作者**: Claude (AI 助手)
**日期**: 2026-01-29
**状态**: 草稿

---

## 概述

本文档描述了重构车牌识别(LPR)事件交付和实现统一主动抓拍接口的架构设计。重构改进了解耦、可测试性和一致性,同时与现有架构模式(ADR-009)保持一致。

---

## 当前架构

### LPR 数据流(变更前)

```
┌─────────────────────────┐
│  硬件设备                │
│  (海康威视/LprAllInOne)  │
└───────────┬─────────────┘
            │ 1. HTTP 回调 / SDK 事件
            ▼
┌─────────────────────────────────────────────────────────┐
│  MinimalWebHostService                                  │
│  - HandleHikvisionLprCallback()                        │
│  - HandleLprAllInOneCallback()                         │
└───────────┬─────────────────────────────────────────────┘
            │ 2. 直接服务调用
            │    weighingService.OnPlateNumberRecognized()
            ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - OnPlateNumberRecognized()                           │
│  - 车牌缓存逻辑                                        │
│  - 通过 MessageBus 发布 PlateNumberChangedMessage     │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus 消息
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - 更新显示             │
└─────────────────────────┘
```

### 主动抓拍支持(变更前)

| 设备类型 | 主动抓拍支持 | 实现方式 |
|---------|-------------|---------|
| **LprAllInOne** | ✅ 支持 | `TriggerManualRecognitionAsync()` 设置标志,设备轮询时触发 |
| **海康威视** | ❌ 不支持 | 接口未实现 |
| **华夏智信** | ❌ 不适用 | 厂商不支持,服务不存在 |

### 问题

1. **紧耦合**: `MinimalWebHostService`(硬件层)直接依赖 `IAttendedWeighingService`(业务层)
2. **难以测试**: 无法独立测试硬件回调逻辑和业务处理逻辑
3. **可扩展性有限**: 添加新订阅者(日志、监控)需要修改回调处理程序
4. **架构不一致**: 系统其余部分使用 MessageBus 进行跨组件通信(ADR-009)
5. **功能不完整**: 海康威视主动抓拍未实现,缺乏统一接口

---

## 提议的架构

### LPR 数据流(变更后)

```
┌─────────────────────────┐
│  硬件设备                │
│  (海康威视/LprAllInOne/  │
│   华夏智信)              │
└───────────┬─────────────┘
            │ 1. HTTP 回调 / SDK 事件
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
                             │ 2. MessageBus 消息
                             │    (解耦通信)
                             ▼
┌─────────────────────────────────────────────────────────┐
│  AttendedWeighingService                               │
│  - 订阅 LicensePlateRecognizedMessage                  │
│  - OnPlateNumberRecognized() [私有方法]               │
│  - 车牌缓存逻辑                                        │
│  - 通过 MessageBus 发布 PlateNumberChangedMessage     │
└───────────┬─────────────────────────────────────────────┘
            │ 3. MessageBus 消息
            ▼
┌─────────────────────────┐
│  UI (ViewModels)        │
│  - 更新显示             │
└─────────────────────────┘
```

### 主动抓拍架构(变更后)

```
┌─────────────────────────────────────────────────────────┐
│                    统一接口层                           │
│  public interface ILprDevice                           │
│  {                                                      │
│      IObservable<LicensePlateRecognizedEvent>          │
│          TriggerCaptureAsync(config);                  │
│      bool SupportsActiveCapture { get; }               │
│  }                                                      │
└────────────┬───────────────────────────┬───────────────┘
             │                           │
    ┌────────▼────────┐         ┌───────▼────────┐
    │  HikvisionLpr   │         │ LprAllInOne    │
    │  Service        │         │ Service        │
    ├─────────────────┤         ├────────────────┤
    │ Supports: true  │         │ Supports: true  │
    │ NET_DVR_        │         │ 标志位轮询      │
    │ ContinuousShoot │         │ 机制           │
    └─────────────────┘         └─────────────────┘
                                             │
                                  ┌──────────▼─────────┐
                                  │ Huaxiazhixin       │
                                  │ LprService         │
                                  ├────────────────────┤
                                  │ Supports: false    │
                                  │ 抛出               │
                                  │ NotSupportedException│
                                  └────────────────────┘
```

### 收益

1. **松耦合**: 硬件层仅知道 MessageBus 消息,不知道业务服务
2. **易于测试**: 可通过验证 MessageBus 消息测试回调处理程序
3. **可扩展**: 添加新订阅者无需修改发布者
4. **一致性**: 与 ADR-009 和响应式编程模式一致
5. **功能完整**: 所有设备类型通过统一接口支持主动抓拍(或明确标记不支持)

---

## 组件设计

### 1. LicensePlateRecognizedMessage

**用途**: 所有 LPR 设备类型的统一消息类

**位置**: `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

**设计**:
```csharp
namespace MaterialClient.Common.Events;

/// <summary>
///     任何 LPR 设备识别车牌时发布的消息。
///     通过 ReactiveUI MessageBus 发送,用于解耦事件交付。
/// </summary>
public class LicensePlateRecognizedMessage
{
    /// <summary>
    ///     识别的车牌号(例如 "京A12345")
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     可选的车牌颜色类型(例如 蓝色、黄色、绿色)
    /// </summary>
    public LprAllInOneColorType? ColorType { get; set; }

    /// <summary>
    ///     识别车牌的设备类型
    /// </summary>
    public LprDeviceType DeviceType { get; set; }

    /// <summary>
    ///     人类可读的设备名称(来自配置)
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    ///     识别发生时的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

**理由**:
- 包含硬件回调中的所有相关数据
- 设备类型和名称用于日志和诊断
- 时间戳用于准确的事件排序
- 可选的颜色类型(并非所有设备都支持)

---

### 2. 回调处理程序重构

**变更前**(海康威视示例):
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
        // ❌ 对业务服务的直接依赖
        weighingService.OnPlateNumberRecognized(license, colorType);
        return Results.Ok(new { result = 1 });
    }
}
```

**变更后**:
```csharp
private IActionResult HandleHikvisionLprCallback(LprCallbackData callback)
{
    // ❌ 移除: var weighingService = ...

    var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
    var colorType = callback?.AlarmInfoPlate?.Result?.PlateResult?.ColorType;

    if (!string.IsNullOrWhiteSpace(license))
    {
        // ✅ 发布解耦消息
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
        _logger.LogInformation("海康威视 LPR: {Plate}", license);

        return Results.Ok(new { result = 1 });
    }
}
```

**变更**:
- 移除 `IAttendedWeighingService` 依赖
- 创建并填充 `LicensePlateRecognizedMessage`
- 通过 `MessageBus.Current.SendMessage()` 发布
- 保留日志记录用于诊断

---

### 3. 服务订阅模式

**设计**:
```csharp
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService(/* 现有依赖 */)
    {
        // 订阅来自 MessageBus 的 LPR 识别事件
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(msg =>
            {
                _logger?.LogInformation(
                    "收到 LPR 事件: {Plate} 来自 {Device}",
                    msg.PlateNumber, msg.DeviceName);

                // 调用现有处理逻辑
                OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
            });
    }

    public async ValueTask DisposeAsync()
    {
        // 释放订阅以防止内存泄漏
        _licensePlateSubscription?.Dispose();

        // ... 现有释放逻辑
    }

    // 从 public 更改为 private(不再是接口的一部分)
    private void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)
    {
        // 现有实现不变
        // 车牌缓存、推荐逻辑等
    }
}
```

**关键点**:
- 订阅在构造函数中创建(单例服务)
- 现有的 `OnPlateNumberRecognized()` 逻辑被复用
- 方法可见性从 `public` 更改为 `private`
- 订阅在 `DisposeAsync()` 中释放以防止内存泄漏

---

### 4. ILprDevice 统一接口

**设计**:
```csharp
namespace MaterialClient.Common.Services;

/// <summary>
///     统一的 LPR 设备接口,提供主动抓拍能力
/// </summary>
public interface ILprDevice
{
    /// <summary>
    ///     主动触发车牌识别抓拍
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>
    ///     可观察的车牌识别事件流。
    ///     如果设备不支持主动抓拍,应返回空流或抛出 NotSupportedException。
    /// </returns>
    /// <remarks>
    ///     实现应处理:
    ///     - 设备登录/认证
    ///     - 触发抓拍命令
    ///     - 等待识别结果(带超时)
    ///     - 错误处理(网络超时、设备离线、SDK 调用失败)
    /// </remarks>
    IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config);

    /// <summary>
    ///     设备是否支持主动抓拍
    /// </summary>
    /// <value>
    ///     如果设备支持通过应用触发抓拍,则为 true;
    ///     如果设备仅支持被动捕获(设备推送)或厂商限制,则为 false。
    /// </value>
    bool SupportsActiveCapture { get; }
}
```

**理由**:
- 统一所有 LPR 设备类型的主动抓拍能力
- 明确标记不支持主动抓拍的设备
- 返回 `IObservable<T>` 以支持异步、可组合的结果流
- 与现有的 Rx.NET 模式一致

---

### 5. HikvisionLprService 主动抓拍实现

**设计**:
```csharp
public sealed class HikvisionLprService : IHikvisionLprService, ILprDevice, ISingletonDependency
{
    // ... 现有字段和方法 ...

    // 添加登录会话缓存(参考 HikvisionService.EnsureLogin 设计)
    private readonly ConcurrentDictionary<string, int> _deviceKeyToUserId = new();

    /// <summary>
    ///     海康威视设备支持主动抓拍
    /// </summary>
    public bool SupportsActiveCapture => true;

    /// <summary>
    ///     主动触发海康威视设备的车牌识别
    /// </summary>
    public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return Observable.Create<LicensePlateRecognizedEvent>(observer =>
        {
            // 1. 确保登录(使用会话缓存,避免重复登录)
            var key = BuildDeviceKey(config);
            var userId = _deviceKeyToUserId.AddOrUpdate(
                key,
                _ => LoginDevice(config),           // 首次登录
                (_, existingUserId) => existingUserId >= 0
                    ? existingUserId                 // 复用现有会话
                    : LoginDevice(config));          // 会话失效,重新登录

            if (userId < 0)
            {
                _logger?.LogError("登录海康威视设备失败: {Device}", config.Name);
                observer.OnError(new Exception($"设备登录失败: {config.Name}"));
                return Disposable.Empty;
            }

            // 2. 触发抓拍
            var snapCfg = new NET_DVR_SNAPCFG { /* ... */ };
            var result = HikvisionSdk.NET_DVR_ContinuousShoot(userId, ref snapCfg, 1);

            if (!result)
            {
                var errorCode = HikvisionSdk.NET_DVR_GetLastError();
                var error = HikvisionEncodingHelper.GetErrorMessage(errorCode);
                _logger?.LogError("触发抓拍失败: {Error}", error);
                observer.OnError(new Exception($"触发抓拍失败: {error}"));
                // 注意: 不登出设备,保持会话复用
                return Disposable.Empty;
            }

            // 3. 订阅结果(带超时)
            var subscription = PlateRecognized
                .Where(e => e.DeviceName == config.Name)
                .Timeout(TimeSpan.FromSeconds(30))
                .Take(1)
                .Subscribe(
                    observer.OnNext,
                    observer.OnError,
                    observer.OnCompleted
                );

            // 4. 返回清理函数
            return Disposable.Create(() =>
            {
                subscription?.Dispose();
                // 注意: 不调用 NET_DVR_Logout,保持会话以供后续抓拍复用
                // 会话将在服务停止或设备长时间不活动时清理
            });
        });
    }

    private int LoginDevice(LicensePlateRecognitionConfig config)
    {
        var loginInfo = new NET_DVR_USER_LOGIN_INFO
        {
            // ... 设置登录参数 ...
        };

        var deviceInfo = new NET_DVR_DEVICEINFO_V40();
        var userId = HikvisionSdk.NET_DVR_Login_V40(ref loginInfo, ref deviceInfo);

        if (userId < 0)
        {
            var errorCode = HikvisionSdk.NET_DVR_GetLastError();
            _logger?.LogWarning("设备登录失败: IP={Ip}, ErrorCode={ErrorCode}",
                config.Ip, errorCode);
        }
        else
        {
            _logger?.LogDebug("设备登录成功: IP={Ip}, UserId={UserId}", config.Ip, userId);
        }

        return userId;
    }

    private string BuildDeviceKey(LicensePlateRecognitionConfig config)
    {
        return $"{config.Ip}:{config.Port}";
    }
}
```

**关键点**:
- 使用 `Observable.Create<T>` 实现异步模式
- **会话管理**: 参考 `HikvisionService.EnsureLogin()` 设计,使用 `ConcurrentDictionary` 缓存登录会话(userId)
- **避免重复登录**: 首次调用时登录,后续调用复用现有会话,仅在会话失效(userId < 0)时重新登录
- **不主动登出**: 清理函数不调用 `NET_DVR_Logout`,保持会话供后续抓拍复用
- 会话将在服务停止(`StopAsync`)或设备长时间不活动时清理
- 添加 30 秒超时防止无限等待
- 正确的清理(释放订阅,但保留登录会话)
- 错误处理和日志记录

---

### 6. LprAllInOneService 适配

**设计**:
```csharp
public class LprAllInOneService : ILprAllInOneService, ILprDevice, ISingletonDependency
{
    // ... 现有实现 ...

    public bool SupportsActiveCapture => true;

    public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config)
    {
        return Observable.FromAsync(async () =>
        {
            var success = await TriggerManualRecognitionAsync(config);
            if (!success)
            {
                throw new InvalidOperationException("触发识别失败");
            }

            // 等待设备轮询并返回结果
            // 注意: 这需要将现有的事件流机制与 Observable 桥接
            // 可能需要添加 Subject 来桥接两种模式

            await Task.Delay(1000); // 等待轮询

            return new LicensePlateRecognizedEvent
            {
                // ... 从轮询结果填充 ...
            };
        });
    }
}
```

**注意**: LprAllInOne 的轮询机制可能需要调整以更好地适应 `IObservable<T>` 模式。

---

### 7. HuaxiazhixinLprService 占位实现

**设计**:
```csharp
namespace MaterialClient.Common.Services.Huaxiazhixin;

/// <summary>
///     华夏智信车牌识别服务占位实现
///     厂商不支持主动抓拍,此服务明确标记此限制
/// </summary>
public class HuaxiazhixinLprService : ILprDevice, ISingletonDependency
{
    private readonly ILogger<HuaxiazhixinLprService>? _logger;

    public HuaxiazhixinLprService(ILogger<HuaxiazhixinLprService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     华夏智信设备不支持主动抓拍
    /// </summary>
    public bool SupportsActiveCapture => false;

    /// <summary>
    ///     华夏智信设备不支持主动抓拍,此方法抛出 NotSupportedException
    /// </summary>
    public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config)
    {
        _logger?.LogWarning(
            "华夏智信设备不支持主动抓拍: {Device}",
            config.Name);

        return Observable.Throw<LicensePlateRecognizedEvent>(
            new NotSupportedException(
                "华夏智信厂商不支持主动抓拍功能。设备仅支持被动捕获模式。"));
    }
}
```

**理由**:
- 明确标记不支持主动抓拍
- 提供清晰的错误消息
- 保持一致的接口
- 记录警告日志

---

## 内存管理

### 订阅释放

**关键**: 所有 MessageBus 订阅必须释放以防止内存泄漏。

**模式**:
```csharp
public class AttendedWeighingService : IAsyncDisposable
{
    private readonly IDisposable _licensePlateSubscription;

    public AttendedWeighingService()
    {
        _licensePlateSubscription = MessageBus.Current
            .Listen<LicensePlateRecognizedMessage>()
            .Subscribe(/* 处理程序 */);
    }

    public async ValueTask DisposeAsync()
    {
        // ✅ 始终释放订阅
        _licensePlateSubscription?.Dispose();
    }
}
```

**内存泄漏预防**:
1. 在字段中存储订阅引用
2. 在 `DisposeAsync()` 或 `Dispose()` 中释放
3. 对 ViewModel 订阅使用 `DisposeWith()` 模式
4. 使用长期运行场景测试(1000+ 周期)

---

## 错误处理

### 回调处理程序错误

**策略**: 记录并继续,不要让一条错误消息破坏系统

```csharp
try
{
    var message = new LicensePlateRecognizedMessage { /* ... */ };
    MessageBus.Current.SendMessage(message);
}
catch (Exception ex)
{
    _logger.LogError(ex, "处理来自 {Device} 的 LPR 回调失败", deviceName);
    // 向硬件设备返回成功(不重试)
    return Results.Ok(new { result = 0, error = "处理失败" });
}
```

### 订阅错误

**策略**: 记录但不要在订阅处理程序中抛出

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
                "处理 LPR 消息失败: {Plate}",
                msg.PlateNumber);
        }
    });
```

---

## 测试策略

### 单元测试

**1. 消息发布**:
```csharp
[Fact]
public void HandleHikvisionLprCallback_ShouldPublishCorrectMessage()
{
    // 安排
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

    // 执行
    var result = _service.HandleHikvisionLprCallback(callback);

    // 断言
    Assert.Single(messages);
    Assert.Equal("京A12345", messages[0].PlateNumber);
    Assert.Equal(LprDeviceType.Hikvision, messages[0].DeviceType);
}
```

**2. 消息订阅**:
```csharp
[Fact]
public void AttendedWeighingService_ShouldSubscribeToLprMessages()
{
    // 安排
    var service = new AttendedWeighingService(/* 模拟 */);
    var plateNumbers = new List<string?>();

    // 执行
    MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
    {
        PlateNumber = "京A12345"
    });

    var mostFrequent = service.GetMostFrequentPlateNumber();

    // 断言
    Assert.Equal("京A12345", mostFrequent);
}
```

**3. 主动抓拍**:
```csharp
[Fact]
public async Task HikvisionLprService_ShouldTriggerCapture()
{
    // 安排
    var mockSdk = new MockHikvisionSdk();
    var service = new HikvisionLprService(mockSdk);
    var config = new LicensePlateRecognitionConfig { /* ... */ };

    // 执行
    var results = await service.TriggerCaptureAsync(config)
        .Timeout(TimeSpan.FromSeconds(5))
        .ToList();

    // 断言
    Assert.Single(results);
    mockSdk.VerifyContinuousShootCalled();
}
```

### 集成测试

**端到端流程**:
```csharp
[Fact]
public async Task LprEventFlow_ShouldUpdateUiCorrectly()
{
    // 安排: 使用模拟硬件设置完整系统
    var (service, viewModel) = CreateSystem();

    // 执行: 模拟硬件回调
    SimulateHikvisionCallback("京A12345");

    // 断言: 验证服务状态
    Assert.Equal("京A12345", service.GetMostFrequentPlateNumber());

    // 断言: 验证 UI 更新
    Assert.Equal("京A12345", viewModel.MostFrequentPlateNumber);
}
```

### 内存泄漏测试

**长期运行场景**:
```csharp
[Fact]
public void RepeatedLprMessages_ShouldNotLeakMemory()
{
    // 安排
    var service = new AttendedWeighingService(/* 模拟 */);
    var initialMemory = GC.GetTotalMemory(true);

    // 执行: 发送 1000 条消息
    for (int i = 0; i < 1000; i++)
    {
        MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
        {
            PlateNumber = $"京A{i:00000}"
        });
    }

    // 清理
    service.Dispose();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // 断言: 内存增长应该最小
    var finalMemory = GC.GetTotalMemory(true);
    var growth = finalMemory - initialMemory;
    Assert.True(growth < 1024 * 1024, $"内存增长了 {growth} 字节");
}
```

---

## 迁移策略

### 阶段 1: 准备(无破坏性变更)
1. 创建 `LicensePlateRecognizedMessage` 类
2. 在 `AttendedWeighingService` 中添加 MessageBus 订阅(与现有方法并行)
3. 在接口中保持 `OnPlateNumberRecognized()`(双模式)

### 阶段 2: 迁移发布者
4. 重构 `MinimalWebHostService` 回调处理程序使用 MessageBus
5. 添加功能标志在直接调用和 MessageBus 之间切换(可选)
6. 并行测试两种模式

### 阶段 3: 移除旧路径
7. 从回调处理程序移除直接服务调用
8. 从公共接口移除 `OnPlateNumberRecognized()`
9. 在实现中使方法私有
10. 更新测试和文档

### 回滚计划

如果出现问题:
1. 将回调处理程序恢复为直接调用
2. 将 MessageBus 订阅作为附加侦听器保留(无危害)
3. 调查并修复问题
4. 重试迁移

---

## 权衡和替代方案

### 替代方案 1: 使用 ABP LocalEventBus

**拒绝原因**:
- 添加 5-10ms 延迟 vs MessageBus 的 <1ms
- 对于简单的 UI 通知来说过于复杂
- 需要额外的 `IEventHandler` 类
- 不太适合高频事件(每分钟 10-20 次 LPR)

**使用场景**: 更适合异步领域事件,如 `TryMatchEvent`(数据库操作)

### 替代方案 2: 保持直接调用

**拒绝原因**:
- 层与层之间紧密耦合
- 难以独立测试
- 架构不一致
- 无法支持多个订阅者

### 替代方案 3: 在服务中使用 Rx Observable

**拒绝原因**:
- `IHikvisionLprService` 已经有 `IObservable<LicensePlateRecognizedEvent>`
- 但 `MinimalWebHostService` 位于 SDK 和服务之间
- 需要为这种情况实现可观察模式,而 MessageBus 更简单
- 为此用例,MessageBus 更简单

---

## 影响分析

### 破坏性变更

**公共接口**:
- `IAttendedWeighingService.OnPlateNumberRecognized()` 移除
- 影响: 低(可能没有外部调用者)

**内部实现**:
- 回调处理程序签名不变
- `OnPlateNumberRecognized()` 逻辑不变
- 影响: 无

### 性能

- **变更前**: 直接方法调用(~0.1ms)
- **变更后**: MessageBus 发布 + 订阅(~0.5ms)
- **影响**: 对于 LPR 频率可忽略不计(每分钟 10-20 个事件)

### 兼容性

- 如果暂时保留 `OnPlateNumberRecognized()`,则向后兼容
- 与新的订阅者模式向前兼容
- 不需要数据库或 API 更改

---

## 开放问题

1. **我们应该弃用 `LicensePlateRecognizedEvent`(ABP 事件)吗?**
   - 它当前未使用
   - 建议: 标记 `[Obsolete]` 并在未来的清理中删除

2. **在转换期间我们应该同时支持 MessageBus 和直接调用吗?**
   - 取决于部署风险容忍度
   - 建议: 通过全面测试进行完全切换(更简单)

3. **我们应该添加设备特定的消息类吗?**
   - 当前设计: 单个 `LicensePlateRecognizedMessage` 带有 `DeviceType` 属性
   - 替代方案: `HikvisionLprMessage`, `LprAllInOneMessage` 等
   - 建议: 单个消息足够,减少重复

4. **如何处理 LprAllInOne 轮询模式与 Observable 模式的桥接?**
   - 当前实现使用标志位和轮询
   - 可能需要添加 `Subject<T>` 来桥接两种模式
   - 建议: 作为实现细节,在任务中解决

---

## 参考

- **ADR-009**: MessageBus 用于跨组件通信
- **响应式模式**: `openspec/docs/timer-to-rx-pattern.md`
- **相关变更**:
  - `unify-lpr-events-with-messagebus` - LPR 事件使用 MessageBus(此提案的基础)
  - `hikvision-lpr-implementation` - 海康威视 LPR 服务实现
  - `hikvision-lpr-integration` - 海康威视 LPR 配置和 UI
- **规范**: `openspec/specs/license-plate-recognition`
