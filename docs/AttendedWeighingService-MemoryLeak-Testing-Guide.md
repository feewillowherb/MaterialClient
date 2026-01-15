# AttendedWeighingService 内存泄漏测试指南

**文档版本**: 1.0
**创建日期**: 2026-01-13
**相关文件**: `AttendedWeighingServiceMemoryLeakTests.cs`
**参考文档**: `内存溢出问题分析报告.md`

---

## 📋 目录

1. [测试概述](#测试概述)
2. [测试目标](#测试目标)
3. [测试环境准备](#测试环境准备)
4. [运行测试](#运行测试)
5. [测试用例说明](#测试用例说明)
6. [解读测试结果](#解读测试结果)
7. [问题诊断流程](#问题诊断流程)
8. [修复验证](#修复验证)

---

## 测试概述

本测试套件专门用于检测 `AttendedWeighingService` 中的内存泄漏问题,重点针对 RxState 模式下的以下问题:

1. **循环引用** (最严重)
2. **ConcurrentBag 清理缺陷**
3. **Buffer 操作符内存积累**
4. **Replay 操作符内存积累**
5. **长时间运行稳定性**

---

## 测试目标

### 🔴 严重问题

#### 1. 循环引用测试
- **测试用例**: `CircularReference_Should_CauseMemoryLeak`
- **问题描述**: `_stateSubject` → 派生流 → `actions` → `stateStream` → `_stateSubject` 形成循环
- **预期结果**: 修复前,大部分服务实例无法被 GC 回收
- **修复目标**: 至少 80% 的实例应被正确回收

#### 2. ConcurrentBag 清理测试
- **测试用例**:
  - `ConcurrentBag_Should_NotGrowIndefinitely`
  - `StuckTasks_Should_NotCauseConcurrentBagLeak`
- **问题描述**:
  - `Clear()` 和 `Add()` 之间的竞态条件
  - 卡住的任务永远不会被移除
  - 每次清理都要重建集合,性能差
- **预期结果**: 修复前,`_pendingOperations` 集合会无限增长
- **修复目标**: 已完成任务应及时被清理,集合大小应保持稳定

### 🟡 中等问题

#### 3. Buffer 内存测试
- **测试用例**: `Buffer_Should_NotAccumulateExcessiveData`
- **问题描述**: 高频数据 + 大窗口导致 Buffer 积累大量数据
- **预期结果**: 修复前,内存可能增长超过 10 MB
- **修复目标**: 应限制 Buffer 大小或添加采样

#### 4. Replay 内存测试
- **测试用例**: `Replay_Should_NotAccumulateExcessiveHistory`
- **问题描述**: `Replay(5秒)` 为每个订阅者保留历史数据
- **预期结果**: 修复前,多订阅者时内存放大效应明显
- **修复目标**: 应限制 Replay 缓冲区大小或添加采样

### 🟢 压力测试

#### 5. 长时间运行测试
- **测试用例**:
  - `LongRunning_Should_NotCauseMemoryLeak` (模拟 10 分钟运行)
  - `ExtremeStress_Should_NotCauseOutofMemory`
- **问题描述**: 综合验证以上所有问题在长时间运行下的表现
- **预期结果**: 修复前,内存会持续增长
- **修复目标**: 内存增长应保持稳定,增长率接近 0

---

## 测试环境准备

### 1. 必要条件

- **.NET SDK**: .NET 10.0 或更高
- **测试框架**: xUnit
- **Mock 库**: NSubstitute (已包含在项目中)
- **IDE**: Visual Studio 2022 或 Rider (推荐)

### 2. 项目依赖

确保项目文件包含以下引用:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.x.x" />
  <PackageReference Include="xunit" Version="2.6.x" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.x" />
  <PackageReference Include="NSubstitute" Version="5.x.x" />
  <PackageReference Include="coverlet.collector" Version="6.x.x" />
</ItemGroup>
```

### 3. 系统要求

- **操作系统**: Windows 10/11 (推荐), macOS, Linux
- **内存**: 至少 4 GB 可用内存
- **CPU**: 多核处理器(用于并行测试)

### 4. 可选工具

- **dotMemory (JetBrains)**: 用于详细的内存分析
- **PerfView (Microsoft)**: 免费的内存分析工具
- **Visual Studio Profiler**: 内置的性能分析器

---

## 运行测试

### 方法 1: Visual Studio / Rider

1. 打开 **Test Explorer** (Visual Studio) 或 **Unit Tests** 工具窗口 (Rider)
2. 找到 `AttendedWeighingServiceMemoryLeakTests` 类
3. 右键点击测试用例或整个测试类
4. 选择 **Run**

### 方法 2: .NET CLI

```bash
# 运行所有内存泄漏测试
dotnet test MaterialClient.Common.Tests --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests"

# 运行特定测试
dotnet test --filter "FullyQualifiedName~CircularReference_Should_CauseMemoryLeak"

# 运行压力测试(标记为 Stress)
dotnet test --filter "Category=Stress"

# 生成测试覆盖率报告
dotnet test --collect:"XPlat Code Coverage"
```

### 方法 3: 使用详细输出

```bash
# 显示详细的测试输出
dotnet test --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests" --logger "console;verbosity=detailed"

# 将输出保存到文件
dotnet test --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests" --logger "trx;logfilename=MemoryLeakTestResults.trx"
```

---

## 测试用例说明

### 🔴 优先级 1: 循环引用测试

#### `CircularReference_Should_CauseMemoryLeak`
- **目的**: 检测 `_stateSubject` 循环引用导致的内存泄漏
- **方法**:
  1. 创建 10 个服务实例并释放
  2. 使用 `WeakReference` 追踪对象生命周期
  3. 强制 GC 后检查存活实例数量
- **通过标准**: 至少 8/10 的实例应被回收
- **失败判定**: 如果 > 7 个实例仍存活,说明存在严重内存泄漏

#### `StateSubject_DerivedStreams_Should_BeDisposed`
- **目的**: 验证派生流是否被正确释放
- **方法**:
  1. 创建服务并触发大量状态变化
  2. 调用 `DisposeAsync`
  3. 测量 Dispose 耗时
- **通过标准**: Dispose 应在 5 秒内完成
- **失败判定**: 如果 Dispose 耗时 > 5 秒,可能存在循环引用

---

### 🔴 优先级 2: ConcurrentBag 测试

#### `ConcurrentBag_Should_NotGrowIndefinitely`
- **目的**: 检查 `_pendingOperations` 集合是否无限增长
- **方法**:
  1. 触发 1000 次重量更新
  2. 通过反射检查 `_pendingOperations` 大小
- **通过标准**: 未完成任务应 < 500 个
- **失败判定**: 如果 > 500 个,说明清理逻辑有问题

#### `StuckTasks_Should_NotCauseConcurrentBagLeak`
- **目的**: 测试卡住任务的影响
- **方法**:
  1. 使用延迟的 Mock 仓库模拟卡住的任务
  2. 检查已完成但未移除的任务数量
- **通过标准**: 已完成但未移除的任务应 < 50 个
- **失败判定**: 如果 > 50 个,说明清理逻辑不完善

---

### 🟡 优先级 3: Buffer/Replay 测试

#### `Buffer_Should_NotAccumulateExcessiveData`
- **目的**: 测试 Buffer 在高频数据下的内存占用
- **配置**:
  - 稳定性窗口: 10 秒
  - 数据频率: 20 Hz
  - 数据量: 500 个点
- **通过标准**: 内存增长应 < 10 MB
- **失败判定**: 如果 > 10 MB,需要限制 Buffer 大小

#### `Replay_Should_NotAccumulateExcessiveHistory`
- **目的**: 测试 Replay 在多订阅者下的内存占用
- **配置**:
  - 5 个订阅者
  - 数据频率: 50 Hz
  - 数据量: 1000 个点
- **通过标准**: 内存增长应 < 20 MB
- **失败判定**: 如果 > 20 MB,需要限制 Replay 缓冲区

---

### 🟢 优先级 4: 压力测试

#### `LongRunning_Should_NotCauseMemoryLeak`
- **目的**: 模拟 10 分钟运行,验证内存稳定性
- **场景**:
  - 20 个称重周期
  - 每个周期包含: 上磅 → 稳定 → 下磅
- **通过标准**: 总内存增长应 < 5 MB
- **失败判定**: 如果 > 5 MB,存在内存泄漏

#### `ExtremeStress_Should_NotCauseOutofMemory`
- **目的**: 极限压力测试
- **场景**:
  - 1000 次快速操作
  - 操作频率: 100 Hz
  - 包含重量变化、类型切换、车牌识别
- **通过标准**: 内存增长应 < 50 MB
- **失败判定**: 如果 > 50 MB,可能存在严重问题

---

## 解读测试结果

### 成功示例

```
=== 测试循环引用导致的内存泄漏 ===
Initial memory: 1024.00 KB
Final memory: 1125.50 KB
Memory increase: 101.50 KB
Alive instances after GC: 1/10
✅ 大部分服务实例已被正确回收
```

**说明**: 测试通过,内存增长正常,大部分实例被回收。

---

### 失败示例

```
=== 测试循环引用导致的内存泄漏 ===
Initial memory: 1024.00 KB
Final memory: 15378.25 KB
Memory increase: 14354.25 KB
Alive instances after GC: 9/10
⚠️ WARNING: 大部分服务实例未被回收,可能存在内存泄漏（循环引用）
```

**说明**: 测试失败,存在严重内存泄漏。9/10 的实例未被回收,内存增长 14 MB。

---

### 常见警告及其含义

#### 警告 1: 循环引用
```
⚠️ WARNING: 大部分服务实例未被回收,可能存在内存泄漏（循环引用）
```
- **原因**: `_stateSubject` 的派生流形成循环引用
- **影响**: 对象无法被 GC 回收,内存持续增长
- **修复**: 移除从 `_stateSubject` 创建的 `deliveryTypeActions` 和 `recordIdActions`

#### 警告 2: ConcurrentBag 泄漏
```
⚠️ WARNING: ConcurrentBag 包含 850 个未清理的任务,可能存在内存泄漏
```
- **原因**: 清理逻辑有缺陷或任务卡住
- **影响**: `_pendingOperations` 集合无限增长
- **修复**: 改用 `ConcurrentQueue` 或优化清理逻辑

#### 警告 3: Buffer 内存过大
```
⚠️ WARNING: 内存增长过大 (15.23 MB),Buffer 可能积累过多数据
```
- **原因**: 高频数据 + 大稳定性窗口
- **影响**: Buffer 积累大量数据点,内存占用高
- **修复**: 限制 Buffer 大小或添加采样

#### 警告 4: Dispose 耗时过长
```
⚠️ WARNING: Dispose 耗时过长,可能存在循环引用导致的资源释放问题
```
- **原因**: 循环引用导致 Dispose 无法正常完成
- **影响**: 资源无法及时释放
- **修复**: 解决循环引用问题

---

## 问题诊断流程

### 第一步: 运行所有测试

```bash
dotnet test --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests"
```

### 第二步: 分析失败测试

按优先级处理:
1. 🔴 **先修复循环引用** (`CircularReference_Should_CauseMemoryLeak`)
2. 🔴 **再修复 ConcurrentBag** (`ConcurrentBag_Should_NotGrowIndefinitely`)
3. 🟡 **优化 Buffer/Replay** (内存测试)
4. 🟢 **验证长时间运行** (压力测试)

### 第三步: 使用内存分析工具

如果测试失败但无法定位问题,使用以下工具:

#### 使用 dotMemory

1. 在 Visual Studio 中: **Analyze** → **Performance Profiler** → **Memory Usage**
2. 运行失败的测试
3. 查看内存快照,重点关注:
   - `AttendedWeighingService` 实例数量
   - `IObservable<T>` 和 `IDisposable` 实例数量
   - `ConcurrentBag<Task>` 大小
   - `BehaviorSubject<WeighingServiceState>` 的订阅者数量

#### 使用 PerfView

```bash
# 启动 PerfView
PerfView.exe collect

# 运行测试
dotnet test --filter "FullyQualifiedName~CircularReference_Should_CauseMemoryLeak"

# 停止收集并查看 GC Heap Info
```

### 第四步: 定位具体代码

根据内存分析结果,定位到以下代码:

#### 循环引用位置
```csharp
// MaterialClient.Common/Services/AttendedWeighingService.cs:579-588
var deliveryTypeActions = _stateSubject
    .Skip(1)
    .Select(state => state.DeliveryType)
    .DistinctUntilChanged()
    .Select(dt => (StateAction)new SetDeliveryTypeAction(dt));

var recordIdActions = _stateSubject
    .Skip(1)
    .Select(state => state.LastCreatedWeighingRecordId)
    .DistinctUntilChanged()
    .Select(id => (StateAction)new WeighingRecordCreatedAction(id));
```

#### ConcurrentBag 清理位置
```csharp
// MaterialClient.Common/Services/AttendedWeighingService.cs:212-224
lock (_operationsLock)
{
    var remainingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToList();
    if (remainingTasks.Count < _pendingOperations.Count)
    {
        _pendingOperations.Clear();
        foreach (var remainingTask in remainingTasks)
        {
            _pendingOperations.Add(remainingTask);
        }
    }
}
```

---

## 修复验证

### 验证循环引用修复

1. **修改代码**: 移除 `deliveryTypeActions` 和 `recordIdActions` 的创建
2. **重新编译**: `dotnet build`
3. **运行测试**:
   ```bash
   dotnet test --filter "FullyQualifiedName~CircularReference_Should_CauseMemoryLeak"
   ```
4. **检查结果**: 应显示 "✅ 大部分服务实例已被正确回收"

### 验证 ConcurrentBag 修复

1. **修改代码**: 改用 `ConcurrentQueue` 或优化清理逻辑
2. **重新编译**: `dotnet build`
3. **运行测试**:
   ```bash
   dotnet test --filter "FullyQualifiedName~ConcurrentBag_Should_NotGrowIndefinitely"
   ```
4. **检查结果**: 未完成任务数量应 < 500

### 验证 Buffer/Replay 优化

1. **修改代码**: 添加采样或限制缓冲区大小
2. **重新编译**: `dotnet build`
3. **运行测试**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Buffer_Should_NotAccumulateExcessiveData"
   dotnet test --filter "FullyQualifiedName~Replay_Should_NotAccumulateExcessiveHistory"
   ```
4. **检查结果**: 内存增长应 < 10 MB (Buffer) 和 < 20 MB (Replay)

### 最终验证: 压力测试

所有修复完成后,运行压力测试:

```bash
dotnet test --filter "Category=Stress"
```

**预期结果**:
- `LongRunning_Should_NotCauseMemoryLeak`: ✅ 内存增长 < 5 MB
- `ExtremeStress_Should_NotCauseOutofMemory`: ✅ 内存增长 < 50 MB

---

## 附录: 快速参考

### 测试优先级

| 优先级 | 测试用例 | 问题类型 | 预期影响 |
|--------|----------|----------|----------|
| 🔴 P0 | `CircularReference_Should_CauseMemoryLeak` | 循环引用 | 最严重 |
| 🔴 P0 | `ConcurrentBag_Should_NotGrowIndefinitely` | 集合泄漏 | 严重 |
| 🟡 P1 | `Buffer_Should_NotAccumulateExcessiveData` | Buffer 内存 | 中等 |
| 🟡 P1 | `Replay_Should_NotAccumulateExcessiveHistory` | Replay 内存 | 中等 |
| 🟢 P2 | `LongRunning_Should_NotCauseMemoryLeak` | 综合验证 | 重要 |

### 常用命令

```bash
# 运行所有内存泄漏测试
dotnet test --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests"

# 运行特定优先级测试
dotnet test --filter "FullyQualifiedName~CircularReference"
dotnet test --filter "FullyQualifiedName~ConcurrentBag"

# 运行压力测试
dotnet test --filter "Category=Stress"

# 详细输出
dotnet test --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests" --logger "console;verbosity=detailed"

# 生成覆盖率报告
dotnet test --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~AttendedWeighingServiceMemoryLeakTests"
```

### 判定标准总结

| 测试用例 | 通过标准 | 失败阈值 |
|----------|----------|----------|
| 循环引用 | ≥ 8/10 实例被回收 | > 7 个实例存活 |
| ConcurrentBag | < 500 个未完成任务 | > 500 个 |
| Buffer | < 10 MB 内存增长 | > 10 MB |
| Replay | < 20 MB 内存增长 | > 20 MB |
| 长时间运行 | < 5 MB 内存增长 | > 5 MB |
| 极限压力 | < 50 MB 内存增长 | > 50 MB |

---

## 更新日志

| 版本 | 日期 | 更新内容 |
|------|------|----------|
| 1.0 | 2026-01-13 | 初始版本,包含所有测试用例和文档 |

---

## 相关文档

- [内存溢出问题分析报告.md](./内存溢出问题分析报告.md)
- [AttendedWeighingService-RxState-Optimization-Report.md](./AttendedWeighingService-RxState-Optimization-Report.md)
- [AttendedWeighingService-Rx-Evaluation-Report.md](./AttendedWeighingService-Rx-Evaluation-Report.md)

---

**文档维护**: 如需更新此文档,请保持版本号和更新日志的同步。
