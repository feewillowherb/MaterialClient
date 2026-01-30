# 变更: 统一 LPR 数据传递方式和主动抓拍接口

**变更 ID**: `unified-lpr-data-transfer-and-capture-interface`
**状态**: 草稿
**创建日期**: 2026-01-29
**类型**: 重构/功能增强

---

## Why

### Background

MaterialClient 系统集成了多种车牌识别(LPR)硬件设备,包括:
- **海康威视(Hikvision)** LPR 设备 - 使用 HCNetSDK
- **LprAllInOne** 设备 - 使用 HTTP 轮询机制
- **华夏智信(Huaxiazhixin)** 设备 - 使用 HTTP 回调

当前系统中,LPR 功能存在以下不一致性:

1. **车牌数据传递机制不统一**:各 LPR 设备服务使用不同的方式将识别结果传递给业务逻辑层(`AttendedWeighingService`)
   - 海康威视设备:通过 `MinimalWebHostService` HTTP 回调直接调用 `weighingService.OnPlateNumberRecognized()`
   - LprAllInOne 设备:同样通过 `MinimalWebHostService` HTTP 回调直接调用服务方法
   - 缺乏统一的事件驱动架构

2. **主动抓拍功能不完整**:
   - ✅ **已实现**: `LprAllInOneService` 支持通过 `TriggerManualRecognitionAsync()` 主动触发抓拍
   - ❌ **未实现**: `HikvisionLprService` 缺少基于 `NET_DVR_ContinuousShoot` 的主动抓拍功能
   - ❌ **未实现**: `HuaxiazhixinLprService` 不存在,厂商不支持主动抓拍

3. **缺乏统一的 LPR 设备抽象层**:
   - 没有标准化的 `ILprDevice` 接口定义主动抓拍和车牌写入行为
   - 硬件特定逻辑散落在各服务实现中,难以维护和扩展

### 技术债务

- **紧耦合**:硬件回调处理程序(`MinimalWebHostService`)直接依赖业务服务(`IAttendedWeighingService`),违反依赖倒置原则
- **可测试性差**:无法独立测试硬件回调逻辑和业务处理逻辑
- **可扩展性差**:添加新的订阅者(日志记录、统计、报警)需要修改现有代码
- **架构不一致**:与应用程序中其他使用 MessageBus 进行跨组件通信的模式不一致(ADR-009)

---

## What Changes

### Overview

重构 LPR 车牌识别事件的交付机制,从直接方法调用改为使用 ReactiveUI MessageBus,并实现统一的主动抓拍接口。主要变更包括:

1. **统一事件传递**:所有 LPR 设备通过 MessageBus 发布 `LicensePlateRecognizedMessage`
2. **实现主动抓拍接口**:为 `HikvisionLprService` 添加主动抓拍能力,定义统一的 `ILprDevice` 接口
3. **解耦架构层次**:硬件集成层不再直接依赖业务逻辑层

### Detailed Changes

#### 1. 统一车牌数据传递方式

**创建统一的消息类**:
```csharp
namespace MaterialClient.Common.Events;

/// <summary>
///     车牌识别消息(通过 ReactiveUI MessageBus 发送)
/// </summary>
public class LicensePlateRecognizedMessage
{
    public string PlateNumber { get; set; }
    public LprAllInOneColorType? ColorType { get; set; }
    public LprDeviceType DeviceType { get; set; }
    public string DeviceName { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**重构回调处理程序**:
- 移除 `MinimalWebHostService` 对 `IAttendedWeighingService` 的直接依赖
- 改为发布 `LicensePlateRecognizedMessage` 到 MessageBus
- 适用于所有三种 LPR 设备类型(Hikvision、LprAllInOne、Huaxiazhixin)

**修改业务服务**:
- `AttendedWeighingService` 订阅 `LicensePlateRecognizedMessage`
- `OnPlateNumberRecognized()` 方法从接口中移除,变为私有方法
- 保持现有的车牌缓存和推荐逻辑不变

#### 2. 实现主动抓拍接口

**定义统一的 LPR 设备接口**:
```csharp
namespace MaterialClient.Common.Services;

/// <summary>
///     统一的 LPR 设备接口,提供主动抓拍能力
/// </summary>
public interface ILprDevice
{
    /// <summary>
    ///     主动触发抓拍
    ///     返回可观察的车牌识别事件流
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>识别结果事件流</returns>
    IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     设备是否支持主动抓拍
    /// </summary>
    bool SupportsActiveCapture { get; }
}
```

**HikvisionLprService 实现**:
- 实现 `ILprDevice` 接口
- 添加 `TriggerCaptureAsync()` 方法:
  - **会话管理**: 参考 `HikvisionService` 的 `EnsureLogin()` 设计,使用 `ConcurrentDictionary` 缓存登录会话(userId),避免频繁重复的登入登出
  - 调用 `NET_DVR_Login_V40()` 登录设备(仅在会话不存在时)
  - 调用 `NET_DVR_ContinuousShoot()` 触发抓拍
  - 订阅现有的 `PlateRecognized` 事件流并返回结果
  - 在可观察流完成或取消时清理资源(但不登出设备,保持会话复用)
- 设置 `SupportsActiveCapture = true`

**LprAllInOneService 适配**:
- 适配现有的 `TriggerManualRecognitionAsync()` 到新接口
- 保持现有的标志位轮询机制

**HuaxiazhixinLprService 占位实现**:
- 创建 `HuaxiazhixinLprService` 类(如果不存在)
- 实现 `ILprDevice` 接口
- 设置 `SupportsActiveCapture = false`
- 在方法中记录日志说明厂商限制

#### 3. 更新服务接口

**修改 IHikvisionLprService**:
- 移除 `IObservable<LicensePlateRecognizedEvent> PlateRecognized` (保留内部实现,但不在接口中暴露)
- 如果需要保留此属性,添加明确的文档说明使用场景

**保持向后兼容**:
- `IHikvisionLprService` 的现有方法保持不变(`StartAsync`, `StopAsync`, `AddOrUpdateDevice`, `IsOnline`)
- `ILprAllInOneService` 的现有方法保持不变
- 仅添加新接口,不破坏现有调用代码

---

## Impact

### Expected Benefits

1. **降低耦合度**:
   - 硬件集成层不再直接依赖业务逻辑层
   - 符合依赖倒置原则和单一职责原则

2. **提高可测试性**:
   - 可以独立测试硬件回调处理程序(验证 MessageBus 消息)
   - 可以独立测试业务逻辑(模拟 MessageBus 消息)
   - 无需真实硬件设备即可完成单元测试

3. **增强可扩展性**:
   - 添加新的订阅者(日志、统计、监控)无需修改现有代码
   - 新增 LPR 设备类型只需实现 `ILprDevice` 接口并发布消息
   - 符合开放封闭原则

4. **完善主动抓拍功能**:
   - 海康威视设备支持主动抓拍,提升用户体验
   - 统一的接口使上层调用代码无需关心设备类型
   - 为未来扩展新设备类型打下基础

5. **架构一致性**:
   - 与 ADR-009(MessageBus 用于跨组件通信)保持一致
   - 与响应式编程模式(`timer-to-rx-pattern.md`)保持一致
   - 统一的事件传递模式降低认知负担

6. **性能优化**:
   - MessageBus 提供同步交付(<1ms 延迟),适合高频 LPR 事件(每分钟 10-20 次)
   - 避免不必要的异步开销

### Risks and Mitigations

| 风险 | 影响 | 缓解措施 |
|------|--------|----------|
| **内存泄漏** | 高 | 确保 MessageBus 订阅正确释放;添加内存泄漏测试;使用 `DisposeWith()` 模式 |
| **破坏现有集成** | 中 | 保持 `OnPlateNumberRecognized()` 方法逻辑不变;分阶段迁移;全面的集成测试 |
| **LPR 功能回归** | 中 | 对所有设备类型进行集成测试;保留旧代码路径作为备份;详细的回滚计划 |
| **海康威视主动抓拍实现复杂** | 中 | 参考 `HikvisionService` 的现有实现;使用真实设备进行测试;添加详细错误处理 |
| **性能开销** | 低 | MessageBus 是轻量级同步调用;开销可忽略不计(<1ms vs 直接调用的 0.1ms) |
| **向后兼容性** | 低 | 添加新接口,不修改现有接口;旧代码继续工作;逐步迁移 |

---

## Success Criteria

- [x] `LicensePlateRecognizedMessage` 类已创建,包含所有必需属性
- [ ] `MinimalWebHostService` 中的所有 LPR 回调处理程序已重构为发布 MessageBus 消息
- [ ] `AttendedWeighingService` 订阅 `LicensePlateRecognizedMessage` 并正确处理
- [ ] `OnPlateNumberRecognized()` 从 `IAttendedWeighingService` 接口中移除
- [ ] 所有三种 LPR 设备类型(Hikvision、LprAllInOne、Huaxiazhixin)的功能正常工作
- [ ] MessageBus 订阅在 `DisposeAsync()` 中正确释放
- [ ] `ILprDevice` 接口已定义并包含主动抓拍方法
- [ ] `HikvisionLprService` 实现 `ILprDevice` 接口,支持主动抓拍
- [ ] `LprAllInOneService` 适配 `ILprDevice` 接口
- [ ] `HuaxiazhixinLprService` 占位实现已创建,`SupportsActiveCapture` 返回 `false`
- [ ] 主动抓拍功能在真实海康威视设备上测试通过
- [ ] 单元测试覆盖消息发布和订阅逻辑
- [ ] 集成测试覆盖所有三种设备类型的端到端流程
- [ ] 内存泄漏测试通过(1000+ 消息周期后无内存增长)
- [ ] 文档已更新(MessageBus 使用指南、主动抓拍接口文档)
- [ ] 代码审查通过

---

## Next Steps

1. **审查并批准提案**:与团队一起审查此提案,确认技术方案和范围
2. **创建设计文档**:创建详细的 `design.md` 说明架构决策和实现细节
3. **创建规范变更**:更新 `license-plate-recognition` 规范中的相关需求
4. **实现消息类**:创建 `LicensePlateRecognizedMessage` 及其 XML 文档
5. **重构回调处理程序**:修改 `MinimalWebHostService` 发布消息而非直接调用
6. **添加服务订阅**:在 `AttendedWeighingService` 中实现 MessageBus 订阅和释放
7. **更新服务接口**:从公共接口中移除 `OnPlateNumberRecognized()`
8. **实现主动抓拍接口**:定义 `ILprDevice` 并在各个服务中实现
9. **实现海康威视主动抓拍**:基于 `NET_DVR_ContinuousShoot` 实现主动抓拍功能
10. **编写测试**:创建单元测试、集成测试和内存泄漏测试
11. **文档更新**:更新用户和开发文档,说明配置和用法
12. **代码审查**:提交代码审查,确保代码质量和一致性

---

## References

- **ADR-009**: `docs/SDD.md:1654-1693` - MessageBus 用于跨组件通信
- **响应式模式**: `openspec/docs/timer-to-rx-pattern.md` - 系统中的响应式编程模式
- **相关变更**:
  - `unify-lpr-events-with-messagebus` - 统一 LPR 事件使用 MessageBus(本提案的子集)
  - `hikvision-lpr-implementation` - 海康威视 LPR 服务实现
  - `hikvision-lpr-integration` - 海康威视 LPR 配置和 UI
- **现有事件**:
  - `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs` - ABP 事件(未使用,将被弃用)
  - `MaterialClient.Common/Events/PlateNumberChangedMessage.cs` - 内部 UI 通知消息
- **规范**: `openspec/specs/license-plate-recognition` - 车牌识别功能规范
- **现有服务**:
  - `MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs` - 海康威视 LPR 服务
  - `MaterialClient.Common/Services/LprAllInOne/LprAllInOneService.cs` - LprAllInOne 服务
  - `MaterialClient.Common/Services/Hikvision/HikvisionService.cs` - 海康威视摄像头服务(参考主动抓拍实现)
