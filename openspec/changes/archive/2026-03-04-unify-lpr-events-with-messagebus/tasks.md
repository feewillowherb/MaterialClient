# 任务：使用 MessageBus 统一 LPR 车牌识别事件

**变更 ID**：`unify-lpr-events-with-messagebus`
**任务总数**：15
**预估周期**：3–4 天

---

## 任务概述

将 LPR 事件从直接方法调用重构为 ReactiveUI MessageBus 投递。包括创建统一消息类、将回调处理改为发布消息、在业务服务中增加订阅，并确保正确释放资源。工作按阶段组织：消息与接口、回调重构、服务订阅、测试与文档。

---

## 阶段 1：基础 - 消息与接口

### 任务 1.1：创建 LicensePlateRecognizedMessage 类

**状态**：待办
**优先级**：高
**预估**：1 小时

**描述**：
创建新的 MessageBus 消息类，用于从硬件回调向业务服务传递 LPR 识别数据。

**步骤**：
1. 创建文件 `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`
2. 定义属性：`PlateNumber`、`ColorType`、`DeviceType`、`DeviceName`、`Timestamp`
3. 为所有成员添加 XML 文档注释
4. 类设为 `public`，使用无参构造函数或主构造函数

**验证**：
- [ ] 文件在正确路径创建
- [ ] 所有属性类型正确
- [ ] XML 文档完整
- [ ] 代码可编译无错误

**产出**：`MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

---

### 任务 1.2：从服务接口中移除 OnPlateNumberRecognized

**状态**：待办
**优先级**：高
**预估**：0.5 小时

**描述**：
从 `IAttendedWeighingService` 接口中移除 `OnPlateNumberRecognized` 方法，因其已成为内部实现细节。

**步骤**：
1. 打开 `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. 从接口定义中移除 `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);`
3. 在具体类中保留方法实现（将通过 MessageBus 订阅调用）
4. 若接口 XML 文档中提及该方法，则更新文档

**验证**：
- [ ] 仅从接口移除方法
- [ ] 具体类中实现仍存在
- [ ] 代码可编译无错误
- [ ] 无未使用方法的构建警告

**产出**：修改后的 `IAttendedWeighingService` 接口

---

## 阶段 2：硬件回调重构

### 任务 2.1：重构海康 LPR 回调处理

**状态**：待办
**优先级**：高
**预估**：1 小时

**描述**：
修改 `MinimalWebHostService` 中的海康 LPR 回调，改为发布 MessageBus 消息而非直接调用服务方法。

**步骤**：
1. 打开 `MaterialClient/Services/MinimalWebHostService.cs`
2. 定位海康回调处理（约 188–200 行）
3. 移除获取 `IAttendedWeighingService` 的依赖
4. 将直接调用替换为 `MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage { ... })`
5. 设置 `DeviceType = LprDeviceType.Hikvision`
6. 从回调数据中提取设备名称

**验证**：
- [ ] 直接服务调用已移除
- [ ] MessageBus 消息属性正确
- [ ] DeviceType 设为 Hikvision
- [ ] 代码可编译无错误

**产出**：修改后的海康回调处理

---

### 任务 2.2：重构 LprAllInOne HTTP 回调处理

**状态**：待办
**优先级**：高
**预估**：1 小时

**描述**：
修改 LprAllInOne HTTP 回调处理，改为发布 MessageBus 消息。

**步骤**：
1. 定位 `MinimalWebHostService` 中的 LprAllInOne 回调（约 347–361 行）
2. 移除获取 `IAttendedWeighingService` 的依赖
3. 将直接调用替换为 `MessageBus.Current.SendMessage()`
4. 设置 `DeviceType = LprDeviceType.LprAllInOne`
5. 从请求数据或配置中提取设备名称

**验证**：
- [ ] 直接服务调用已移除
- [ ] 已发布 MessageBus 消息
- [ ] DeviceType 设置正确
- [ ] 原有日志保留

**产出**：修改后的 LprAllInOne 回调处理

---

### 任务 2.3：重构华夏智信 HTTP 回调处理

**状态**：待办
**优先级**：中
**预估**：1 小时

**描述**：
修改华夏智信 HTTP 回调处理，改为发布 MessageBus 消息。

**步骤**：
1. 定位 `MinimalWebHostService` 中的华夏智信回调
2. 移除获取 `IAttendedWeighingService` 的依赖
3. 将直接调用替换为 `MessageBus.Current.SendMessage()`
4. 设置 `DeviceType = LprDeviceType.Huaxiazhixin`
5. 从请求数据或配置中提取设备名称

**验证**：
- [ ] 直接服务调用已移除
- [ ] 已发布 MessageBus 消息
- [ ] DeviceType 设置正确
- [ ] 原有日志保留

**产出**：修改后的华夏智信回调处理

---

## 阶段 3：业务服务集成

### 任务 3.1：在 AttendedWeighingService 中增加 MessageBus 订阅

**状态**：待办
**优先级**：高
**预估**：2 小时

**描述**：
在 `AttendedWeighingService` 构造函数中增加 MessageBus 订阅，以接收 LPR 识别消息。

**步骤**：
1. 打开 `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. 增加私有字段 `IDisposable _licensePlateSubscription`
3. 在构造函数中创建订阅：
   ```csharp
   _licensePlateSubscription = MessageBus.Current
       .Listen<LicensePlateRecognizedMessage>()
       .Subscribe(msg => OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType));
   ```
4. 在 `DisposeAsync()` 中释放该订阅

**验证**：
- [ ] 已添加订阅字段
- [ ] 在构造函数中创建订阅
- [ ] 复用现有 `OnPlateNumberRecognized()` 方法
- [ ] 在 `DisposeAsync()` 中释放订阅
- [ ] 代码可编译无错误

**产出**：带 MessageBus 订阅的修改后 `AttendedWeighingService`

---

### 任务 3.2：将 OnPlateNumberRecognized 改为 private/internal

**状态**：待办
**优先级**：低
**预估**：0.5 小时

**描述**：
将 `OnPlateNumberRecognized` 的可见性从 public 改为 private 或 internal，因其不再属于公开接口。

**步骤**：
1. 在 `AttendedWeighingService` 中定位 `OnPlateNumberRecognized` 方法
2. 将可见性从 `public` 改为 `private`
3. 在 XML 文档中注明由 MessageBus 调用

**验证**：
- [ ] 可见性已改为 private
- [ ] 代码可编译无错误
- [ ] 无外部代码直接引用该方法

**产出**：具有 private 可见性的 `OnPlateNumberRecognized` 方法

---

## 阶段 4：测试

### 任务 4.1：编写消息发布的单元测试

**状态**：待办
**优先级**：高
**预估**：2 小时

**描述**：
编写单元测试，验证回调处理会发布正确的消息。

**步骤**：
1. 创建测试文件 `MaterialClient.Common.Tests/Tests/LicensePlateRecognizedMessageTests.cs`
2. 测试海康回调发布的消息属性正确
3. 测试 LprAllInOne 回调发布的消息属性正确
4. 测试华夏智信回调发布的消息属性正确
5. 使用测试隔离手段 Mock `MessageBus.Current`
6. 验证设备相关属性（DeviceType、DeviceName）

**验证**：
- [ ] 测试文件已创建
- [ ] 三种设备类型均有测试
- [ ] 消息属性已验证
- [ ] 测试稳定通过

**产出**：消息发布的单元测试集

---

### 任务 4.2：编写消息订阅的单元测试

**状态**：待办
**优先级**：高
**预估**：2 小时

**描述**：
编写单元测试，验证 `AttendedWeighingService` 正确订阅并处理 LPR 消息。

**步骤**：
1. 创建或扩展 `AttendedWeighingService` 的测试文件
2. 测试构造函数中创建了订阅
3. 测试收到消息时会调用 `OnPlateNumberRecognized`
4. 测试 `DisposeAsync()` 中的订阅释放
5. 测试多条消息处理（车牌缓存逻辑）
6. 使用 Mock 服务隔离订阅行为

**验证**：
- [ ] 订阅创建已验证
- [ ] 消息处理已验证
- [ ] 释放逻辑已测试
- [ ] 测试中无内存泄漏
- [ ] 测试稳定通过

**产出**：消息订阅的单元测试集

---

### 任务 4.3：编写端到端流程的集成测试

**状态**：待办
**优先级**：高
**预估**：3 小时

**描述**：
编写集成测试，模拟从硬件回调经 MessageBus 到业务逻辑的完整流程。

**步骤**：
1. 创建集成测试文件 `MaterialClient.Common.Tests/Integration/LprEventFlowTests.cs`
2. 测试完整流程：硬件回调 → MessageBus → 服务处理 → UI 通知
3. 对每种设备类型使用 Mock 硬件模拟器
4. 验证车牌缓存逻辑正确
5. 验证仍向 UI 发送 `PlateNumberChangedMessage`
6. 测试错误处理（无效车牌、空值）

**验证**：
- [ ] 集成测试文件已创建
- [ ] 所有设备类型均有端到端测试
- [ ] 车牌缓存已验证
- [ ] UI 通知已验证
- [ ] 错误分支已覆盖
- [ ] 测试稳定通过

**产出**：LPR 事件流的集成测试集

---

### 任务 4.4：运行内存泄漏测试

**状态**：待办
**优先级**：高
**预估**：2 小时

**描述**：
运行内存泄漏测试，确保 MessageBus 订阅不会导致泄漏。

**步骤**：
1. 扩展现有 `AttendedWeighingServiceMemoryLeakTests`
2. 增加重复订阅与释放的测试
3. 在 1000+ 次消息循环下观察内存增长
4. 验证订阅被正确释放
5. 检查是否存在对消息处理器的残留引用
6. 若可用，使用 dotMemory 等分析工具

**验证**：
- [ ] 内存泄漏测试已创建
- [ ] 1000+ 次循环后无内存增长
- [ ] 订阅被正确释放
- [ ] 测试稳定通过

**产出**：内存泄漏测试结果

---

## 阶段 5：文档与收尾

### 任务 5.1：更新软件设计文档

**状态**：待办
**优先级**：中
**预估**：1 小时

**描述**：
更新 `docs/SDD.md`，说明 LPR 事件对 MessageBus 的使用，并澄清事件系统架构。

**步骤**：
1. 在 ADR-009 中补充 LPR 事件示例
2. 补充何时使用 MessageBus 与 LocalEventBus 的指导
3. 记录 `LicensePlateRecognizedMessage` 的用法
4. 增加订阅防内存泄漏的指南
5. 交叉引用相关 ADR 与模式

**验证**：
- [ ] ADR-009 已补充 LPR 示例
- [ ] MessageBus 与 LocalEventBus 指导已添加
- [ ] 内存泄漏指南已记录
- [ ] 文档可正常构建

**产出**：更新后的 `docs/SDD.md`

---

### 任务 5.2：编写 LPR 集成文档

**状态**：待办
**优先级**：低
**预估**：1.5 小时

**描述**：
创建或更新文档，说明 LPR 设备如何通过 MessageBus 与系统集成。

**步骤**：
1. 创建文件 `openspec/docs/lpr-event-architecture.md`
2. 描述事件流：硬件 → 回调 → MessageBus → 服务
3. 包含新增 LPR 设备类型的代码示例
4. 说明订阅模式与释放要求
5. 添加常见问题排查指南

**验证**：
- [ ] 文档文件已创建
- [ ] 事件流说明清晰
- [ ] 提供代码示例
- [ ] 包含排查指南
- [ ] 文档经审阅清晰易懂

**产出**：`openspec/docs/lpr-event-architecture.md`

---

### 任务 5.3：弃用未使用的 LicensePlateRecognizedEvent

**状态**：待办
**优先级**：低
**预估**：0.5 小时

**描述**：
将未使用的 ABP 事件 `LicensePlateRecognizedEvent` 标记为过时或完全移除。

**步骤**：
1. 打开 `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs`
2. 添加带弃用说明的 `[Obsolete]` 特性
3. 在代码库中搜索对该事件的使用
4. 若未使用，可考虑直接删除文件
5. 更新相关文档

**验证**：
- [ ] 事件已标记为过时或已移除
- [ ] 代码库中无活跃引用
- [ ] 文档已更新
- [ ] 代码可编译无错误

**产出**：已弃用或已移除的 `LicensePlateRecognizedEvent`

---

### 任务 5.4：更新 OpenSpec 规格

**状态**：待办
**优先级**：中
**预估**：1 小时

**描述**：
更新 `license-plate-recognition` 规格，体现基于 MessageBus 的架构。

**步骤**：
1. 打开 `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md`
2. 增加 LPR 事件处理的 MODIFIED 需求
3. 将场景更新为引用 MessageBus 而非直接调用
4. 增加 MessageBus 订阅与释放的场景
5. 运行 `openspec validate unify-lpr-events-with-messagebus --strict`

**验证**：
- [ ] 已创建带 MODIFIED 需求的规格增量
- [ ] 场景已更新为 MessageBus 模式
- [ ] 验证无错误通过
- [ ] 规格清晰完整

**产出**：更新后的 `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md`

---

## 进度跟踪

**阶段 1 进度**：0/2 任务完成（0%）
**阶段 2 进度**：0/3 任务完成（0%）
**阶段 3 进度**：0/2 任务完成（0%）
**阶段 4 进度**：0/4 任务完成（0%）
**阶段 5 进度**：0/4 任务完成（0%）
**总进度**：0/15 任务（0%）

---

## 依赖与并行

**可并行**：
- 阶段 1 各任务（相互独立）
- 阶段 2 各任务（各设备处理独立）
- 任务 4.1 与 4.2（单元测试可并行编写）
- 任务 5.1、5.2、5.3（文档任务独立）

**须按序**：
- 阶段 1 须在阶段 3 之前完成
- 阶段 2 与阶段 3 应在阶段 4 之前完成（集成测试需发布方与订阅方就绪）
- 阶段 4 须在阶段 5 文档定稿前完成
