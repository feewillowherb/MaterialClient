# Proposal: AttendedWeighingService 内存泄漏修复

**Change ID**: `attended-weighing-memory-leak-fix`
**Status**: 🟡 Proposed
**Created**: 2026-01-13
**Priority**: 🔴 High

---

## 📋 Overview

### 问题摘要

`AttendedWeighingService` 服务存在严重的内存泄漏问题，根据 `docs/内存溢出问题分析报告.md` 的分析，发现了 **5 个严重的内存泄漏风险点**。最严重的问题是**循环引用**，这会导致 GC 无法回收对象，最终导致内存溢出（OOM）。

### 影响范围

- **核心服务**: `AttendedWeighingService`
- **相关服务**: `HikvisionService`, `TruckScaleWeightService`
- **影响模块**: 称重管理系统、车牌识别、摄像头集成
- **部署环境**: Windows 桌面应用程序（长期运行）

### 业务影响

- 🔴 **严重**: 长时间运行后内存溢出，导致应用崩溃
- 🔴 **严重**: 影响生产环境的稳定性和可靠性
- 🟡 **中等**: 性能下降，响应变慢
- 🟢 **低**: 需要定期重启应用以释放内存

---

## 🎯 Objectives

### 主要目标

1. **修复循环引用问题**（优先级：🔴 P0）
   - 移除 `_stateSubject` 的循环依赖
   - 重构状态流设计，避免 `deliveryTypeActions` 和 `recordIdActions` 的循环引用
   - 确保服务实例可以被 GC 正确回收

2. **优化 ConcurrentBag 清理逻辑**（优先级：🔴 P0）
   - 替换 `ConcurrentBag` 为 `ConcurrentQueue`
   - 实现定期清理机制，避免无限增长
   - 修复竞态条件问题

3. **优化 Buffer 和 Replay 操作符**（优先级：🟡 P1）
   - 为 `Buffer` 操作符添加大小限制
   - 为 `Replay` 操作符添加采样或缓冲区限制
   - 减少高频数据时的内存占用

4. **优化回调函数生命周期**（优先级：🟡 P1）
   - 在 `HikvisionService` 中使用弱引用或标志位
   - 确保解码器对象可以被及时释放

### 次要目标

- 完善单元测试，覆盖所有内存泄漏场景
- 添加长时间运行测试（24 小时+）
- 添加性能基准测试
- 更新文档，记录修复方案和最佳实践

---

## 📊 Current State Analysis

### 现有测试结果

✅ **好消息**: 新创建的内存泄漏测试套件 (`AttendedWeighingServiceMemoryLeakTests`) **全部通过** (8/8)

```
测试结果摘要 (2026-01-13):
- 循环引用测试: ✅ 通过 (90% 实例被回收)
- ConcurrentBag 测试: ✅ 通过 (0 个未完成任务)
- Buffer 内存测试: ✅ 通过 (+179 KB)
- Replay 内存测试: ✅ 通过 (-376 KB, 实际释放)
- 长时间运行测试: ✅ 通过 (136 秒 +264 KB, 1.94 KB/s)
- 极限压力测试: ✅ 通过 (+138 KB)
```

### 分析

**为什么测试通过，但报告说有严重问题？**

1. **.NET 10.0 的 GC 改进**: 可能已经能够处理这种循环引用
2. **Rx 的正确 Dispose**: 测试中所有订阅都被正确释放
3. **测试场景不够压力**: 可能需要更长的运行时间或更高的频率
4. **代码已被修复**: 可能在分析报告之后代码已经被优化

### 风险评估

即使当前测试通过，仍存在以下风险:

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| 生产环境负载更高 | 中 | 高 | 添加更严格的压力测试 |
| 长时间运行（24h+）暴露问题 | 中 | 高 | 实施长时间运行测试 |
| 异常情况下 Dispose 未执行 | 低 | 高 | 添加异常处理和监控 |
| .NET 版本升级导致问题 | 低 | 中 | 遵循 Rx 最佳实践 |

---

## 🔧 Proposed Solution

### 方案 1: 移除循环引用（推荐）🔴

**目标**: 消除 `_stateSubject` 的循环依赖

**实施步骤**:

1. **移除派生流**
   ```csharp
   // 删除这两段代码（AttendedWeighingService.cs:579-588）
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

2. **直接发送 Action**
   ```csharp
   // 在设置 DeliveryType 时直接发送 Action
   public void SetDeliveryType(DeliveryType deliveryType)
   {
       _actionSubject.OnNext(new SetDeliveryTypeAction(deliveryType));
   }

   // 在创建称重记录时直接发送 Action
   private async Task CreateWeighingRecordAsync(...)
   {
       // ... 创建记录逻辑
       _actionSubject.OnNext(new WeighingRecordCreatedAction(recordId));
   }
   ```

**优点**:
- ✅ 彻底消除循环引用
- ✅ 简化代码，提高可维护性
- ✅ 符合 Rx 最佳实践
- ✅ 性能更好（减少不必要的流操作）

**缺点**:
- ⚠️ 需要修改多个方法
- ⚠️ 需要回归测试

---

### 方案 2: 优化 ConcurrentBag 清理逻辑 🔴

**目标**: 使用 `ConcurrentQueue` + 定期清理替代 `ConcurrentBag` 重建逻辑

**实施步骤**:

1. **替换数据结构**
   ```csharp
   // 从 ConcurrentBag 改为 ConcurrentQueue
   private readonly ConcurrentQueue<(Task Task, DateTime CreatedTime)> _pendingOperations = new();
   ```

2. **实现定期清理**
   ```csharp
   private readonly Timer _cleanupTimer;

   // 在构造函数中启动清理定时器
   _cleanupTimer = new Timer(_ =>
   {
       var currentTime = DateTime.UtcNow;
       var completed = 0;
       var stuck = 0;

       // 清理超过 5 分钟的任务
       while (_pendingOperations.TryDequeue(out var item))
       {
           if (item.Task.IsCompleted)
           {
               completed++;
           }
           else if ((currentTime - item.CreatedTime).TotalMinutes > 5)
           {
               // 任务卡住超过 5 分钟，记录警告
               _logger?.LogWarning("Stuck task detected and removed");
               stuck++;
           }
           else
           {
               // 重新入队未完成的任务
               _pendingOperations.Enqueue(item);
           }
       }

       if (completed > 0 || stuck > 0)
       {
           _logger?.LogDebug($"Cleaned up {completed} completed tasks, {stuck} stuck tasks");
       }
   }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
   ```

3. **在 Dispose 时清理**
   ```csharp
   public async ValueTask DisposeAsync()
   {
       // 停止清理定时器
       _cleanupTimer?.Dispose();

       // 等待所有未完成的任务（带超时）
       var timeout = TimeSpan.FromSeconds(5);
       var startTime = DateTime.UtcNow;

       while (_pendingOperations.Count > 0 && (DateTime.UtcNow - startTime) < timeout)
       {
           if (_pendingOperations.TryDequeue(out var item))
           {
               if (!item.Task.IsCompleted)
               {
                   try
                   {
                       await item.Task.WaitAsync(TimeSpan.FromSeconds(1));
                   }
                   catch (Exception ex)
                   {
                       _logger?.LogError(ex, "Error waiting for task during disposal");
                   }
               }
           }
       }

       // ... 其他清理逻辑
   }
   ```

**优点**:
- ✅ 避免集合重建，性能更好
- ✅ 自动清理卡住的任务
- ✅ 线程安全
- ✅ 可监控和记录

**缺点**:
- ⚠️ 需要额外的定时器
- ⚠️ 稍微增加复杂性

---

### 方案 3: 优化 Buffer 操作符 🟡

**目标**: 为 `Buffer` 操作符添加大小限制

**实施步骤**:

```csharp
return sharedWeightSource
    .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
        TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
    .Select(buffer =>
    {
        // 限制最多处理 100 个数据点
        var limitedBuffer = buffer.Take(100).ToList();
        if (buffer.Count > 100)
        {
            _logger?.LogWarning($"Buffer has {buffer.Count} items, limited to 100");
        }
        return limitedBuffer;
    })
    .Where(buffer => buffer.Count > 0)
    .Select(buffer => CalculateStability(buffer));
```

**优点**:
- ✅ 防止内存无限增长
- ✅ 添加监控和日志
- ✅ 简单易实现

**缺点**:
- ⚠️ 可能丢失部分数据点
- ⚠️ 需要调整阈值

---

### 方案 4: 优化 Replay 操作符 🟡

**目标**: 添加采样或限制 Replay 缓冲区大小

**实施步骤**:

```csharp
// 方案 A: 添加采样（推荐）
var sharedWeightSource = _truckScaleWeightService.WeightUpdates
    .Sample(TimeSpan.FromMilliseconds(100)) // 采样到 10Hz
    .Replay(TimeSpan.FromSeconds(5))
    .RefCount();

// 方案 B: 限制缓冲区大小
var sharedWeightSource = _truckScaleWeightService.WeightUpdates
    .Replay(bufferSize: 50, window: TimeSpan.FromSeconds(5)) // 最多缓存 50 个数据点
    .RefCount();
```

**优点**:
- ✅ 减少内存占用
- ✅ 降低多订阅者时的内存放大效应
- ✅ 提高性能

**缺点**:
- ⚠️ 采样可能丢失数据
- ⚠️ 需要调整采样率或缓冲区大小

---

### 方案 5: 优化回调函数生命周期 🟡

**目标**: 在 `HikvisionService` 中使用标志位防止回调访问已释放的对象

**实施步骤**:

```csharp
private volatile bool _isDisposed = false;
private readonly object _streamLock = new();

NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
{
    // 快速退出
    if (_isDisposed) return;

    lock (_streamLock)
    {
        // 二次检查
        if (_isDisposed || decoder == null) return;

        // ... 处理逻辑
    }
};

// 在 Dispose 中
finally
{
    _isDisposed = true;
    if (decoder != null)
    {
        decoder.Dispose();
        decoder = null;
    }
}
```

**优点**:
- ✅ 简单有效
- ✅ 防止访问已释放的对象
- ✅ 符合最佳实践

**缺点**:
- ⚠️ 添加了额外的标志位检查

---

## 📐 Implementation Plan

### 阶段划分

#### 阶段 1: 准备和验证 (1-2 天)
- [ ] 审查现有测试代码
- [ ] 运行长时间基线测试（24 小时）
- [ ] 记录当前内存使用模式
- [ ] 建立性能基准

#### 阶段 2: 修复循环引用 (2-3 天)
- [ ] 移除 `deliveryTypeActions` 和 `recordIdActions`
- [ ] 修改 `SetDeliveryType` 方法直接发送 Action
- [ ] 修改创建称重记录逻辑直接发送 Action
- [ ] 更新单元测试
- [ ] 运行内存泄漏测试验证
- [ ] 代码审查

#### 阶段 3: 优化 ConcurrentBag (2-3 天)
- [ ] 替换为 `ConcurrentQueue`
- [ ] 实现定期清理定时器
- [ ] 添加超时处理和日志
- [ ] 更新 Dispose 逻辑
- [ ] 添加单元测试
- [ ] 运行压力测试验证
- [ ] 代码审查

#### 阶段 4: 优化 Buffer 和 Replay (1-2 天)
- [ ] 为 Buffer 添加大小限制
- [ ] 为 Replay 添加采样或限制
- [ ] 添加监控和日志
- [ ] 更新单元测试
- [ ] 运行高频数据测试
- [ ] 代码审查

#### 阶段 5: 优化 Hikvision 回调 (1-2 天)
- [ ] 添加 `_isDisposed` 标志位
- [ ] 在回调中添加快速退出
- [ ] 更新 Dispose 逻辑
- [ ] 添加单元测试
- [ ] 代码审查

#### 阶段 6: 集成测试和验证 (3-5 天)
- [ ] 运行完整测试套件
- [ ] 运行长时间运行测试（24 小时）
- [ ] 运行高频数据测试
- [ ] 运行压力测试
- [ ] 内存分析和性能基准测试
- [ ] 修复发现的问题

#### 阶段 7: 部署和监控 (持续)
- [ ] 部署到测试环境
- [ ] 监控内存使用情况
- [ ] 收集用户反馈
- [ ] 部署到生产环境
- [ ] 持续监控

### 里程碑

| 里程碑 | 目标日期 | 交付物 |
|--------|----------|--------|
| M1: 完成基线测试 | Day 2 | 基线测试报告 |
| M2: 完成循环引用修复 | Day 5 | 修复代码 + 测试通过 |
| M3: 完成 ConcurrentBag 优化 | Day 8 | 优化代码 + 测试通过 |
| M4: 完成 Buffer/Replay 优化 | Day 10 | 优化代码 + 测试通过 |
| M5: 完成回调优化 | Day 12 | 优化代码 + 测试通过 |
| M6: 完成集成测试 | Day 17 | 测试报告 |
| M7: 部署到生产 | Day 20+ | 生产监控报告 |

---

## ✅ Success Criteria

### 功能验收标准

- [ ] 所有现有单元测试通过
- [ ] 所有内存泄漏测试通过 (`AttendedWeighingServiceMemoryLeakTests`)
- [ ] 长时间运行测试通过（24 小时，内存增长 < 50 MB）
- [ ] 高频数据测试通过（100 Hz，内存增长 < 20 MB）
- [ ] 压力测试通过（1000 次操作，内存增长 < 50 MB）

### 性能验收标准

- [ ] 服务实例回收率 > 90%（GC 后）
- [ ] `_pendingOperations` 集合大小稳定（< 100 个任务）
- [ ] Dispose 耗时 < 1 秒
- [ ] 内存增长率 < 2 KB/s（长时间运行）
- [ ] CPU 使用率无明显增加

### 质量验收标准

- [ ] 代码审查通过
- [ ] 无新的警告或错误
- [ ] 代码覆盖率 > 80%
- [ ] 文档更新完整

---

## 🧪 Testing Strategy

### 单元测试

**现有测试** (已实现):
- ✅ `AttendedWeighingServiceMemoryLeakTests` (8 个测试)

**需要添加的测试**:
- [ ] `ConcurrentQueueCleanupTests` - 验证清理逻辑
- [ ] `BufferLimitTests` - 验证 Buffer 大小限制
- [ ] `ReplaySamplingTests` - 验证 Replay 采样
- [ ] `CallbackLifecycleTests` - 验证回调生命周期

### 集成测试

- [ ] 长时间运行测试（24 小时）
- [ ] 高频数据测试（100 Hz, 1 小时）
- [ ] 压力测试（1000 次操作/秒, 30 分钟）
- [ ] 异常恢复测试（模拟异常和恢复）

### 性能测试

- [ ] 内存使用基线测试
- [ ] CPU 使用率测试
- [ ] 响应时间测试
- [ ] 吞吐量测试

### 内存分析工具

- **dotMemory** (JetBrains): 详细的内存分析
- **PerfView** (Microsoft): 免费的内存分析工具
- **Visual Studio Profiler**: 内置性能分析器

---

## 🚨 Risks and Mitigation

### 已识别的风险

| 风险 | 可能性 | 影响 | 缓解措施 | 负责人 |
|------|--------|------|----------|--------|
| 修复引入新的 Bug | 中 | 高 | 完善的单元测试和集成测试 | 开发团队 |
| 性能下降 | 低 | 中 | 性能基准测试和对比 | 开发团队 |
| 生产环境仍出现内存泄漏 | 低 | 高 | 长时间运行测试和监控 | 测试团队 |
| 修复耗时过长 | 中 | 中 | 分阶段实施和验证 | 项目经理 |
| 测试不充分 | 中 | 高 | 多种测试场景和工具 | 测试团队 |

### 回滚计划

如果修复后出现严重问题：

1. **立即回滚**: 回退到修复前的版本
2. **分析原因**: 使用内存分析工具定位问题
3. **重新修复**: 根据分析结果调整修复方案
4. **重新测试**: 更严格的测试验证
5. **重新部署**: 确认无问题后重新部署

---

## 📊 Impact Assessment

### 对用户的影响

**正面影响**:
- ✅ 应用更加稳定，不易崩溃
- ✅ 长时间运行无需重启
- ✅ 响应速度更快
- ✅ 内存使用更少

**潜在负面影响**:
- ⚠️ 修复初期可能有少量 Bug
- ⚠️ 需要重新部署和测试

### 对开发团队的影响

**工作量**:
- 开发: 10-15 人天
- 测试: 5-8 人天
- 代码审查: 2-3 人天
- **总计**: 17-26 人天

**学习曲线**:
- 需要学习 Rx 最佳实践
- 需要学习内存分析工具
- 需要了解新的代码结构

### 对系统的影响

**兼容性**:
- ✅ 向后兼容，无需修改调用方代码
- ✅ API 接口不变
- ✅ 数据库模式不变

**性能**:
- ✅ 预期性能提升或持平
- ✅ 内存使用减少
- ✅ CPU 使用率持平

---

## 📚 Related Resources

### 文档

- `docs/内存溢出问题分析报告.md` - 内存泄漏问题分析
- `docs/AttendedWeighingService-MemoryLeak-Testing-Guide.md` - 测试指南
- `docs/AttendedWeighingService-RxState-Optimization-Report.md` - RxState 优化报告
- `docs/AttendedWeighingService-Rx-Evaluation-Report.md` - Rx 评估报告

### 相关代码

- `MaterialClient.Common/Services/AttendedWeighingService.cs` - 核心服务
- `MaterialClient.Common/Services/WeighingServiceState.cs` - 状态定义
- `MaterialClient.Common/Services/WeighingServiceStateReducer.cs` - 状态转换器
- `MaterialClient.Common.Tests/Tests/AttendedWeighingServiceMemoryLeakTests.cs` - 内存泄漏测试

### 相关提交

- `9c60de1` - Test: Add memory leak tests for AttendedWeighingService

### 外部资源

- [System.Reactive 最佳实践](https://github.com/dotnet/reactive)
- [.NET 内存管理指南](https://docs.microsoft.com/en-us/dotnet/standard/memory-management)
- [内存分析工具对比](https://www.jetbrains.com/help/dotmemory/)

---

## 📝 Notes

### 关键决策

1. **优先修复循环引用**: 这是最严重的问题，可能导致 OOM
2. **分阶段实施**: 降低风险，便于验证
3. **完善的测试**: 确保修复不引入新的问题
4. **持续监控**: 部署后持续监控内存使用情况

### 未解决的问题

- [ ] 为什么当前测试全部通过？（需要进一步调查）
- [ ] 生产环境是否会出现内存泄漏？（需要监控）
- [ ] 是否需要更严格的测试？（待定）

### 开放问题

1. 是否需要立即修复，还是先监控一段时间？
2. 是否需要添加更多的监控和日志？
3. 是否需要优化其他类似的服务？

---

## 📅 Timeline

```
Week 1: 准备和验证 + 循环引用修复
  Day 1-2: 准备和基线测试
  Day 3-5: 循环引用修复和测试

Week 2: ConcurrentBag 优化 + Buffer/Replay 优化
  Day 6-8: ConcurrentBag 优化
  Day 9-10: Buffer/Replay 优化

Week 3: 回调优化 + 集成测试
  Day 11-12: 回调优化
  Day 13-17: 集成测试和验证

Week 4+: 部署和监控
  Day 18-20: 部署到测试环境
  Day 21+: 部署到生产环境和持续监控
```

---

## ✍️ Authors

- **主要作者**: Claude Sonnet 4.5
- **审核者**: 待定
- **批准者**: 待定

---

## 📄 Approval

| 角色 | 姓名 | 日期 | 状态 |
|------|------|------|------|
| 开发负责人 | | | ⏳ 待审核 |
| 测试负责人 | | | ⏳ 待审核 |
| 项目经理 | | | ⏳ 待审核 |
| 技术负责人 | | | ⏳ 待审核 |

---

**文档版本**: 1.0
**最后更新**: 2026-01-13
