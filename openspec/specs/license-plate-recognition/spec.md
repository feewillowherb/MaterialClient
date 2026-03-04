# 车牌识别 规范

## 目的
待定 - 由变更 hikvision-lpr-integration 归档后创建。归档后更新目的。

## 需求

### 需求：海康威视设备配置字段

系统应支持车牌识别设备的海康威视专用配置字段。

#### 场景：用户添加海康威视 LPR 设备配置
- **假设** 系统配置为 `LprDeviceType = Hikvision`
- **当** 用户在设置窗口中添加新的车牌识别设备
- **则** 系统应：
  - 显示海康威视专用配置字段：UserName、Password、Port、Channel
  - 将 Channel 字段默认值设为 "1"
  - 将 Channel 字段显示为只读（禁用）
  - 允许用户输入 UserName、Password、Port

#### 场景：用户查看已有海康威视 LPR 配置
- **假设** 已有填好海康威视专用字段的 LPR 配置
- **且** `LprDeviceType = Hikvision`
- **当** 用户打开设置窗口
- **则** 系统应：
  - 显示所有海康威视专用字段及其已保存值
  - UserName 显示已配置值
  - Password 以掩码显示（PasswordChar="●"）
  - Port 显示已配置值
  - Channel 以只读显示，值为 "1"

#### 场景：用户将设备类型切换为 LprAllInOne
- **假设** 当前 `LprDeviceType = Hikvision`
- **且** 海康威视专用字段可见
- **当** 用户将 `LprDeviceType` 改为 `LprAllInOne`
- **则** 系统应：
  - 隐藏所有海康威视专用字段（UserName、Password、Port、Channel）
  - 在内存中保留海康威视字段值（不丢失）
  - 仅显示通用 LPR 字段（Name、Ip、Direction）
  - 无需重启窗口即可更新 UI

---

### 需求：按设备类型动态显示字段

系统应根据所选 `LprDeviceType` 动态显示或隐藏海康威视专用配置字段。

#### 场景：设备类型为 Hikvision 时显示海康威视字段
- **假设** 用户在设置窗口中
- **且** `LprDeviceType = Hikvision`
- **则** 系统应显示：UserName（可编辑）、Password（可编辑带掩码）、Port（可编辑）、Channel（只读，固定值 "1"）

#### 场景：设备类型为 LprAllInOne 时不显示海康威视字段
- **假设** 用户在设置窗口中且 `LprDeviceType = LprAllInOne`
- **则** 系统不应显示 UserName、Password、Port、Channel，仅显示 Name、Ip、Direction

#### 场景：设备类型为 Huaxiazhixin 时不显示海康威视字段
- **假设** 用户在设置窗口中且 `LprDeviceType = Huaxiazhixin`
- **则** 系统不应显示海康威视专用字段，仅显示 Name、Ip、Direction（华夏智信设备配置将在后续变更中实现）

---

### 需求：海康威视字段的 JSON 配置持久化

系统应将海康威视专用配置字段持久化到 JSON，并在加载设置时正确恢复，同时兼容旧数据。

#### 场景：保存海康威视 LPR 配置到 JSON
- **假设** 用户已配置海康威视 LPR（含 Name、Ip、Direction、UserName、Password、Port、Channel）
- **当** 用户在设置窗口点击保存
- **则** 系统应将所有字段序列化到 `SettingsEntity.LicensePlateRecognitionConfigsJson`，并保存到 SQLite

#### 场景：从 JSON 加载海康威视 LPR 配置
- **假设** 数据库中存在含完整海康威视字段的 JSON
- **当** 用户打开设置窗口
- **则** 系统应反序列化并正确显示所有海康威视字段及通用字段

#### 场景：加载旧配置 JSON（向后兼容）
- **假设** 数据库中存在不含海康威视字段的旧 JSON
- **当** 用户打开设置窗口
- **则** 系统应成功反序列化、正确加载已有字段、将新海康威视字段设为 null，且无需手动数据迁移

#### 场景：混合设备类型配置持久化
- **假设** 用户配置了多台 LPR（含海康威视与 LprAllInOne）
- **当** 用户保存并重新加载
- **则** 系统应正确序列化/反序列化各设备配置并保持完整

---

### 需求：海康威视 LPR 服务接口定义

系统应定义海康威视 LPR 设备集成的服务接口，为后续实现确立契约。

#### 场景：已定义服务接口
- **则** 系统应在命名空间 `MaterialClient.Common.Services.Hikvision` 中定义 `IHikvisionLprService` 接口
- **且** 声明方法：ConnectAsync、DisconnectAsync、StartListeningAsync、StopListeningAsync
- **且** 声明属性：PlateRecognized（IObservable）、IsConnected
- **且** 为所有成员提供 XML 文档注释，不包含实现代码（实现在单独提案中）

#### 场景：接口遵循 ReactiveUI 模式
- **则** 应使用 IObservable 表示事件流、Task 表示异步操作，并遵循现有 ReactiveUI 模式与依赖注入

**说明**：本需求仅确立接口定义；实际实现与 HCNetSDK 集成由单独提案覆盖。
