# license-plate-recognition 规范增量

**能力**：`license-plate-recognition`
**变更 ID**：`unified-lpr-data-transfer-and-capture-interface`
**类型**：MODIFIED（修改）
**日期**：2026-01-29

本文说明由变更 `unified-lpr-data-transfer-and-capture-interface` 修改的需求，仅包含发生变更的需求。完整需求请参见基础规范。

---

## 修改的需求

### Requirement: 通过 MessageBus 进行 LPR 服务集成

系统应使用 ReactiveUI MessageBus 从硬件回调处理程序向业务服务交付车牌识别事件,解耦硬件集成层和业务逻辑层。

#### Scenario: 海康威视设备识别车牌并发布 MessageBus 消息
- **给定** 系统配置了海康威视 LPR 设备
- **且** 海康威视 SDK 回调 `MSGCallBack` 接收到 `COMM_UPLOAD_PLATE_RESULT` (0x2800) 消息
- **且** `MinimalWebHostService` 中的回调处理程序被调用
- **当** 回调解析车牌识别结果时
- **则** 系统应:
  - 创建包含以下内容的 `LicensePlateRecognizedMessage`:
    - `PlateNumber` 设置为识别的车牌文本(使用 GBK 编码)
    - `ColorType` 设置为解析的颜色类型(如果可用)
    - `DeviceType` 设置为 `LprDeviceType.Hikvision`
    - `DeviceName` 设置为回调配置中的设备名称
    - `Timestamp` 设置为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录包含设备名称和车牌号的识别事件
  - **不**直接调用 `IAttendedWeighingService.OnPlateNumberRecognized()`

#### Scenario: LprAllInOne 设备识别车牌并发布 MessageBus 消息
- **给定** 系统配置了 LprAllInOne 设备
- **且** HTTP 回调端点接收到带有 `type=online` 的 POST 请求
- **且** 表单数据包含 `plate_num` 参数
- **当** `MinimalWebHostService` 中的回调处理程序处理请求时
- **则** 系统应:
  - 创建包含以下内容的 `LicensePlateRecognizedMessage`:
    - `PlateNumber` 设置为 `plate_num` 值
    - `ColorType` 设置为 null 或从 `plate_color` 解析(如果可用)
    - `DeviceType` 设置为 `LprDeviceType.LprAllInOne`
    - `DeviceName` 设置为配置的设备名称
    - `Timestamp` 设置为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录识别事件
  - 向硬件设备返回 HTTP 200 响应

#### Scenario: 华夏智信设备识别车牌并发布 MessageBus 消息
- **给定** 系统配置了华夏智信设备
- **且** HTTP 回调端点接收到带有识别车牌数据的 POST 请求
- **当** `MinimalWebHostService` 中的回调处理程序处理请求时
- **则** 系统应:
  - 创建包含以下内容的 `LicensePlateRecognizedMessage`:
    - `PlateNumber` 设置为识别的车牌号
    - `ColorType` 设置为 null 或解析(如果可用)
    - `DeviceType` 设置为 `LprDeviceType.Huaxiazhixin`
    - `DeviceName` 设置为配置的设备名称
    - `Timestamp` 设置为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录识别事件
  - 向硬件设备返回 HTTP 200 响应

---

### Requirement: AttendedWeighingService 订阅 LPR 消息

系统应配置 `AttendedWeighingService` 订阅通过 ReactiveUI MessageBus 的 `LicensePlateRecognizedMessage`,通过现有的车牌缓存和推荐逻辑处理识别事件。

#### Scenario: AttendedWeighingService 在初始化时订阅 LPR 消息
- **给定** `AttendedWeighingService` 正作为单例依赖被实例化
- **当** 执行服务构造函数时
- **则** 系统应:
  - 使用 `MessageBus.Current.Listen<LicensePlateRecognizedMessage>()` 创建 MessageBus 订阅
  - 在私有字段( `_licensePlateSubscription`)中存储订阅 `IDisposable`
  - 配置订阅处理程序以:
    - 记录接收到的消息(车牌号、设备名称、时间戳)
    - 使用消息数据调用私有的 `OnPlateNumberRecognized()` 方法
  - 确保订阅在服务生命周期内持久化

#### Scenario: AttendedWeighingService 处理来自 MessageBus 的 LPR 消息
- **给定** `AttendedWeighingService` 具有活动的 MessageBus 订阅
- **且** `LicensePlateRecognizedMessage` 被发布到总线
- **当** 订阅处理程序接收消息时
- **则** 系统应:
  - 记录包含设备信息的识别事件
  - 调用 `OnPlateNumberRecognized(message.PlateNumber, message.ColorType)`
  - 执行现有的车牌缓存逻辑(频率计数、颜色过滤)
  - 通过 MessageBus 发布 `PlateNumberChangedMessage` 用于 UI 更新
  - 根据现有规则处理低优先级车牌颜色

#### Scenario: AttendedWeighingService 在清理时释放 LPR 消息订阅
- **给定** `AttendedWeighingService` 正在被释放
- **且** MessageBus 订阅处于活动状态
- **当** 调用 `DisposeAsync()` 方法时
- **则** 系统应:
  - 在 `_licensePlateSubscription` 字段上调用 `Dispose()`
  - 将字段设置为 null 以释放引用
  - 继续现有的释放逻辑
  - 防止来自未传递消息的内存泄漏

---

### Requirement: 统一 LPR 设备主动抓拍接口

系统应提供统一的 `ILprDevice` 接口,定义所有 LPR 设备类型的主动抓拍能力,包括海康威视、LprAllInOne 和华夏智信设备。

#### Scenario: ILprDevice 接口定义主动抓拍能力
- **给定** 系统需要统一的主动抓拍接口
- **当** 定义 `ILprDevice` 接口时
- **则** 接口应包含:
  - `IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(LicensePlateRecognitionConfig config)` 方法
  - `bool SupportsActiveCapture { get; }` 属性
  - 详细的 XML 文档注释,说明使用场景和限制

#### Scenario: HikvisionLprService 实现主动抓拍
- **给定** 系统配置了海康威视 LPR 设备
- **且** `HikvisionLprService` 实现 `ILprDevice` 接口
- **且** `SupportsActiveCapture` 返回 `true`
- **当** 应用调用 `TriggerCaptureAsync(config)` 时
- **则** 系统应:
  - 使用提供的配置调用 `NET_DVR_Login_V40()` 登录设备
  - 调用 `NET_DVR_ContinuousShoot()` 触发抓拍
  - 返回可观察的车牌识别事件流
  - 在 30 秒后超时(如果没有结果)
  - 处理错误情况:
    - 网络超时
    - 设备离线
    - SDK 调用失败
  - 记录所有操作和错误
  - 清理资源(登出设备,释放订阅)

#### Scenario: LprAllInOneService 适配主动抓拍接口
- **给定** 系统配置了 LprAllInOne 设备
- **且** `LprAllInOneService` 实现 `ILprDevice` 接口
- **且** `SupportsActiveCapture` 返回 `true`
- **当** 应用调用 `TriggerCaptureAsync(config)` 时
- **则** 系统应:
  - 调用现有的 `TriggerManualRecognitionAsync()` 机制
  - 返回车牌识别结果的可观察流
  - 保持现有的标志位和轮询机制
  - 在结果可用时完成可观察流

#### Scenario: HuaxiazhixinLprService 标记不支持主动抓拍
- **给定** 系统配置了华夏智信设备
- **且** `HuaxiazhixinLprService` 实现 `ILprDevice` 接口
- **且** `SupportsActiveCapture` 返回 `false`
- **当** 应用调用 `TriggerCaptureAsync(config)` 时
- **则** 系统应:
  - 记录警告日志,说明厂商不支持主动抓拍
  - 抛出 `NotSupportedException` 并附带清晰的错误消息
  - **不**尝试触发任何硬件操作
  - 在 XML 文档中明确说明此限制

---

### Requirement: LicensePlateRecognizedMessage 定义

系统应提供统一的消息类,用于通过 ReactiveUI MessageBus 传输车牌识别数据,支持所有 LPR 设备类型且结构一致。

#### Scenario: LicensePlateRecognizedMessage 携带完整识别数据
- **给定** 来自任何 LPR 设备类型的车牌识别事件发生
- **当** 创建 `LicensePlateRecognizedMessage` 时
- **则** 消息应包含:
  - `PlateNumber` (string): 识别的车牌文本(例如 "京A12345")
  - `ColorType` (LprAllInOneColorType?): 可选的车牌颜色(蓝色、黄色、绿色等)
  - `DeviceType` (LprDeviceType): 指示设备类型的枚举值(海康威视、LprAllInOne、华夏智信)
  - `DeviceName` (string): 来自配置的人类可读设备名称
  - `Timestamp` (DateTime): 识别发生的 UTC 时间戳

#### Scenario: LicensePlateRecognizedMessage 通过 MessageBus 发布
- **给定** 创建了包含完整数据的 `LicensePlateRecognizedMessage`
- **当** 使用 `MessageBus.Current.SendMessage(message)` 发布消息时
- **则** 系统应:
  - 将消息传递给 `LicensePlateRecognizedMessage` 的所有活动订阅者
  - 同步传递,无队列延迟(<1ms 延迟)
  - 保留所有消息属性,无数据丢失
  - 允许多个订阅者接收同一条消息实例

---

### Requirement: 服务接口简化

系统应从 `IAttendedWeighingService` 公共接口中移除 `OnPlateNumberRecognized` 方法,使其成为通过 MessageBus 订阅调用的私有实现细节。

#### Scenario: IAttendedWeighingService 接口排除 OnPlateNumberRecognized 方法
- **给定** 定义了 `IAttendedWeighingService` 接口
- **当** 重构完成时
- **则** 接口应**不**包含:
  - `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)`
- **且** 接口应保持所有其他现有成员

#### Scenario: AttendedWeighingService 将 OnPlateNumberRecognized 实现为私有方法
- **给定** `AttendedWeighingService` 类实现 `IAttendedWeighingService`
- **且** MessageBus 订阅处于活动状态
- **当** 订阅处理程序接收 `LicensePlateRecognizedMessage` 时
- **则** 类应调用私有的 `OnPlateNumberRecognized()` 方法
- **且** 方法应**不**可从外部代码访问
- **且** 方法应保留其现有实现逻辑(缓存、过滤等)

---

### Requirement: MessageBus 订阅的内存泄漏预防

系统应确保所有 MessageBus 订阅正确释放,以防止长期运行场景中的内存泄漏。

#### Scenario: 订阅正确存储和释放
- **给定** 服务创建 MessageBus 订阅
- **当** 创建订阅时
- **则** 系统应:
  - 在私有字段中存储 `Subscribe()` 返回的 `IDisposable`
  - 添加 XML 文档注释,指示释放责任
  - 在释放序列中包含该字段

#### Scenario: 服务清理时释放订阅
- **给定** 具有活动 MessageBus 订阅的服务正在被释放
- **当** 调用 `DisposeAsync()` 或 `Dispose()` 方法时
- **则** 系统应:
  - 在所有存储的订阅 `IDisposable` 字段上调用 `Dispose()`
  - 优雅地处理 null 订阅
  - 如果启用了日志记录,记录释放完成
  - 释放订阅的所有引用

#### Scenario: 长期运行系统不会从重复的 LPR 事件中泄漏内存
- **给定** 系统运行了长时间(24+ 小时)
- **且** LPR 设备识别了 1000+ 个车牌
- **当** 监控内存使用时
- **则** 系统应:
  - 显示来自 MessageBus 订阅的连续内存增长
  - 在服务重启时正确释放订阅
  - 通过内存泄漏测试,1000 个事件后 <1MB 增长
  - 不在 MessageBus 队列中累积未传递的消息

---

## ADDED Requirements

### Requirement: 主动抓拍错误处理和超时

系统应正确处理主动抓拍操作中的错误情况,包括网络超时、设备离线和 SDK 调用失败,并在合理的时间内超时。

#### Scenario: 主动抓拍在设备离线时超时
- **给定** 设备配置为离线或不可达
- **且** 应用调用 `TriggerCaptureAsync(config)`
- **当** 登录尝试超时时
- **则** 系统应:
  - 在合理的时间范围内超时(例如 10 秒用于登录,30 秒用于完整抓拍)
  - 记录错误日志,指示设备不可达
  - 在返回的可观察流上调用 `OnError()`
  - **不**无限期挂起

#### Scenario: 主动抓拍处理 SDK 调用失败
- **给定** 设备在线但 SDK 调用失败
- **且** 应用调用 `TriggerCaptureAsync(config)`
- **当** `NET_DVR_ContinuousShoot()` 或其他 SDK 调用返回 false
- **则** 系统应:
  - 调用 `NET_DVR_GetLastError()` 获取错误代码
  - 将错误代码映射到可读的错误消息
  - 记录带有设备名称和错误的错误日志
  - 清理资源(登出设备,释放订阅)
  - 在返回的可观察流上调用 `OnError()`

#### Scenario: 主动抓带在无识别结果时超时
- **给定** 设备在线并触发抓拍
- **且** 应用调用 `TriggerCaptureAsync(config)`
- **当** 设备在 30 秒内未返回识别结果
- **则** 系统应:
  - 使用 `.Timeout()` 操作符在可观察流上超时
  - 记录警告日志,指示未收到结果
  - 在可观察流上调用 `OnError()` 或 `OnCompleted()`
  - 清理资源

---

### Requirement: 主动抓拍资源清理

系统应在主动抓拍操作后正确清理所有资源,包括设备登出、订阅释放和内存清理。

#### Scenario: 主动抓拍操作后清理资源
- **给定** 主动抓拍操作已完成(成功或失败)
- **当** 可观察流完成或出错时
- **则** 系统应:
  - 调用 `NET_DVR_Logout()` 登出设备
  - 释放所有临时订阅
  - 清理任何非托管内存
  - 记录清理完成(如果启用了日志记录)
  - **不**留下活动连接或订阅

#### Scenario: 主动抓拍操作取消时清理资源
- **给定** 主动抓拍操作正在进行
- **且** 订阅者取消订阅(调用返回的 `IDisposable.Dispose()`)
- **当** 取消发生时
- **则** 系统应:
  - 登出设备(如果已登录)
  - 释放所有内部订阅
  - 清理临时资源
  - 记录取消(如果启用了日志记录)
  - **不**抛出异常

---

## 交叉引用

**修改的需求**:
- 通过 MessageBus 进行 LPR 服务集成(MODIFIED 从直接服务调用)
- AttendedWeighingService 订阅 LPR 消息(新订阅模式)
- LicensePlateRecognizedMessage 定义(新消息类)
- 服务接口简化(MODIFIED 接口)
- MessageBus 订阅的内存泄漏预防(新需求)
- 统一 LPR 设备主动抓拍接口(新接口和实现)

**添加的需求**:
- 主动抓拍错误处理和超时(新)
- 主动抓拍资源清理(新)

**相关能力**:
- `attended-weighing` - 依赖用于自动车牌捕获的 LPR 事件

**基础规范**:
- `openspec/specs/license-plate-recognition/spec.md` - 完整规范,包括未修改的需求

**相关变更**:
- `hikvision-lpr-implementation` - 实现海康威视 LPR 服务和事件流
- `hikvision-lpr-integration` - 海康威视 LPR 配置和 UI
- `unify-lpr-events-with-messagebus` - 统一 LPR 事件使用 MessageBus(相关变更)
