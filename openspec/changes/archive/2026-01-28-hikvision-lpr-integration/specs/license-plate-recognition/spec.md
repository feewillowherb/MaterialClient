# 车牌识别规范增量

**能力**：`license-plate-recognition`
**变更 ID**：`hikvision-lpr-integration`
**类型**：ADDED（新增）

---

## 新增需求

### 需求：海康威视设备配置字段

系统应支持车牌识别设备的海康威视专用配置字段。

#### 场景：用户添加海康威视 LPR 设备配置
- **给定**系统配置为 `LprDeviceType = Hikvision`
- **当**用户在设置窗口中添加新的车牌识别设备时
- **则**系统应：
  - 显示海康威视专用配置字段：UserName、Password、Port、Channel
  - 将 Channel 字段默认值设为 "1"
  - 将 Channel 字段显示为只读（禁用）
  - 允许用户输入 UserName、Password、Port

#### 场景：用户查看已有海康威视 LPR 配置
- **给定**已有 LPR 配置且海康威视专用字段已填写
- **且** `LprDeviceType = Hikvision`
- **当**用户打开设置窗口时
- **则**系统应：
  - 显示所有海康威视专用字段及其已保存值
  - UserName 显示已配置值
  - Password 以掩码显示（PasswordChar="●"）
  - Port 显示已配置值
  - Channel 以只读显示且值为 "1"

#### 场景：用户将设备类型切换为 LprAllInOne
- **给定**当前 `LprDeviceType = Hikvision`
- **且**海康威视专用字段可见
- **当**用户将 `LprDeviceType` 改为 `LprAllInOne` 时
- **则**系统应：
  - 隐藏所有海康威视专用字段（UserName、Password、Port、Channel）
  - 在内存中保留海康威视字段值（不丢失）
  - 仅显示通用 LPR 字段（Name、Ip、Direction）
  - 无需重启窗口即可更新 UI

---

### 需求：按设备类型的动态字段可见性

系统应根据所选 `LprDeviceType` 动态显示或隐藏海康威视专用配置字段。

#### 场景：设备类型为 Hikvision 时海康威视字段可见
- **给定**用户在设置窗口中
- **且** `LprDeviceType = Hikvision`
- **则**系统应显示以下字段：
  - UserName（可编辑 TextBox）
  - Password（可编辑 TextBox，PasswordChar 掩码）
  - Port（可编辑 TextBox）
  - Channel（只读 TextBox，固定值 "1"）

#### 场景：设备类型为 LprAllInOne 时海康威视字段不可见
- **给定**用户在设置窗口中
- **且** `LprDeviceType = LprAllInOne`
- **则**系统不得显示：
  - UserName、Password、Port、Channel 字段
- **且**仅显示通用字段：Name、Ip、Direction

#### 场景：设备类型为 Huaxiazhixin 时海康威视字段不可见
- **给定**用户在设置窗口中
- **且** `LprDeviceType = Huaxiazhixin`
- **则**系统不得显示海康威视专用字段
- **且**仅显示通用字段：Name、Ip、Direction
- **原因**：华夏智信设备有不同配置需求（将在后续变更中实现）

---

### 需求：海康威视字段的 JSON 配置持久化

系统应将海康威视专用配置字段持久化到 JSON 存储，并在加载设置时正确恢复，且对旧数据保持向后兼容。

#### 场景：将海康威视 LPR 配置保存到 JSON
- **给定**用户已配置海康威视 LPR 设备（Name、Ip、Direction、UserName、Password、Port、Channel 等）
- **当**用户在设置窗口点击保存时
- **则**系统应将所有字段序列化到 `SettingsEntity.LicensePlateRecognitionConfigsJson`，并保存到 SQLite 数据库

#### 场景：从 JSON 加载海康威视 LPR 配置
- **给定**数据库中存在包含海康威视 LPR 配置的 JSON
- **当**用户打开设置窗口时
- **则**系统应反序列化并正确显示所有海康威视专用字段及通用字段

#### 场景：加载旧配置 JSON（向后兼容）
- **给定**数据库中存在不含海康威视字段的旧 JSON
- **当**用户打开设置窗口时
- **则**系统应成功反序列化且不抛异常，新字段为 null，Channel 在切换到海康威视类型时显示默认值 "1"，且无需手动数据迁移

#### 场景：混合设备类型的配置持久化
- **给定**用户配置了多台 LPR 设备（如海康威视与 LprAllInOne 混合）
- **当**用户保存并重新加载设置时
- **则**系统应正确序列化/反序列化各设备配置，并按设备类型显示对应字段

---

### 需求：海康威视 LPR 服务接口定义

系统应定义海康威视 LPR 设备集成的服务接口，为后续实现约定契约。

#### 场景：服务接口已定义
- **给定**系统需要支持海康威视 LPR 设备
- **当**开发团队实现配置与 UI 时
- **则**系统应在 `MaterialClient.Common.Services.Hikvision` 命名空间中定义 `IHikvisionLprService` 接口，声明 ConnectAsync、DisconnectAsync、StartListeningAsync、StopListeningAsync、PlateRecognized、IsConnected 等成员，并提供 XML 文档注释，且**不**包含任何实现代码（实现在单独提案中）。

#### 场景：接口遵循 ReactiveUI 模式
- **给定**项目使用 ReactiveUI
- **当**定义接口时
- **则**系统应使用 `IObservable<T>` 表示事件流（PlateRecognized），使用 `Task` 表示异步操作，并符合依赖注入与现有 ReactiveUI 模式。

**说明**：本需求仅确立接口定义，实际实现、HCNetSDK 集成与事件流逻辑不在范围内，由单独提案覆盖。

---

## 已移除需求

无。本变更未移除任何需求。

---

## 架构说明

### 设备类型枚举

系统使用 `LprDeviceType` 枚举区分设备类型；Hikvision 在本变更中支持，LprAllInOne 为现有功能，Huaxiazhixin 为未来支持。

### 条件字段显示逻辑

UI 使用计算属性决定字段可见性（如 `ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision`），并在 XAML 中绑定到 `IsVisible`。

### JSON 存储架构

配置数据存储在 `SettingsEntity.LicensePlateRecognitionConfigsJson`（NVARCHAR 列），使用 `System.Text.Json` 序列化/反序列化；旧 JSON 缺字段时反序列化自动置为 null，无需数据库迁移。

### 服务接口设计

`IHikvisionLprService` 定义于 `MaterialClient.Common.Services.Hikvision`，采用异步与 ReactiveUI IObservable 模式；本变更仅定义接口，不包含实现。

---

## 测试要求

### 单元测试
- 按设备类型验证 `ShowHikvisionLprFields`；验证 `LicensePlateRecognitionConfig` 海康威视字段及 `IsValid()`；验证 `IHikvisionLprService` 接口定义正确。

### 集成测试
- 验证海康威视配置的保存/加载、旧 JSON 反序列化、空字段往返序列化、混合设备类型保存/加载。

### UI 测试
- 手动验证设备类型切换时海康威视字段的显示/隐藏及添加/编辑/删除配置；验证加载旧配置不导致崩溃。

### JSON 序列化测试
- 验证序列化包含所有新字段、旧 JSON 反序列化成功、缺字段置为 null、往返序列化数据保持、Channel 默认值应用。

---

## 依赖与更新

**依赖**：SystemSettings.LprDeviceType、LprDeviceType 枚举、LicensePlateRecognitionConfig、SettingsEntity.LicensePlateRecognitionConfigsJson、System.Text.Json、ReactiveUI 等（均已存在）。

**更新**：LicensePlateRecognitionConfig（新增 4 属性）、LicensePlateRecognitionConfigViewModel、SettingsWindowViewModel（新增 ShowHikvisionLprFields）、AddLprDialogViewModel、SettingsWindow.axaml、AddLprDialog.axaml、IHikvisionLprService（新文件，仅定义）。

**无需变更**：数据库架构、现有 LprAllInOne 配置、CameraConfig 等。

---

## 非目标

以下明确不在本变更范围内：海康威视 LPR 服务实现、华夏智信设备支持、密码加密、海康威视字段的额外配置校验、配置导入/导出、数据库迁移。

---

## 参考

- 本变更的 proposal.md、design.md、tasks.md
- attended-weighing 规范、LicensePlateRecognitionConfig、SystemSettings、SettingsEntity 等。
