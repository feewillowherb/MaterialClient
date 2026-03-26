## 1. 服务重命名

- [x] 1.1 重命名服务类和接口
  - 将 `LprGateIoControlService.cs` 重命名为 `GateIoControlService.cs`
  - 将 `ILprGateIoControlService.cs` 重命名为 `IGateIoControlService.cs`
  - 更新类名和接口名：`LprGateIoControlService` → `GateIoControlService`
  - 更新接口名：`ILprGateIoControlService` → `IGateIoControlService`

- [x] 1.2 更新依赖注入注册
  - 修改 `MaterialClientCommonModule.cs` 中的服务注册
  - 更新所有服务引用（如 `DeviceManagerService`）
  - 验证编译通过

## 2. 枚举迁移与配置更新

- [x] 2.1 修改 `LicensePlateDirection` 枚举值从 `In/Out` 改为 `A/B`
  - 更新 `MaterialClient.Common\Entities\Enums\LicensePlateDirection.cs`
  - 保持整数值兼容（`A=0`, `B=1`）
  - 添加 `Description` 特性：
    - `A` 枚举：`[Description("入口")]`
    - `B` 枚举：`[Description("出口")]`
  - 更新 XML 注释说明（区分物理侧别与会话角色）
  - 注意：A/B 为物理侧别位置（入口/出口），Entry/Exit 为运行时会话角色

- [x] 2.2 新增 `GateIoControlMode` 枚举
  - 创建 `MaterialClient.Common\Entities\Enums\GateIoControlMode.cs`
  - 定义：`LrpSdk = 1`, `DirectCom = 2`
  - 添加 XML 注释说明

- [ ] 2.3 验证配置序列化/反序列化兼容性
  - 添加单元测试验证 `In/Out` 旧配置能正确反序列化为 `A/B`
  - 测试配置保存和加载功能

## 3. 双控制模式架构

- [x] 3.1 实现统一控制接口 `OpenGateAsync()`
  - 在 `GateIoControlService` 中添加 `OpenGateAsync()` 私有方法
  - 根据 `GateIoControlMode` 分发到不同的控制方法
  - 处理不支持的控制方式（抛出 `NotSupportedException`）

- [x] 3.2 实现 LRP SDK 控制方法 `OpenGateViaLrpSdk()`
  - 将现有 SDK 调用逻辑迁移到此方法
  - 检查 `LprDeviceType` 是否为 `Vzvision`
  - 调用 `_vzvisionLprService.SetIoOutputAutoRespAsync()`

- [x] 3.3 实现 COM 直接控制方法 `OpenGateViaCom()`
  - 添加预留方法签名
  - 实现抛出 `NotSupportedException` 异常
  - 异常消息："直接通过 COM 控制道闸 I/O 功能暂不支持，请使用 LRP SDK 控制方式"

- [x] 3.4 更新现有调用点
  - 修改 `HandlePlateRecognizedAsync()` 调用 `OpenGateAsync()` 而非直接调用 SDK
  - 修改 `OnStatusChanged()` 出口开闸逻辑调用 `OpenGateAsync()`

## 4. 道闸会话状态管理

- [x] 4.1 在 `GateIoControlService` 中添加 `GateIoSession` 私有类
  - 添加字段：`SessionActive`、`EntrySide`、`ExitOpened`、`SessionStartedAt`
  - 初始化默认值（`SessionActive=false`, `EntrySide=null`, `ExitOpened=false`）

- [x] 4.2 实现会话创建逻辑
  - 修改 `HandlePlateRecognizedAsync()` 方法
  - 检查会话状态：如果 `SessionActive=false` 且称重状态为 `OffScale`，创建新会话
  - 设置 `EntrySide = config.Direction`，记录 `SessionStartedAt`
  - 调用 `OpenGateAsync()` 打开入口道闸

- [x] 4.3 实现会话互斥逻辑
  - 在 `HandlePlateRecognizedAsync()` 中检查 `SessionActive`
  - 如果会话已激活，拒绝任何 LRP 触发（记录日志）
  - 防止重复开闸和入口侧改变

- [x] 4.4 实现会话清理逻辑
  - 添加 `ClearSession()` 私有方法
  - 重置会话状态：`SessionActive=false`, `EntrySide=null`, `ExitOpened=false`
  - 记录清理日志和会话持续时间

## 5. 状态订阅与同步

- [x] 5.1 在 `GateIoControlService` 中添加 `StatusChangedMessage` 订阅
  - 在 `StartAsync()` 中订阅 `MessageBus.Current.Listen<StatusChangedMessage>()`
  - 添加 `_statusSubscription` 字段存储订阅

- [x] 5.2 实现 `OnStatusChanged()` 方法
  - 处理 `OffScale` 状态转换：调用 `ClearSession()`
  - 处理 `WaitingForStability/WeightStabilized`：设置内部"禁止 LRP 开闸"标志
  - 处理 `WaitingForDeparture`：触发出口开闸逻辑
  - 记录状态转换日志

- [x] 5.3 在 `StopAsync()` 中释放状态订阅
  - 释放 `_statusSubscription`
  - 设置 `_statusSubscription = null`

- [x] 5.4 添加内部状态标志（可选实现方式）
  - 添加 `CurrentWeighingStatus` 字段缓存当前称重状态
  - 或使用 `_gateIoEnabled` 标志控制道闸功能总开关

## 6. 出口道闸自动开闸逻辑

- [x] 6.1 实现出口侧计算逻辑
  - 在 `OnStatusChanged()` 中处理 `WaitingForDeparture` 状态
  - 计算出口侧：`exitSide = EntrySide == A ? B : A`
  - 检查 `ExitOpened` 标志，避免重复开闸

- [x] 6.2 实现出口侧配置查找
  - 从 `_configByName` 中查找 `Direction == exitSide` 且 `EnableGateIo == true` 的配置
  - 处理配置不存在场景（记录警告日志）

- [x] 6.3 调用 `OpenGateAsync()` 打开出口道闸
  - 解析 `IoChannel`（错误处理）
  - 调用 `OpenGateAsync()` 打开出口道闸
  - 设置 `ExitOpened = true`
  - 记录操作日志

- [x] 6.4 处理出口开闸失败场景
  - 捕获 SDK 调用异常，记录错误日志
  - 保持 `ExitOpened = false`（允许人工干预）
  - 不中断状态转换流程

## 7. 状态门控逻辑

- [x] 7.1 在 `HandlePlateRecognizedAsync()` 中添加称重状态检查
  - 获取当前称重状态（通过 `IAttendedWeighingService.GetCurrentStatus()` 或缓存状态）
  - 如果状态为 `WaitingForStability` 或 `WeightStabilized`，拒绝开闸
  - 如果状态为 `WaitingForDeparture`，拒绝 LRP 开闸（出口由状态转换触发）
  - 记录拒绝日志

- [x] 7.2 实现基于状态的开闸许可判断
  - 提取状态判断逻辑为独立方法 `ShouldAllowGateOpen()`
  - 封装规则：仅 `OffScale` 且会话未激活时允许入口开闸
  - 在 `HandlePlateRecognizedAsync()` 开头调用此方法

## 8. 配置校验

- [x] 8.1 实现 `ValidateGateConfiguration()` 方法
  - 从 `LicensePlateRecognitionConfigs` 中筛选 `EnableGateIo == true` 的配置
  - 统计 `Direction.A` 和 `Direction.B` 的数量
  - 验证规则：恰好一对 A/B（`CountA == 1 && CountB == 1`）
  - 返回校验结果结构（`IsValid`, `Reason`, `CountA`, `CountB`, `DevicesA`, `DevicesB`）

- [x] 8.2 在 `StartAsync()` 中调用配置校验
  - 在订阅 MessageBus 之前调用 `ValidateGateConfiguration()`
  - 如果校验失败，记录警告日志并设置 `_gateIoEnabled = false`
  - 继续启动流程（不抛异常）

- [x] 8.3 在配置保存后重新校验
  - 在 `SettingsSavedMessage` 订阅中调用 `ValidateGateConfiguration()`
  - 更新 `_gateIoEnabled` 标志
  - 记录配置更新日志

- [x] 8.4 在道闸控制逻辑中检查 `_gateIoEnabled` 标志
  - 在 `HandlePlateRecognizedAsync()` 开头检查此标志
  - 在 `OnStatusChanged()` 开头检查此标志
  - 如果为 `false`，跳过所有道闸控制逻辑

## 9. 错误处理与降级

- [x] 9.1 添加控制调用异常捕获
  - 在所有 `OpenGateAsync()` 调用外包裹 try-catch
  - 记录错误日志（设备名、IoChannel、异常信息）
  - 不抛出异常，允许称重流程继续

- [x] 9.2 实现会话异常恢复机制
  - 在 `ClearSession()` 中添加异常处理
  - 如果清理失败，强制重置会话状态（防止会话泄漏）
  - 记录错误日志

- [x] 9.3 添加配置缺失场景的日志
  - 出口侧配置不存在：记录警告日志
  - `IoChannel` 解析失败：记录警告日志
  - 设置 `ExitOpened = true`（避免重复日志）

- [x] 9.4 处理 COM 控制方式不支持异常
  - 在 `OpenGateAsync()` 中捕获 `NotSupportedException`
  - 记录警告日志："直接通过 COM 控制道闸功能暂不支持"
  - 回退到 LRP SDK 控制方式

## 10. UI 更新

- [x] 10.1 更新 `AddLprDialogViewModel` 中的 Direction 选项
  - 将 `In/Out` 选项改为 `A/B`
  - 使用 `Description` 特性获取中文描述显示"入口"/"出口"
  - 更新 UI 文本提示
  - 确保用户理解 A/B 为物理侧别位置（与会话运行时角色区分）

- [x] 10.2 更新 `SettingsWindowViewModel` 中的 Direction 显示
  - 将配置列表中的 Direction 显示从 `In/Out` 改为 A/B
  - 使用 `Description` 特性显示中文名称"入口"/"出口"
  - 确保编辑和保存功能正常工作

## 11. 单元测试

- [ ] 11.1 添加服务重命名相关测试
  - 验证 `IGateIoControlService` 接口正确注入
  - 测试服务生命周期管理

- [ ] 11.2 添加 `GateIoSession` 状态管理测试
  - 测试会话创建、激活、清理流程
  - 测试会话互斥逻辑（重复触发被拒绝）

- [ ] 11.3 添加状态同步逻辑测试
  - 使用 Mock 对象模拟 `StatusChangedMessage`
  - 测试 `OffScale` 清理会话
  - 测试 `WaitingForDeparture` 打开出口道闸

- [ ] 11.4 添加配置校验测试
  - 测试有效的一对 A/B 配置
  - 测试缺少 A 侧、缺少 B 侧、多个 A 侧、多个 B 侧场景
  - 测试无道闸配置场景

- [ ] 11.5 添加状态门控逻辑测试
  - 测试各称重状态下的 LRP 开闸许可判断
  - 测试会话期间拒绝重复触发

- [ ] 11.6 添加双控制模式测试
  - 测试 LRP SDK 控制方式正常工作
  - 测试 COM 控制方式抛出 `NotSupportedException`
  - 测试不支持控制方式的错误处理

- [ ] 11.7 添加错误处理测试
  - 测试控制调用失败场景
  - 测试配置缺失场景
  - 测试异常恢复机制

## 12. 集成测试与验证

- [ ] 12.1 手动测试完整车辆通行流程
  - A 侧入口 → 上磅 → 称重 → 等待下磅 → B 侧出口 → 离磅
  - B 侧入口 → 上磅 → 称重 → 等待下磅 → A 侧出口 → 离磅
  - 验证会话状态正确创建和清理

- [ ] 12.2 测试状态门控逻辑
  - 在 `WaitingForStability` 和 `WeightStabilized` 阶段尝试 LRP 识别
  - 验证道闸不会打开

- [ ] 12.3 测试会话互斥逻辑
  - 快速连续触发 A 侧和 B 侧 LRP 识别
  - 验证仅首次触发创建会话并开闸

- [ ] 12.4 测试配置校验功能
  - 配置零对、多对、不成对的 A/B 道闸
  - 验证启动时正确校验并记录警告日志
  - 验证道闸功能进入降级模式

- [ ] 12.5 测试无道闸模式兼容性
  - 设置 `EnableGateIo = false`
  - 验证称重流程正常工作，道闸功能不干扰

- [ ] 12.6 测试人工干预场景
  - 在会话期间使用遥控器手动开关道闸
  - 验证软件状态可能不一致，但称重流程不受影响

- [ ] 12.7 测试服务重命名后的功能完整性
  - 验证所有依赖注入正确解析
  - 验证服务启动和停止流程正常
  - 验证道闸控制功能无退化

- [ ] 12.8 测试双控制模式架构
  - 验证 LRP SDK 控制方式正常工作（默认方式）
  - 验证 COM 控制方式正确抛出"不支持"异常
  - 验证异常消息清晰明确

## 13. 文档更新

- [ ] 13.1 更新配置说明文档
  - 说明道闸 A/B 配置要求（恰好一对）
  - 说明 `Direction` 枚举从 `In/Out` 改为 `A/B`
  - **说明 A/B 枚举的中文描述**：
    - `A` = "入口"（物理侧别位置）
    - `B` = "出口"（物理侧别位置）
    - 注意：与会话运行时角色（Entry/Exit）区分
  - 说明服务重命名：`LprGateIoControlService` → `GateIoControlService`
  - 说明双控制模式架构（LRP SDK 与 COM 直接控制）
  - 提供配置示例

- [ ] 13.2 更新升级说明
  - 说明服务重命名的影响
  - 说明枚举迁移的影响（`In/Out` → `A/B`，中文"入口"/"出口"）
  - 说明配置兼容性处理（整数值兼容）
  - 提示用户检查道闸配置

- [ ] 13.3 添加故障排查指南
  - 会话状态不一致的排查方法
  - 配置校验失败的解决步骤
  - COM 控制方式不支持的说明
  - 日志关键字说明
