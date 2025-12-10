# Timer 调整为 ReactiveX 改造总结

## 日期
2024年（当前会话）

## 问题概述

### 初始问题
1. **XAML 设计器错误**：无法识别 `MostFrequentPlateNumber` 属性
   - 错误信息：`Unable to resolve property or method of name 'MostFrequentPlateNumber'`
   - 原因：使用 `[ObservableProperty]` 特性时，设计器无法识别生成的属性

2. **状态显示需求**：需要将称重状态绑定到 UI，并显示中文文本

3. **性能优化需求**：将轮询方式改为响应式编程模式，直接监听服务状态变化

## 解决方案

### 1. 修复 MostFrequentPlateNumber 设计时支持

**文件**：`MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**问题**：`[ObservableProperty]` 生成的属性在设计时无法被识别

**解决方案**：
- 移除了 `[ObservableProperty]` 特性
- 手动添加公共属性，使用 `SetProperty` 方法实现属性变更通知

```csharp
private string? _mostFrequentPlateNumber;

public string? MostFrequentPlateNumber
{
    get => _mostFrequentPlateNumber;
    set => SetProperty(ref _mostFrequentPlateNumber, value);
}
```

### 2. 添加状态显示功能

**文件**：
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`
- `MaterialClient/Views/AttendedWeighingWindow.axaml`

**实现**：
- 添加 `_currentWeighingStatus` 字段存储当前状态
- 添加 `CurrentWeighingStatusText` 属性返回中文文本
- 添加 `GetStatusText` 方法将枚举转换为中文：
  - `OffScale` → "预重已结束"
  - `WaitingForStability` → "等待稳定"
  - `WeightStabilized` → "重量已稳定"
- 在 XAML 中绑定 `{Binding CurrentWeighingStatusText}`

### 3. 实现响应式状态监听（ReactiveX）

**参考设计**：`ITruckScaleWeightService.WeightUpdates`

**文件**：`MaterialClient.Common/Services/AttendedWeighingService.cs`

#### 3.1 添加 StatusChanges Observable

**接口添加**：
```csharp
IObservable<AttendedWeighingStatus> StatusChanges { get; }
```

**实现**：
```csharp
private readonly Subject<AttendedWeighingStatus> _statusSubject = new();
public IObservable<AttendedWeighingStatus> StatusChanges => _statusSubject;
```

**状态变化时发布**：
```csharp
if (_currentStatus != previousStatus)
{
    _logger?.LogInformation(...);
    _statusSubject.OnNext(_currentStatus);
}
```

#### 3.2 添加 MostFrequentPlateNumberChanges Observable

**接口添加**：
```csharp
IObservable<string?> MostFrequentPlateNumberChanges { get; }
```

**实现**：
```csharp
private readonly Subject<string?> _plateNumberSubject = new();
public IObservable<string?> MostFrequentPlateNumberChanges => _plateNumberSubject;
```

**车牌号变化时发布**：
- `OnPlateNumberRecognized` - 识别到车牌号后
- `SelectPlateNumberFromCache` - 选择车牌号后
- `UpdatePlateNumberIfNeeded` - 更新车牌号后
- `ClearPlateNumberCache` - 清空缓存时（发布 `null`）

**资源清理**：
```csharp
public async ValueTask DisposeAsync()
{
    await StopAsync();
    _statusSubject?.OnCompleted();
    _statusSubject?.Dispose();
    _plateNumberSubject?.OnCompleted();
    _plateNumberSubject?.Dispose();
}
```

### 4. ViewModel 改造

**文件**：`MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

#### 4.1 移除 Timer 轮询

**移除**：
- `_plateNumberUpdateTimer` 字段
- `_statusUpdateTimer` 字段
- `PlateNumberUpdateIntervalMs` 常量
- `StatusUpdateIntervalMs` 常量
- `StartPlateNumberUpdateTimer()` 方法
- `StartStatusUpdateTimer()` 方法

#### 4.2 实现 Observable 订阅

**状态订阅**：
```csharp
private void StartStatusObservable()
{
    if (_attendedWeighingService == null) return;

    _attendedWeighingService.StatusChanges
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(status =>
        {
            _currentWeighingStatus = status;
            OnPropertyChanged(nameof(CurrentWeighingStatusText));
        })
        .DisposeWith(_disposables);
}
```

**车牌号订阅**：
```csharp
private void StartPlateNumberObservable()
{
    if (_attendedWeighingService == null) return;

    _attendedWeighingService.MostFrequentPlateNumberChanges
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(plateNumber =>
        {
            MostFrequentPlateNumber = plateNumber;
        })
        .DisposeWith(_disposables);
}
```

## 技术要点

### ReactiveX 模式
- 使用 `Subject<T>` 作为内部事件源
- 通过 `IObservable<T>` 属性暴露给外部
- 在数据变化时调用 `OnNext()` 发布更新
- 使用 `ObserveOn()` 确保在 UI 线程更新
- 使用 `DisposeWith()` 管理订阅生命周期

### 优势
1. **实时响应**：状态变化时立即通知，无需轮询
2. **性能优化**：只在数据变化时触发更新，减少不必要的检查
3. **代码简洁**：符合响应式编程模式，代码更易维护
4. **资源利用**：避免定时器开销，资源利用更合理

## 修改的文件清单

1. `MaterialClient.Common/Services/AttendedWeighingService.cs`
   - 添加 `StatusChanges` Observable
   - 添加 `MostFrequentPlateNumberChanges` Observable
   - 在状态变化时发布更新
   - 在车牌号变化时发布更新

2. `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`
   - 修复 `MostFrequentPlateNumber` 设计时支持
   - 添加状态显示功能
   - 移除 Timer 轮询
   - 实现 Observable 订阅

3. `MaterialClient/Views/AttendedWeighingWindow.axaml`
   - 绑定状态文本到 `CurrentWeighingStatusText`

## 命名空间引用

**AttendedWeighingService.cs**：
```csharp
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
```

**AttendedWeighingViewModel.cs**：
```csharp
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
```

## 测试建议

1. 验证状态变化时 UI 是否立即更新
2. 验证车牌号变化时 UI 是否立即更新
3. 验证资源是否正确释放（无内存泄漏）
4. 验证设计器是否能正常识别属性

## 后续优化建议

1. 考虑添加初始值发布（使用 `StartWith`）
2. 考虑添加错误处理（使用 `Catch`）
3. 考虑添加防抖处理（使用 `Throttle`）如果更新频率过高

## 相关参考

- `ITruckScaleWeightService.WeightUpdates` - 参考设计模式
- ReactiveUI 文档：https://www.reactiveui.net/
- System.Reactive 文档：https://github.com/dotnet/reactive
