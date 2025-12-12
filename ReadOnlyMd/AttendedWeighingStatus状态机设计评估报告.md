# AttendedWeighingStatus 状态机设计评估报告

## 执行摘要

本报告针对 `AttendedWeighingStatus` 枚举的三种状态设计进行评估，分析是否满足业务需求，并评估是否需要扩展为更详细的状态模型。

**评估结论**：⚠️ **建议扩展** - 当前三种状态在功能上基本满足需求，但缺少对"正常完成"和"异常下磅"的区分，建议扩展为四种状态以提升业务语义清晰度和可追溯性。

---

## 目录

1. [当前状态设计分析](#当前状态设计分析)
2. [业务场景分析](#业务场景分析)
3. [问题识别](#问题识别)
4. [扩展方案设计](#扩展方案设计)
5. [影响评估](#影响评估)
6. [实施建议](#实施建议)
7. [总结](#总结)

---

## 当前状态设计分析

### 当前三种状态

```csharp
public enum AttendedWeighingStatus
{
    /// <summary>
    /// Off scale - 车辆未上磅或已下磅
    /// </summary>
    OffScale = 0,

    /// <summary>
    /// On scale waiting for weight stability - 车辆已上磅，等待重量稳定
    /// </summary>
    WaitingForStability = 1,

    /// <summary>
    /// Weight stabilized - 重量已稳定
    /// </summary>
    WeightStabilized = 2
}
```

### 当前状态转换图

```
┌─────────────┐
│  OffScale   │ ◄──────────────────┐
│  (未上磅)   │                    │
└──────┬──────┘                    │
       │ 重量 > 0.5t                │
       ↓                            │
┌─────────────────────┐            │
│ WaitingForStability │            │
│   (等待稳定)        │            │
└──────┬──────────────┘            │
       │                            │
       ├─ 重量稳定 ────────────────┐│
       │                            ││
       │ 重量 < 0.5t (异常下磅)     ││
       ↓                            ││
┌─────────────────────┐            ││
│ WeightStabilized   │            ││
│   (重量已稳定)      │            ││
└──────┬──────────────┘            ││
       │                            ││
       │ 重量 < 0.5t (正常下磅)     ││
       └───────────────────────────┘│
                                     │
                              (两种路径都回到 OffScale)
```

### 当前状态转换逻辑

#### 1. OffScale → WaitingForStability
**触发条件**：重量从 < 0.5t 增加到 > 0.5t  
**业务含义**：车辆已上磅  
**代码位置**：`ProcessWeightChange()` 第 318-327 行

```314:327:MaterialClient/MaterialClient.Common/Services/AttendedWeighingService.cs
    private void ProcessWeightChange(decimal currentWeight)
    {
        switch (_currentStatus)
        {
            case AttendedWeighingStatus.OffScale:
                // OffScale -> WaitingForStability: weight increases from <0.5t to >0.5t
                if (currentWeight > WeightThreshold)
                {
                    _currentStatus = AttendedWeighingStatus.WaitingForStability;
                    _stableWeight = null;

                    _logger.LogInformation(
                        $"AttendedWeighingService: Entered WaitingForStability state, weight: {currentWeight}kg");
                }

                break;
```

#### 2. WaitingForStability → WeightStabilized
**触发条件**：重量稳定（3秒内变化 < ±0.1t）  
**业务含义**：车辆已上磅且称重已稳定  
**代码位置**：`CheckWeightStability()` 第 389-413 行

#### 3. WeightStabilized → OffScale
**触发条件**：重量从 > 0.5t 减少到 < 0.5t  
**业务含义**：车辆已下磅，称重已完成（正常流程）  
**处理逻辑**：清空车牌缓存，记录日志  
**代码位置**：`ProcessWeightChange()` 第 368-382 行

```368:382:MaterialClient/MaterialClient.Common/Services/AttendedWeighingService.cs
            case AttendedWeighingStatus.WeightStabilized:
                if (currentWeight < WeightThreshold)
                {
                    // WeightStabilized -> OffScale: normal flow
                    _currentStatus = AttendedWeighingStatus.OffScale;


                    // Clear plate number cache
                    ClearPlateNumberCache();

                    _logger?.LogInformation(
                        $"AttendedWeighingService: Normal flow completed, entered OffScale state, weight: {currentWeight}kg");
                }

                break;
```

#### 4. WaitingForStability → OffScale（异常路径）
**触发条件**：重量从 > 0.5t 减少到 < 0.5t（未达到稳定状态）  
**业务含义**：车辆已下磅，称重未完成（异常流程）  
**处理逻辑**：拍照但不创建称重记录，清空车牌缓存  
**代码位置**：`ProcessWeightChange()` 第 331-359 行

```331:359:MaterialClient/MaterialClient.Common/Services/AttendedWeighingService.cs
            case AttendedWeighingStatus.WaitingForStability:
                if (currentWeight < WeightThreshold)
                {
                    // Unstable weighing flow: directly from WaitingForStability to OffScale
                    _currentStatus = AttendedWeighingStatus.OffScale;
                    _stableWeight = null;

                    // Capture all cameras and log (no need to save photos)
                    _ = Task.Run(async () =>
                    {
                        var photos = await CaptureAllCamerasAsync("UnstableWeighingFlow");
                        if (photos.Count == 0)
                        {
                            _logger?.LogWarning(
                                $"AttendedWeighingService: Unstable weighing flow capture completed, but no photos were obtained");
                        }
                        else
                        {
                            _logger?.LogInformation(
                                $"AttendedWeighingService: Unstable weighing flow captured {photos.Count} photos");
                        }
                    });

                    // Clear plate number cache
                    ClearPlateNumberCache();

                    _logger?.LogWarning(
                        $"AttendedWeighingService: Unstable weighing flow, weight returned to {currentWeight}kg, triggered capture");
                }
```

---

## 业务场景分析

### 场景 1：正常称重流程 ✅

**流程**：
1. 车辆上磅 → `OffScale` → `WaitingForStability`
2. 重量稳定 → `WaitingForStability` → `WeightStabilized`
3. 创建称重记录（包含照片）
4. 车辆下磅 → `WeightStabilized` → `OffScale`

**当前状态覆盖**：✅ 完全覆盖

### 场景 2：异常下磅流程 ⚠️

**流程**：
1. 车辆上磅 → `OffScale` → `WaitingForStability`
2. 车辆下磅（未稳定） → `WaitingForStability` → `OffScale`
3. 拍照但不创建称重记录

**当前状态覆盖**：⚠️ 功能覆盖，但语义不清晰

**问题**：
- 最终状态都是 `OffScale`，无法区分是"正常完成"还是"异常下磅"
- 需要通过日志或业务逻辑推断历史状态

### 场景 3：状态查询和统计 📊

**需求场景**：
- UI 显示当前状态："车辆已下磅，称重已完成" vs "车辆已下磅，称重未完成"
- 统计报表：正常完成次数 vs 异常下磅次数
- 业务规则：根据完成状态执行不同逻辑

**当前状态覆盖**：❌ 无法区分

---

## 问题识别

### 问题 1：状态语义模糊

| 问题 | 描述 | 影响 |
|------|------|------|
| **OffScale 状态歧义** | 无法区分"正常完成"和"异常下磅" | 🟡 中 |
| **缺少完成状态** | 没有明确的"称重已完成"状态 | 🟡 中 |
| **缺少异常状态** | 没有明确的"称重未完成"状态 | 🟡 中 |

### 问题 2：业务逻辑依赖历史推断

**当前实现**：
- 通过日志判断是否创建了称重记录
- 通过 `_stableWeight` 是否为 null 推断是否稳定过
- 无法直接通过状态判断业务结果

**影响**：
- 代码可读性降低
- 调试困难
- 业务规则复杂

### 问题 3：UI 显示限制

**当前 UI 显示**：
```csharp
AttendedWeighingStatus.OffScale => "称重已结束"
```

**问题**：
- "称重已结束"无法区分是"已完成"还是"未完成"
- 用户无法直观了解称重结果

---

## 扩展方案设计

### 方案 A：扩展为四种状态（推荐）⭐

#### 状态定义

```csharp
public enum AttendedWeighingStatus
{
    /// <summary>
    /// Off scale - 车辆未上磅（初始状态）
    /// </summary>
    OffScale = 0,

    /// <summary>
    /// Vehicle on scale - 车辆已上磅
    /// </summary>
    VehicleOnScale = 1,

    /// <summary>
    /// Weight stabilized - 车辆已上磅且称重已稳定
    /// </summary>
    WeightStabilized = 2,

    /// <summary>
    /// Weighing completed - 车辆已下磅，称重已完成（正常流程）
    /// </summary>
    WeighingCompleted = 3,

    /// <summary>
    /// Weighing incomplete - 车辆已下磅，称重未完成（未稳定下磅）
    /// </summary>
    WeighingIncomplete = 4
}
```

#### 状态转换图

```
┌─────────────┐
│  OffScale   │ ◄──────────────────────────────┐
│  (未上磅)   │                                │
└──────┬──────┘                                │
       │ 重量 > 0.5t                            │
       ↓                                        │
┌─────────────────────┐                        │
│ VehicleOnScale      │                        │
│   (车辆已上磅)       │                        │
└──────┬──────────────┘                        │
       │                                        │
       ├─ 重量稳定 ────────────────┐           │
       │                            │           │
       │ 重量 < 0.5t (异常下磅)     │           │
       ↓                            │           │
┌─────────────────────┐            │           │
│ WeightStabilized    │            │           │
│   (重量已稳定)      │            │           │
└──────┬──────────────┘            │           │
       │                            │           │
       │ 重量 < 0.5t (正常下磅)     │           │
       ↓                            │           │
┌─────────────────────┐            │           │
│ WeighingCompleted   │            │           │
│   (称重已完成)      │            │           │
└─────────────────────┘            │           │
                                   │           │
┌─────────────────────┐            │           │
│ WeighingIncomplete  │            │           │
│   (称重未完成)      │            │           │
└─────────────────────┘            │           │
                                   │           │
                            (自动回到 OffScale)
```

#### 状态转换逻辑

| 转换 | 触发条件 | 业务含义 | 处理逻辑 |
|------|----------|----------|----------|
| `OffScale` → `VehicleOnScale` | 重量 > 0.5t | 车辆已上磅 | 重置稳定重量，清空车牌缓存 |
| `VehicleOnScale` → `WeightStabilized` | 重量稳定（3秒内变化 < ±0.1t） | 称重已稳定 | 创建称重记录，拍照 |
| `WeightStabilized` → `WeighingCompleted` | 重量 < 0.5t | 正常完成 | 清空车牌缓存 |
| `VehicleOnScale` → `WeighingIncomplete` | 重量 < 0.5t（未稳定） | 异常下磅 | 拍照但不创建记录，清空车牌缓存 |
| `WeighingCompleted` → `OffScale` | 延迟或手动重置 | 准备下次称重 | 无特殊处理 |
| `WeighingIncomplete` → `OffScale` | 延迟或手动重置 | 准备下次称重 | 无特殊处理 |

#### 实现要点

1. **完成状态持续时间**
   - `WeighingCompleted` 和 `WeighingIncomplete` 状态应持续一段时间（如 5-10 秒）
   - 然后自动转换回 `OffScale`，准备下次称重
   - 或通过定时器/延迟任务实现

2. **状态持久化**
   - 如果需要历史追溯，可以考虑将完成状态记录到数据库
   - 当前实现中，状态仅在内存中，重启后丢失

3. **向后兼容**
   - 保留原有状态值（0, 1, 2）以保持兼容性
   - 新增状态使用新值（3, 4）

---

### 方案 B：保持三种状态 + 添加完成标志（简化方案）

#### 设计思路

保持三种状态不变，但添加一个标志位记录完成状态：

```csharp
private AttendedWeighingStatus _currentStatus = AttendedWeighingStatus.OffScale;
private bool _lastWeighingCompleted = false; // 上次称重是否完成
```

#### 优点
- ✅ 改动最小
- ✅ 向后兼容
- ✅ 实现简单

#### 缺点
- ❌ 状态语义仍然不够清晰
- ❌ 需要额外的标志位维护
- ❌ UI 显示仍需特殊处理

---

### 方案 C：使用状态机模式（过度设计）

使用状态机库（如 Stateless）管理复杂状态转换。

#### 优点
- ✅ 状态转换清晰
- ✅ 支持复杂业务规则

#### 缺点
- ❌ 过度设计，当前场景不需要
- ❌ 增加依赖和复杂度
- ❌ 学习成本高

---

## 影响评估

### 方案 A 影响分析

#### 代码修改范围

| 文件 | 修改内容 | 工作量 |
|------|----------|--------|
| `AttendedWeighingStatus.cs` | 扩展枚举定义 | 5 分钟 |
| `AttendedWeighingService.cs` | 修改状态转换逻辑 | 1-2 小时 |
| `AttendedWeighingViewModel.cs` | 更新 UI 显示文本 | 15 分钟 |
| 其他订阅者 | 处理新状态 | 30 分钟 |
| **总计** | | **2-3 小时** |

#### 兼容性影响

| 方面 | 影响 | 缓解措施 |
|------|------|----------|
| **数据库** | 🟢 无影响 | 状态不持久化 |
| **API 接口** | 🟡 中等 | 枚举值扩展，向后兼容 |
| **UI 显示** | 🟡 中等 | 需要更新显示文本 |
| **订阅者** | 🟡 中等 | 需要处理新状态（可选） |

#### 测试影响

| 测试类型 | 影响 | 工作量 |
|----------|------|--------|
| **单元测试** | 🟡 中等 | 更新状态转换测试，1 小时 |
| **集成测试** | 🟡 中等 | 验证新状态流程，1 小时 |
| **UI 测试** | 🟢 低 | 验证显示文本，30 分钟 |

---

## 实施建议

### 推荐方案：方案 A（扩展为四种状态）

#### 实施步骤

1. **阶段 1：扩展枚举定义**（5 分钟）
   ```csharp
   public enum AttendedWeighingStatus
   {
       OffScale = 0,
       VehicleOnScale = 1,        // 新增：原 WaitingForStability
       WeightStabilized = 2,
       WeighingCompleted = 3,     // 新增
       WeighingIncomplete = 4     // 新增
   }
   ```

2. **阶段 2：重构状态转换逻辑**（1-2 小时）
   - 重命名 `WaitingForStability` → `VehicleOnScale`
   - 添加 `WeightStabilized` → `WeighingCompleted` 转换
   - 添加 `VehicleOnScale` → `WeighingIncomplete` 转换
   - 添加完成状态自动回到 `OffScale` 的逻辑

3. **阶段 3：更新 UI 显示**（15 分钟）
   ```csharp
   private static string GetStatusText(AttendedWeighingStatus status)
   {
       return status switch
       {
           AttendedWeighingStatus.OffScale => "未上磅",
           AttendedWeighingStatus.VehicleOnScale => "车辆已上磅",
           AttendedWeighingStatus.WeightStabilized => "重量已稳定",
           AttendedWeighingStatus.WeighingCompleted => "称重已完成",
           AttendedWeighingStatus.WeighingIncomplete => "称重未完成",
           _ => "未知状态"
       };
   }
   ```

4. **阶段 4：测试验证**（2 小时）
   - 单元测试：验证状态转换逻辑
   - 集成测试：验证完整业务流程
   - UI 测试：验证状态显示

#### 关键实现细节

**1. 完成状态自动重置**

```csharp
private async Task TransitionToOffScaleAfterDelay(TimeSpan delay)
{
    await Task.Delay(delay);
    lock (_statusLock)
    {
        if (_currentStatus == AttendedWeighingStatus.WeighingCompleted ||
            _currentStatus == AttendedWeighingStatus.WeighingIncomplete)
        {
            _currentStatus = AttendedWeighingStatus.OffScale;
            _logger?.LogDebug("AttendedWeighingService: Reset to OffScale after completion");
        }
    }
}
```

**2. 状态转换示例**

```csharp
case AttendedWeighingStatus.WeightStabilized:
    if (currentWeight < WeightThreshold)
    {
        _currentStatus = AttendedWeighingStatus.WeighingCompleted;
        ClearPlateNumberCache();
        _logger?.LogInformation(
            $"AttendedWeighingService: Weighing completed, weight: {currentWeight}kg");
        
        // 5 秒后自动回到 OffScale
        _ = Task.Run(async () => 
            await TransitionToOffScaleAfterDelay(TimeSpan.FromSeconds(5)));
    }
    break;

case AttendedWeighingStatus.VehicleOnScale:
    if (currentWeight < WeightThreshold)
    {
        _currentStatus = AttendedWeighingStatus.WeighingIncomplete;
        _stableWeight = null;
        
        // 拍照但不创建记录
        _ = Task.Run(async () =>
        {
            var photos = await CaptureAllCamerasAsync("UnstableWeighingFlow");
            _logger?.LogWarning(
                $"AttendedWeighingService: Weighing incomplete, captured {photos.Count} photos");
        });
        
        ClearPlateNumberCache();
        _logger?.LogWarning(
            $"AttendedWeighingService: Weighing incomplete, weight: {currentWeight}kg");
        
        // 5 秒后自动回到 OffScale
        _ = Task.Run(async () => 
            await TransitionToOffScaleAfterDelay(TimeSpan.FromSeconds(5)));
    }
    break;
```

---

## 总结

### 当前状态评估

| 方面 | 评级 | 说明 |
|------|------|------|
| **功能完整性** | 🟢 **良好** | 三种状态基本满足功能需求 |
| **语义清晰度** | 🟡 **一般** | OffScale 状态存在歧义 |
| **业务可追溯性** | 🟡 **一般** | 无法直接区分完成状态 |
| **UI 友好性** | 🟡 **一般** | 显示文本不够精确 |
| **扩展性** | 🟢 **良好** | 易于扩展 |

### 关键发现

1. ✅ **功能覆盖完整**：当前三种状态能够处理所有业务场景
2. ⚠️ **语义不够清晰**：`OffScale` 状态无法区分"正常完成"和"异常下磅"
3. ⚠️ **缺少完成状态**：没有明确的"称重已完成"和"称重未完成"状态
4. ✅ **扩展成本可控**：扩展为四种状态的工作量约 2-3 小时

### 最终建议

**推荐实施方案 A：扩展为四种状态** ⭐

**理由**：
1. ✅ **语义清晰**：每个状态都有明确的业务含义
2. ✅ **易于理解**：代码和日志更易读
3. ✅ **便于扩展**：未来可以基于完成状态添加更多业务逻辑
4. ✅ **成本可控**：工作量约 2-3 小时，影响范围可控
5. ✅ **向后兼容**：保留原有状态值，新状态使用新值

**不推荐方案 B（添加标志位）**：
- 虽然实现简单，但状态语义仍然不够清晰
- 需要维护额外的状态标志，增加复杂度

**不推荐方案 C（状态机库）**：
- 当前场景不需要如此复杂的实现
- 增加依赖和学习成本

### 实施优先级

| 优先级 | 任务 | 工作量 | 收益 |
|--------|------|--------|------|
| **P0** | 扩展枚举定义 | 5 分钟 | 🟢 高 |
| **P0** | 重构状态转换逻辑 | 1-2 小时 | 🟢 高 |
| **P1** | 更新 UI 显示 | 15 分钟 | 🟡 中 |
| **P1** | 添加单元测试 | 1 小时 | 🟡 中 |
| **P2** | 更新订阅者逻辑 | 30 分钟 | 🟢 低 |

---

## 参考资料

### 相关文档
- [TruckScaleWeightService背压风险评估报告.md](./TruckScaleWeightService背压风险评估报告.md)
- [重量稳定性监控优化分析.md](./重量稳定性监控优化分析.md)
- [有人值守实现.md](./有人值守实现.md)

### 相关代码
- `MaterialClient.Common/Entities/Enums/AttendedWeighingStatus.cs`
- `MaterialClient.Common/Services/AttendedWeighingService.cs`
- `MaterialClient/MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

---

*创建时间：2025-12-11*  
*评估版本：v1.0*  
*评估人：AI Assistant*


