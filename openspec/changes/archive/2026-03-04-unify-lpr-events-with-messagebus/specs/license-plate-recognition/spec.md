# license-plate-recognition 规格增量

**能力**：`license-plate-recognition`
**变更 ID**：`unify-lpr-events-with-messagebus`
**类型**：MODIFIED
**日期**：2026-01-29

本文档规定由变更 `unify-lpr-events-with-messagebus` 修改的需求，仅包含发生变更的需求。完整需求请参见基础规格。

---

## 修改的需求

### 需求：LPR 服务与 MessageBus 集成

系统应使用 ReactiveUI MessageBus 将车牌识别事件从硬件回调处理投递到业务服务，解耦硬件集成层与业务逻辑层。

#### 场景：海康设备识别车牌并发布 MessageBus 消息
- **给定** 系统已配置海康 LPR 设备
- **且** 海康 SDK 回调 `MSGCallBack` 收到 `COMM_UPLOAD_PLATE_RESULT` (0x2800) 消息
- **且** 调用 `MinimalWebHostService` 中的回调处理
- **当** 回调解析车牌结果
- **则** 系统应：
  - 创建 `LicensePlateRecognizedMessage`，其中：
    - `PlateNumber` 设为识别出的车牌文本（使用 GBK 编码）
    - `ColorType` 在可用时设为解析出的颜色类型
    - `DeviceType` 设为 `LprDeviceType.Hikvision`
    - `DeviceName` 设为回调配置中的设备名称
    - `Timestamp` 设为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录识别事件（设备名与车牌号）
  - 不得直接调用 `IAttendedWeighingService.OnPlateNumberRecognized()`

#### 场景：LprAllInOne 设备识别车牌并发布 MessageBus 消息
- **给定** 系统已配置 LprAllInOne 设备
- **且** HTTP 回调端点收到带 `type=online` 的 POST 请求
- **且** 表单数据包含 `plate_num` 参数
- **当** `MinimalWebHostService` 中的回调处理该请求
- **则** 系统应：
  - 创建 `LicensePlateRecognizedMessage`，其中：
    - `PlateNumber` 设为 `plate_num` 值
    - `ColorType` 设为 null 或在有 `plate_color` 时解析
    - `DeviceType` 设为 `LprDeviceType.LprAllInOne`
    - `DeviceName` 设为配置的设备名称
    - `Timestamp` 设为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录识别事件
  - 向硬件设备返回 HTTP 200

#### 场景：华夏智信设备识别车牌并发布 MessageBus 消息
- **给定** 系统已配置华夏智信设备
- **且** HTTP 回调端点收到带识别车牌数据的 POST 请求
- **当** `MinimalWebHostService` 中的回调处理该请求
- **则** 系统应：
  - 创建 `LicensePlateRecognizedMessage`，其中：
    - `PlateNumber` 设为识别出的车牌号
    - `ColorType` 设为 null 或在可用时解析
    - `DeviceType` 设为 `LprDeviceType.Huaxiazhixin`
    - `DeviceName` 设为配置的设备名称
    - `Timestamp` 设为当前 UTC 时间
  - 通过 `MessageBus.Current.SendMessage(message)` 发布消息
  - 记录识别事件
  - 向硬件设备返回 HTTP 200

---

### 需求：AttendedWeighingService 订阅 LPR 消息

系统应配置 `AttendedWeighingService` 通过 ReactiveUI MessageBus 订阅 `LicensePlateRecognizedMessage`，经现有车牌缓存与推荐逻辑处理识别事件。

#### 场景：AttendedWeighingService 在初始化时订阅 LPR 消息
- **给定** `AttendedWeighingService` 正作为单例依赖被实例化
- **当** 执行服务构造函数
- **则** 系统应：
  - 使用 `MessageBus.Current.Listen<LicensePlateRecognizedMessage>()` 创建 MessageBus 订阅
  - 将订阅的 `IDisposable` 保存在私有字段 `_licensePlateSubscription`
  - 将订阅处理配置为：
    - 记录收到的消息（车牌号、设备名、时间戳）
    - 使用消息数据调用私有方法 `OnPlateNumberRecognized()`
  - 保证订阅在服务生命周期内持续有效

#### 场景：AttendedWeighingService 处理来自 MessageBus 的 LPR 消息
- **给定** `AttendedWeighingService` 存在活跃的 MessageBus 订阅
- **且** 向总线发布了 `LicensePlateRecognizedMessage`
- **当** 订阅处理收到该消息
- **则** 系统应：
  - 记录带设备信息的识别事件
  - 调用 `OnPlateNumberRecognized(message.PlateNumber, message.ColorType)`
  - 执行现有车牌缓存逻辑（频次统计、颜色过滤）
  - 通过 MessageBus 发布 `PlateNumberChangedMessage` 以更新 UI
  - 按现有规则处理低优先级车牌颜色

#### 场景：AttendedWeighingService 在清理时释放 LPR 消息订阅
- **给定** `AttendedWeighingService` 正在被释放
- **且** MessageBus 订阅仍活跃
- **当** 调用 `DisposeAsync()` 方法
- **则** 系统应：
  - 对字段 `_licensePlateSubscription` 调用 `Dispose()`
  - 将字段置为 null 以释放引用
  - 继续执行原有释放逻辑
  - 防止未投递消息导致内存泄漏

---

### 需求：LicensePlateRecognizedMessage 定义

系统应提供统一的消息类，通过 ReactiveUI MessageBus 传递车牌识别数据，以一致结构支持所有 LPR 设备类型。

#### 场景：LicensePlateRecognizedMessage 承载完整识别数据
- **给定** 任意 LPR 设备类型发生车牌识别事件
- **当** 创建 `LicensePlateRecognizedMessage`
- **则** 消息应包含：
  - `PlateNumber`（string）：识别出的车牌文本（如 "京A12345"）
  - `ColorType`（LprAllInOneColorType?）：可选车牌颜色（蓝色、黄色、绿色等）
  - `DeviceType`（LprDeviceType）：表示设备类型的枚举值（Hikvision、LprAllInOne、Huaxiazhixin）
  - `DeviceName`（string）：配置中的可读设备名称
  - `Timestamp`（DateTime）：识别发生的 UTC 时间戳

#### 场景：通过 MessageBus 发布 LicensePlateRecognizedMessage
- **给定** 已用完整数据创建 `LicensePlateRecognizedMessage`
- **当** 使用 `MessageBus.Current.SendMessage(message)` 发布消息
- **则** 系统应：
  - 将消息投递到所有 `LicensePlateRecognizedMessage` 的活跃订阅方
  - 同步投递，无队列延迟（<1ms 延迟）
  - 保持所有消息属性不丢失
  - 允许多个订阅方接收同一消息实例

---

### 需求：服务接口简化

系统应从 `IAttendedWeighingService` 公开接口中移除 `OnPlateNumberRecognized` 方法，使其成为仅通过 MessageBus 订阅调用的私有实现细节。

#### 场景：IAttendedWeighingService 接口不包含 OnPlateNumberRecognized 方法
- **给定** 定义 `IAttendedWeighingService` 接口
- **当** 重构完成
- **则** 接口不得包含：
  - `void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)`
- **且** 接口应保留所有其他现有成员

#### 场景：AttendedWeighingService 将 OnPlateNumberRecognized 实现为 private 方法
- **给定** `AttendedWeighingService` 类实现 `IAttendedWeighingService`
- **且** MessageBus 订阅处于活跃状态
- **当** 订阅处理收到 `LicensePlateRecognizedMessage`
- **则** 类应调用私有方法 `OnPlateNumberRecognized()`
- **且** 该方法不得被外部代码访问
- **且** 该方法应保留现有实现逻辑（缓存、过滤等）

---

### 需求：MessageBus 订阅的防内存泄漏

系统应确保所有 MessageBus 订阅被正确释放，在长运行场景下防止内存泄漏。

#### 场景：订阅被正确保存并释放
- **给定** 某服务创建了 MessageBus 订阅
- **当** 创建该订阅
- **则** 系统应：
  - 将 `Subscribe()` 返回的 `IDisposable` 保存在私有字段
  - 添加说明释放责任的 XML 文档注释
  - 在释放流程中包含该字段

#### 场景：服务清理时释放订阅
- **给定** 存在活跃 MessageBus 订阅的服务正在被释放
- **当** 调用 `DisposeAsync()` 或 `Dispose()` 方法
- **则** 系统应：
  - 对所有保存的订阅 `IDisposable` 字段调用 `Dispose()`
  - 对 null 订阅做安全处理
  - 若启用日志，记录释放完成
  - 释放对订阅的所有引用

#### 场景：长运行系统中重复 LPR 事件不导致内存泄漏
- **给定** 系统长时间运行（24+ 小时）
- **且** LPR 设备识别了 1000+ 个车牌
- **当** 监控内存使用
- **则** 系统应：
  - 不因 MessageBus 订阅出现持续内存增长
  - 在服务重启时正确释放订阅
  - 通过内存泄漏测试，1000 次事件后增长 <1MB
  - 不在 MessageBus 队列中堆积未投递消息

---

## 交叉引用

**修改的需求**：
- LPR 服务与 MessageBus 集成（由直接服务调用 MODIFIED）
- AttendedWeighingService 订阅 LPR 消息（新增订阅模式）
- LicensePlateRecognizedMessage 定义（新增消息类）
- 服务接口简化（MODIFIED 接口）
- MessageBus 订阅的防内存泄漏（新增需求）

**相关能力**：
- `attended-weighing` —— 依赖 LPR 事件实现自动车牌采集

**基础规格**：
- `openspec/specs/license-plate-recognition/spec.md` —— 含未修改需求的完整规格

**相关变更**：
- `hikvision-lpr-implementation` —— 海康 LPR 服务与事件流实现
- `hikvision-lpr-integration` —— 海康配置与 UI
