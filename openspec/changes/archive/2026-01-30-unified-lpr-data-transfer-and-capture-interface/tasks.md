# 任务: 统一 LPR 数据传递方式和主动抓拍接口

**变更 ID**: `unified-lpr-data-transfer-and-capture-interface`
**总任务数**: 28
**预计工期**: 1-2 周

---

## 任务概览

本变更将 LPR 车牌识别事件从直接方法调用重构为使用 ReactiveUI MessageBus,并实现统一的主动抓拍接口。工作分为五个阶段:基础设施、回调处理程序重构、业务服务集成、主动抓拍接口实现、测试和文档。

---

## 阶段 1: 基础设施和接口定义

### 任务 1.1: 创建 LicensePlateRecognizedMessage 类

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 1 小时

**描述**:
创建统一的消息类,用于从硬件回调处理程序向业务服务传递车牌识别数据。

**步骤**:
1. 创建文件 `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`
2. 定义属性: `PlateNumber`, `ColorType`, `DeviceType`, `DeviceName`, `Timestamp`
3. 为所有成员添加 XML 文档注释
4. 标记类为 `public` 并提供无参构造函数或主构造函数

**验收标准**:
- [x] 文件在正确路径创建
- [x] 所有属性使用正确的类型定义
- [x] XML 文档注释完整
- [ ] 代码编译无错误

**输出**: `MaterialClient.Common/Events/LicensePlateRecognizedMessage.cs`

---

### 任务 1.2: 定义 ILprDevice 统一接口

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 1.5 小时

**描述**:
创建统一的 LPR 设备接口,定义主动抓拍能力标准。

**步骤**:
1. 创建文件 `MaterialClient.Common/Services/ILprDevice.cs`
2. 定义 `TriggerCaptureAsync()` 方法签名
3. 定义 `SupportsActiveCapture` 属性
4. 添加详细的 XML 文档注释,说明每个方法的用途和使用场景
5. 考虑添加其他通用方法(如 `GetDeviceStatus()`, `GetLastRecognitionResult()`)

**验收标准**:
- [x] 接口文件已创建
- [x] 方法和属性定义清晰
- [x] XML 文档完整
- [x] 接口命名符合项目约定
- [ ] 代码编译无错误

**输出**: `MaterialClient.Common/Services/ILprDevice.cs`

---

### 任务 1.3: 从服务接口移除 OnPlateNumberRecognized

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 0.5 小时

**描述**:
从 `IAttendedWeighingService` 接口中移除 `OnPlateNumberRecognized` 方法,因为它将成为内部实现细节。

**步骤**:
1. 打开 `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. 从接口定义中移除 `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);`
3. 保持具体类中的方法实现(将通过 MessageBus 订阅调用)
4. 更新接口 XML 文档(如果引用了此方法)

**验收标准**:
- [x] 方法仅从接口中移除
- [x] 方法实现仍在具体类中存在
- [ ] 代码编译无错误
- [ ] 无构建警告关于未使用的方法

**输出**: 修改后的 `IAttendedWeighingService` 接口

---

## 阶段 2: 硬件回调处理程序重构

### 任务 2.1: 重构海康威视 LPR 回调处理程序

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 1.5 小时

**描述**:
修改 `MinimalWebHostService` 中的海康威视 LPR 回调处理程序,发布 MessageBus 消息而非直接调用服务方法。

**步骤**:
1. 打开 `MaterialClient/Services/MinimalWebHostService.cs`
2. 定位海康威视回调处理程序(约第 188-200 行)
3. 移除 `IAttendedWeighingService` 依赖解析
4. 替换直接调用为 `MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage { ... })`
5. 设置 `DeviceType = LprDeviceType.Hikvision`
6. 从回调数据中提取设备名称

**验收标准**:
- [x] 直接服务调用已移除
- [x] MessageBus 消息已发布,属性正确
- [x] DeviceType 设置为 Hikvision
- [ ] 代码编译无错误
- [x] 现有日志记录保留

**输出**: 修改后的海康威视回调处理程序

---

### 任务 2.2: 重构 LprAllInOne HTTP 回调处理程序

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 1.5 小时

**描述**:
修改 LprAllInOne HTTP 回调处理程序,发布 MessageBus 消息。

**步骤**:
1. 定位 LprAllInOne 回调处理程序(约第 347-361 行)
2. 移除 `IAttendedWeighingService` 依赖解析
3. 替换直接调用为 `MessageBus.Current.SendMessage()`
4. 设置 `DeviceType = LprDeviceType.LprAllInOne`
5. 从请求数据或配置中提取设备名称

**验收标准**:
- [x] 直接服务调用已移除
- [x] MessageBus 消息已发布
- [x] DeviceType 设置正确
- [x] 现有日志记录保留
- [x] HTTP 200 响应正确返回

**输出**: 修改后的 LprAllInOne 回调处理程序

---

### 任务 2.3: 重构华夏智信 HTTP 回调处理程序

**状态**: ✅ 已完成
**优先级**: 中
**预计时间**: 1.5 小时

**描述**:
修改华夏智信 HTTP 回调处理程序,发布 MessageBus 消息。

**步骤**:
1. 定位华夏智信回调处理程序(如果存在)
2. 移除 `IAttendedWeighingService` 依赖解析
3. 替换直接调用为 `MessageBus.Current.SendMessage()`
4. 设置 `DeviceType = LprDeviceType.Huaxiazhixin`
5. 从请求数据或配置中提取设备名称

**验收标准**:
- [x] 直接服务调用已移除
- [x] MessageBus 消息已发布
- [x] DeviceType 设置正确
- [x] 现有日志记录保留
- [x] HTTP 200 响应正确返回

**输出**: 修改后的华夏智信回调处理程序

---

## 阶段 3: 业务服务集成

### 任务 3.1: 在 AttendedWeighingService 中添加 MessageBus 订阅

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 2 小时

**描述**:
在 `AttendedWeighingService` 构造函数中添加 MessageBus 订阅,以接收 LPR 识别消息。

**步骤**:
1. 打开 `MaterialClient.Common/Services/AttendedWeighingService.cs`
2. 添加私有字段 `IDisposable _licensePlateSubscription`
3. 在构造函数中创建订阅:
   ```csharp
   _licensePlateSubscription = MessageBus.Current
       .Listen<LicensePlateRecognizedMessage>()
       .Subscribe(msg => OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType));
   ```
4. 确保订阅在 `DisposeAsync()` 方法中释放

**验收标准**:
- [x] 订阅字段已添加
- [x] 订阅在构造函数中创建
- [x] 现有的 `OnPlateNumberRecognized()` 方法被复用
- [x] 订阅在 `DisposeAsync()` 中释放
- [ ] 代码编译无错误

**输出**: 修改后的 `AttendedWeighingService`,包含 MessageBus 订阅

---

### 任务 3.2: 使 OnPlateNumberRecognized 成为私有方法

**状态**: ✅ 已完成
**优先级**: 低
**预计时间**: 0.5 小时

**描述**:
将 `OnPlateNumberRecognized` 的可见性从 public 更改为 private 或 internal,因为它不再是公共接口的一部分。

**步骤**:
1. 定位 `AttendedWeighingService` 中的 `OnPlateNumberRecognized` 方法
2. 将可见性从 `public` 更改为 `private`
3. 更新 XML 文档,说明它通过 MessageBus 调用

**验收标准**:
- [x] 可见性更改为 private
- [ ] 代码编译无错误
- [x] 无外部代码直接引用此方法
- [x] XML 文档已更新

**输出**: `OnPlateNumberRecognized` 方法,具有私有可见性

---

## 阶段 4: 主动抓拍接口实现

### 任务 4.1: HikvisionLprService 实现 ILprDevice 接口

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 2 小时

**描述**:
修改 `HikvisionLprService` 实现 `ILprDevice` 接口。

**步骤**:
1. 打开 `MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs`
2. 更新类声明:`public sealed class HikvisionLprService : IHikvisionLprService, ILprDevice, ISingletonDependency`
3. 实现 `SupportsActiveCapture` 属性,返回 `true`
4. 添加 XML 文档说明支持主动抓拍

**验收标准**:
- [x] 类声明已更新以实现 `ILprDevice`
- [x] `SupportsActiveCapture` 返回 `true`
- [ ] 代码编译无错误
- [x] XML 文档已添加

**输出**: 修改后的 `HikvisionLprService` 声明

---

### 任务 4.2: 实现 HikvisionLprService.TriggerCaptureAsync

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 4 小时

**描述**:
实现海康威视 LPR 服务的主动抓拍功能,使用 `NET_DVR_ContinuousShoot` SDK 接口。

**步骤**:
1. 在 `HikvisionLprService` 中添加登录会话管理字段:
   ```csharp
   private readonly ConcurrentDictionary<string, int> _deviceKeyToUserId = new();
   ```
2. 实现辅助方法:
   - `LoginDevice(config)`: 执行设备登录,返回 userId
   - `BuildDeviceKey(config)`: 生成设备唯一键(例如 $"{Ip}:{Port}")
3. 在 `HikvisionLprService` 中实现 `TriggerCaptureAsync()` 方法:
   - 使用会话缓存确保登录(参考 `HikvisionService.EnsureLogin()` 模式)
   - 首次调用时登录设备,后续调用复用现有会话
   - 仅在会话失效(userId < 0)时重新登录
   - 调用 `NET_DVR_ContinuousShoot(userId)` 触发抓拍
   - 返回现有的 `PlateRecognized` 可观察流(过滤此设备的结果)
   - 添加超时处理(例如 30 秒)
4. 错误处理:
   - 网络超时
   - 设备离线
   - SDK 调用失败
   - 调用 `NET_DVR_GetLastError()` 并记录错误
5. 资源管理:
   - 清理函数**不**调用 `NET_DVR_Logout`,保持会话供后续抓拍复用
   - 会话将在服务停止(`StopAsync`)时统一清理
   - 释放订阅,但保留登录会话
6. 添加详细的日志记录
7. 参考 `HikvisionService.EnsureLogin()` 的现有实现模式

**验收标准**:
- [x] 方法签名匹配 `ILprDevice` 接口
- [x] 登录会话缓存正确实现,避免重复登录
- [x] 设备登录和抓拍逻辑正确
- [x] 返回正确的可观察流
- [x] 错误处理和日志记录完整
- [x] 清理函数不登出设备,保持会话复用
- [ ] 在真实海康威视设备上测试通过,多次抓拍复用同一会话
- [ ] 代码编译无错误

**输出**: `HikvisionLprService.TriggerCaptureAsync()` 实现

---

### 任务 4.3: LprAllInOneService 适配 ILprDevice 接口

**状态**: ✅ 已完成
**优先级**: 高
**预计时间**: 2 小时

**描述**:
修改 `LprAllInOneService` 适配新的 `ILprDevice` 接口。

**步骤**:
1. 打开 `MaterialClient.Common/Services/LprAllInOne/LprAllInOneService.cs`
2. 更新类声明以实现 `ILprDevice`
3. 实现 `SupportsActiveCapture`,返回 `true`
4. 实现 `TriggerCaptureAsync()`:
   - 调用现有的 `TriggerManualRecognitionAsync()`
   - 包装返回值为可观察流
   - 或者,修改 `TriggerManualRecognitionAsync()` 返回 `IObservable<LicensePlateRecognizedEvent>`
5. 更新现有方法的文档

**验收标准**:
- [x] 类实现 `ILprDevice` 接口
- [x] `SupportsActiveCapture` 返回 `true`
- [x] `TriggerCaptureAsync()` 适配现有逻辑
- [x] 现有功能不受影响
- [ ] 代码编译无错误

**输出**: 修改后的 `LprAllInOneService`

---

### 任务 4.4: 创建 HuaxiazhixinLprService 占位实现

**状态**: ✅ 已完成
**优先级**: 低
**预计时间**: 1.5 小时

**描述**:
创建 `HuaxiazhixinLprService` 类,实现 `ILprDevice` 接口,标记不支持主动抓拍。

**步骤**:
1. 创建文件 `MaterialClient.Common/Services/Huaxiazhixin/HuaxiazhixinLprService.cs`
2. 实现 `ILprDevice` 接口
3. 实现 `SupportsActiveCapture`,返回 `false`
4. 实现 `TriggerCaptureAsync()`:
   - 记录警告日志,说明厂商不支持主动抓拍
   - 返回空的可观察流或抛出 `NotSupportedException`
5. 添加 XML 文档说明厂商限制

**验收标准**:
- [x] 文件已创建
- [x] 实现 `ILprDevice` 接口
- [x] `SupportsActiveCapture` 返回 `false`
- [x] `TriggerCaptureAsync()` 记录适当的警告
- [ ] 代码编译无错误

**输出**: `MaterialClient.Common/Services/Huaxiazhixin/HuaxiazhixinLprService.cs`

---

### 任务 4.5: 更新依赖注入配置

**状态**: ✅ 已完成
**优先级**: 中
**预计时间**: 1 小时

**描述**:
更新依赖注入配置,注册新的服务实现。

**步骤**:
1. 定位依赖注入配置文件(通常在 `MaterialClientModule.cs` 或类似文件中)
2. 添加 `HuaxiazhixinLprService` 注册(如果创建)
3. 验证所有 LPR 服务都正确注册为单例
4. 添加 XML 注释说明服务生命周期

**验收标准**:
- [x] 所有 LPR 服务都在 DI 容器中注册
- [x] 服务生命周期正确(单例)
- [ ] 代码编译无错误
- [ ] 应用启动无 DI 错误

**输出**: 更新后的依赖注入配置

---

## 阶段 5: 测试

### 任务 5.1: 创建消息发布单元测试

**状态**: 待处理
**优先级**: 高
**预计时间**: 2.5 小时

**描述**:
创建单元测试,验证回调处理程序发布正确的消息。

**步骤**:
1. 创建测试文件 `MaterialClient.Common.Tests/Tests/LicensePlateRecognizedMessageTests.cs`
2. 测试海康威视回调发布正确属性的消息
3. 测试 LprAllInOne 回调发布正确属性的消息
4. 测试华夏智信回调发布正确属性的消息(如果实现)
5. 使用测试隔离技术模拟 `MessageBus.Current`
6. 验证设备特定属性(DeviceType, DeviceName)

**验收标准**:
- [ ] 测试文件已创建
- [ ] 所有三种设备类型已测试
- [ ] 消息属性已验证
- [ ] 测试持续通过
- [ ] 测试覆盖边界情况(null 值,空字符串)

**输出**: 消息发布单元测试套件

---

### 任务 5.2: 创建消息订阅单元测试

**状态**: 待处理
**优先级**: 高
**预计时间**: 2.5 小时

**描述**:
创建单元测试,验证 `AttendedWeighingService` 正确订阅和处理 LPR 消息。

**步骤**:
1. 创建或扩展 `AttendedWeighingService` 的测试文件
2. 测试订阅在构造函数中创建
3. 测试接收消息调用 `OnPlateNumberRecognized`
4. 测试 `DisposeAsync()` 中的订阅释放
5. 测试多条消息处理(车牌缓存逻辑)
6. 使用模拟服务隔离订阅行为

**验收标准**:
- [ ] 订阅创建已验证
- [ ] 消息处理已验证
- [ ] 释放逻辑已测试
- [ ] 测试中无内存泄漏
- [ ] 测试持续通过

**输出**: 消息订阅单元测试套件

---

### 任务 5.3: 创建主动抓拍单元测试

**状态**: 待处理
**优先级**: 高
**预计时间**: 3 小时

**描述**:
创建单元测试,验证各 LPR 服务的主动抓拍功能。

**步骤**:
1. 创建测试文件 `MaterialClient.Common.Tests/Tests/LprActiveCaptureTests.cs`
2. 测试 `HikvisionLprService.TriggerCaptureAsync()`:
   - 模拟 SDK 调用成功
   - 模拟 SDK 调用失败
   - 验证返回可观察流
3. 测试 `LprAllInOneService.TriggerCaptureAsync()`:
   - 验证适配现有逻辑
4. 测试 `HuaxiazhixinLprService.TriggerCaptureAsync()`:
   - 验证返回 NotSupportedException 或空流
5. 测试 `SupportsActiveCapture` 属性

**验收标准**:
- [ ] 所有三种服务类型已测试
- [ ] 成功和失败场景已覆盖
- [ ] `SupportsActiveCapture` 已验证
- [ ] 测试持续通过
- [ ] 使用模拟避免硬件依赖

**输出**: 主动抓拍单元测试套件

---

### 任务 5.4: 创建集成测试

**状态**: 待处理
**优先级**: 高
**预计时间**: 3.5 小时

**描述**:
创建集成测试,模拟真实硬件回调通过 MessageBus 到业务逻辑的流程。

**步骤**:
1. 创建集成测试文件 `MaterialClient.Common.Tests/Integration/LprEventFlowTests.cs`
2. 测试完整流程: 硬件回调 → MessageBus → 服务处理 → UI 通知
3. 使用模拟硬件模拟器测试每种设备类型
4. 验证车牌缓存逻辑正确工作
5. 验证 `PlateNumberChangedMessage` 仍发送到 UI
6. 测试错误处理(无效车牌号,null 值)

**验收标准**:
- [ ] 集成测试文件已创建
- [ ] 所有设备类型端到端测试
- [ ] 车牌缓存已验证
- [ ] UI 通知已验证
- [ ] 错误情况已处理
- [ ] 测试持续通过

**输出**: LPR 事件流集成测试套件

---

### 任务 5.5: 运行内存泄漏测试

**状态**: 待处理
**优先级**: 高
**预计时间**: 2.5 小时

**描述**:
运行内存泄漏测试,确保 MessageBus 订阅不会导致内存泄漏。

**步骤**:
1. 扩展现有的 `AttendedWeighingServiceMemoryLeakTests`
2. 添加重复消息订阅和释放的测试
3. 监控 1000+ 消息周期后的内存增长
4. 验证订阅正确释放
5. 检查消息处理程序中的残留引用
6. 如果可用,使用 dotMemory 或类似分析器

**验收标准**:
- [ ] 内存泄漏测试已创建
- [ ] 1000+ 周期后无内存增长
- [ ] 订阅正确释放
- [ ] 测试持续通过
- [ ] 内存增长 <1MB

**输出**: 内存泄漏测试结果

---

## 阶段 6: 文档和清理

### 任务 6.1: 更新软件设计文档

**状态**: 待处理
**优先级**: 中
**预计时间**: 1.5 小时

**描述**:
更新 `docs/SDD.md`,文档化 MessageBus 用于 LPR 事件和事件系统架构。

**步骤**:
1. 更新 ADR-009 部分,包含 LPR 事件示例
2. 添加何时使用 MessageBus vs LocalEventBus 的指南
3. 文档化 `LicensePlateRecognizedMessage` 用法
4. 添加订阅的内存泄漏预防指南
5. 交叉引用相关 ADR 和模式
6. 添加 `ILprDevice` 接口的架构决策记录

**验收标准**:
- [ ] ADR-009 已更新,包含 LPR 示例
- [ ] MessageBus vs LocalEventBus 指南已添加
- [ ] 内存泄漏指南已文档化
- [ ] `ILprDevice` 接口已文档化
- [ ] 文档构建无错误

**输出**: 更新后的 `docs/SDD.md`

---

### 任务 6.2: 创建 LPR 集成文档

**状态**: 待处理
**优先级**: 低
**预计时间**: 2 小时

**描述**:
创建或更新文档,解释 LPR 设备如何通过 MessageBus 与系统集成。

**步骤**:
1. 创建文件 `openspec/docs/lpr-event-architecture.md`
2. 文档化事件流程: 硬件 → 回调 → MessageBus → 服务
3. 包含添加新 LPR 设备类型的代码示例
4. 解释订阅模式和释放要求
5. 添加常见问题故障排除指南
6. 文档化 `ILprDevice` 接口和主动抓拍功能

**验收标准**:
- [ ] 文档文件已创建
- [ ] 事件流程清晰解释
- [ ] 提供代码示例
- [ ] 包含故障排除指南
- [ ] 主动抓拍功能已文档化
- [ ] 文档审查清晰性

**输出**: `openspec/docs/lpr-event-architecture.md`

---

### 任务 6.3: 弃用未使用的 LicensePlateRecognizedEvent

**状态**: 待处理
**优先级**: 低
**预计时间**: 0.5 小时

**描述**:
标记未使用的 ABP 事件 `LicensePlateRecognizedEvent` 为过时或完全删除。

**步骤**:
1. 打开 `MaterialClient.Common/Events/LicensePlateRecognizedEvent.cs`
2. 添加带有弃用消息的 `[Obsolete]` 属性
3. 搜索代码库中此事件的任何使用
4. 如果未使用,考虑完全删除文件
5. 更新任何相关文档

**验收标准**:
- [ ] 事件标记为过时或删除
- [ ] 代码库中无活动使用
- [ ] 文档已更新
- [ ] 代码编译无错误

**输出**: 弃用或删除的 `LicensePlateRecognizedEvent`

---

### 任务 6.4: 更新 OpenSpec 规范

**状态**: 待处理
**优先级**: 中
**预计时间**: 1.5 小时

**描述**:
更新 `license-plate-recognition` 规范,反映基于 MessageBus 的架构和主动抓拍接口。

**步骤**:
1. 打开 `openspec/changes/unified-lpr-data-transfer-and-capture-interface/specs/license-plate-recognition/spec.md`
2. 添加 LPR 事件处理的 MODIFIED 需求
3. 更新场景引用 MessageBus 而非直接调用
4. 添加 MessageBus 订阅和释放的场景
5. 添加 `ILprDevice` 接口和主动抓拍的场景
6. 运行 `openspec validate unified-lpr-data-transfer-and-capture-interface --strict --no-interactive`

**验收标准**:
- [ ] 规范变更已创建,包含 MODIFIED 需求
- [ ] 场景已更新为 MessageBus 模式
- [ ] 主动抓拍场景已添加
- [ ] 验证通过,无错误
- [ ] 规范清晰完整

**输出**: 更新后的规范在 `openspec/changes/unified-lpr-data-transfer-and-capture-interface/specs/license-plate-recognition/spec.md`

---

## 进度跟踪

**阶段 1 进度**: 3/3 任务完成 (100%)
**阶段 2 进度**: 3/3 任务完成 (100%)
**阶段 3 进度**: 2/2 任务完成 (100%)
**阶段 4 进度**: 5/5 任务完成 (100%)
**阶段 5 进度**: 0/5 任务完成 (0%)
**阶段 6 进度**: 0/4 任务完成 (0%)
**总体进度**: 13/28 任务 (46%)

---

## 依赖关系和并行化

**可以并行完成**:
- 阶段 1 中的所有任务(相互独立)
- 阶段 2 中的所有任务(每个设备处理程序相互独立)
- 任务 5.1、5.2 和 5.3(单元测试可以并行编写)
- 阶段 6 中的所有任务(文档任务相互独立)

**必须顺序执行**:
- 阶段 1 必须在阶段 3 之前完成
- 阶段 2 和阶段 3 应在阶段 4 之前完成(需要基础架构在添加主动抓拍之前就绪)
- 阶段 2 和阶段 3 必须在阶段 5 之前完成(集成测试需要发布者和订阅者)
- 阶段 5 必须在完成阶段 6 中的文档之前完成

**关键路径**:
1. 阶段 1(基础设施) → 阶段 2(回调重构) → 阶段 3(服务集成) → 阶段 5(测试) → 阶段 6(文档)
2. 阶段 4(主动抓拍)可以在阶段 3 之后与阶段 5 并行进行
