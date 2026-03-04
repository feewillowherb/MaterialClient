# Design: 新增海康威视LPR设备支持

**Change ID**: `hikvision-lpr-integration`
**Status**: Draft
**Created**: 2026-01-28

---

## Architecture Overview

本设计文档说明如何扩展 MaterialClient 的车牌识别功能,支持海康威视 LPR 设备。设计重点在于**配置模型扩展**、**动态 UI 实现**和**服务接口定义**。

### 范围定义

**包含**:
- ✅ 配置模型扩展(`LicensePlateRecognitionConfig`)
- ✅ JSON 序列化/反序列化兼容性处理
- ✅ 动态 UI 实现(SettingsWindow、AddLprDialog)
- ✅ 设备类型条件判断逻辑
- ✅ 服务接口定义(`IHikvisionLprService`)

**不包含**（本设计不涉及）:
- ❌ 海康威示 LPR 监听服务实现(需单独提案)
- ❌ HCNetSDK LPR 组件集成
- ❌ 车牌识别事件流实现
- ❌ `IHikvisionLprService` 的具体实现类

---

## 组件设计

### 1. 配置模型扩展

#### 1.1 LicensePlateRecognitionConfig 实体

**当前状态**:
```csharp
public class LicensePlateRecognitionConfig
{
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.In;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Ip);
    }
}
```

**提议变更**:
```csharp
public class LicensePlateRecognitionConfig
{
    // 现有字段(所有设备类型通用)
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.In;

    // 海康威视专用字段(仅在 LprDeviceType == Hikvision 时使用)
    /// <summary>
    ///     设备认证用户名(海康威视专用)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    ///     设备认证密码(海康威视专用)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     设备服务端口号(海康威视专用)
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    ///     通道号(海康威视专用,默认值为 "1")
    /// </summary>
    public string? Channel { get; set; }

    public bool IsValid()
    {
        // 保持现有验证逻辑:仅验证通用字段
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Ip);
    }
}
```

**设计理由**:
- **可空类型**: 新字段使用可空类型(`string?`),确保向后兼容性,不影响现有 LPRAllInOne 设备配置
- **无需验证**: `IsValid()` 方法不验证海康威示字段,因为:
  - 现有 LPRAllInOne 设备不需要这些字段
  - 验证逻辑应放在服务层,而非配置模型
  - 保持向后兼容性
- **默认值**: `Channel` 字段在 UI 层提供默认值 "1",不在模型层硬编码

#### 1.2 JSON 存储策略

**存储架构**:
```
SettingsEntity (SQLite Table)
├── LicensePlateRecognitionConfigsJson (NVARCHAR column)
│   └── "[{\"Name\":\"device1\",\"Ip\":\"192.168.1.100\",\"Direction\":0,\"UserName\":\"admin\",\"Password\":\"123\",\"Port\":\"8000\",\"Channel\":\"1\"}]"
└── ... other JSON columns
```

**要点**:
- **不需要数据库迁移**: 新增字段只改变 JSON 内容,不改变表结构
- **System.Text.Json 自动处理**:
  - **反序列化**: 旧 JSON 缺少字段时,对应属性为 `null`
  - **序列化**: 新字段会被包含在 JSON 输出中
- **向后兼容**: 旧配置数据可以正常加载,新字段为 `null`

**JSON 兼容性示例**：

旧 JSON（v1.0）：
```json
[
  {
    "Name": "lpr_device_1",
    "Ip": "192.168.1.100",
    "Direction": 0
  }
]
```

新 JSON（v2.0）：
```json
[
  {
    "Name": "lpr_device_1",
    "Ip": "192.168.1.100",
    "Direction": 0,
    "UserName": "admin",
    "Password": "password123",
    "Port": "8000",
    "Channel": "1"
  }
]
```

**兼容性处理**：
```csharp
// 加载旧数据时
Channel = config.Channel ?? "1"  // 旧数据为 null,使用默认值 "1"

// 保存新数据时
Channel = Channel ?? "1"          // 确保 Channel 始终有值
```

---

### 2. 动态 UI 设计

#### 2.1 SettingsWindowViewModel 扩展

**新增计算属性**：
```csharp
/// <summary>
///     是否显示海康威视专用配置字段
/// </summary>
public bool ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision;
```

**属性变更通知**：
```csharp
[Reactive] private LprDeviceType _lprDeviceType = LprDeviceType.Hikvision;

public LprDeviceType LprDeviceType
{
    get => _lprDeviceType;
    set => this.RaiseAndSetIfChanged(ref _lprDeviceType, value);
}
```

**设计理由**:
- **计算属性**: `ShowHikvisionLprFields` 不需要存储状态,直接计算结果
- **ReactiveUI 集成**: 使用 `RaiseAndSetIfChanged` 自动触发属性变化通知
- **UI 绑定**: XAML 中的 `IsVisible="{Binding ShowHikvisionLprFields}"` 会自动响应变化

#### 2.2 LicensePlateRecognitionConfigViewModel 扩展

**新增属性**：
```csharp
public partial class LicensePlateRecognitionConfigViewModel : ReactiveObject
{
    // 现有属性
    [Reactive] private string _name = string.Empty;
    [Reactive] private string _ip = string.Empty;
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.In;

    // 新增海康威视字段
    [Reactive] private string? _userName;
    [Reactive] private string? _password;
    [Reactive] private string? _port;
    [Reactive] private string? _channel;

    // 公共属性包装器
    public string? UserName
    {
        get => _userName;
        set => this.RaiseAndSetIfChanged(ref _userName, value);
    }
    // ... 其他字段类似
}
```

**设计理由**:
- **一致性**: 所有字段使用 `[Reactive]` + `RaiseAndSetIfChanged` 模式
- **可空类型**: 保持与配置模型一致的可空性
- **源生成器**: 使用 `ReactiveUI.SourceGenerators` 减少样板代码

#### 2.3 AddLprDialogViewModel 扩展

**构造函数参数**：
```csharp
public partial class AddLprDialogViewModel : ViewModelBase
{
    private readonly LprDeviceType _lprDeviceType;

    public AddLprDialogViewModel(LprDeviceType lprDeviceType)
    {
        _lprDeviceType = lprDeviceType;
        // ... 现有初始化代码
    }

    public bool ShowHikvisionLprFields => _lprDeviceType == LprDeviceType.Hikvision;
}
```

**更新后的保存命令**：
```csharp
[ReactiveCommand]
private void Save()
{
    Result = new LicensePlateRecognitionConfigViewModel
    {
        Name = Name,
        Ip = Ip,
        Direction = Direction,
        UserName = UserName,
        Password = Password,
        Port = Port,
        Channel = Channel ?? "1"  // 默认值
    };
}
```

**设计理由**:
- **依赖注入**: 通过构造函数传递 `LprDeviceType`,避免全局状态依赖
- **默认值应用**: 在保存时应用 `Channel = "1"` 默认值,确保配置完整性
- **条件显示**: `ShowHikvisionLprFields` 基于传入的 `LprDeviceType` 计算

#### 2.4 XAML UI 设计

**SettingsWindow.axaml - 海康威视字段**：
```xml
<!-- 车牌识别配置列表 -->
<ItemsControl ItemsSource="{Binding LicensePlateRecognitionConfigs}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="0,0,0,10">
                <!-- 现有通用字段 -->
                <TextBlock Text="设备名称:" />
                <TextBox Text="{Binding Name}" IsEnabled="False" />

                <TextBlock Text="IP地址:" />
                <TextBox Text="{Binding Ip}" IsEnabled="False" />

                <TextBlock Text="方向:" />
                <TextBlock Text="{Binding DirectionText}" IsEnabled="False" />

                <!-- 海康威视专用字段(条件显示) -->
                <StackPanel IsVisible="{Binding $parent[Window].ShowHikvisionLprFields}" Margin="0,10,0,0">
                    <TextBlock Text="用户名:" FontWeight="Bold" />
                    <TextBox Text="{Binding UserName}"
                             Watermark="admin"
                             IsEnabled="False" />

                    <TextBlock Text="密码:" FontWeight="Bold" />
                    <TextBox Text="{Binding Password}"
                             Watermark="请输入密码"
                             PasswordChar="●"
                             IsEnabled="False" />

                    <TextBlock Text="端口:" FontWeight="Bold" />
                    <TextBox Text="{Binding Port}"
                             Watermark="8000"
                             IsEnabled="False" />

                    <TextBlock Text="通道:" FontWeight="Bold" />
                    <TextBox Text="{Binding Channel}"
                             IsEnabled="False"
                             Background="LightGray" />
                </StackPanel>
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**AddLprDialog.axaml**:
```xml
<StackPanel>
    <!-- 通用字段 -->
    <TextBlock Text="设备名称:" />
    <TextBox Text="{Binding Name}" Watermark="camera_1" />

    <TextBlock Text="IP地址:" />
    <TextBox Text="{Binding Ip}" Watermark="192.168.1.100" />

    <TextBlock Text="方向:" />
    <ComboBox SelectedIndex="{Binding DirectionIndex}">
        <ComboBoxItem Content="进场" />
        <ComboBoxItem Content="出场" />
    </ComboBox>

    <!-- 海康威视专用字段(条件显示) -->
    <StackPanel IsVisible="{Binding ShowHikvisionLprFields}">
        <Separator Margin="0,10" />
        <TextBlock Text="海康威视设备配置" FontWeight="Bold" FontSize="14" />

        <TextBlock Text="用户名:" />
        <TextBox Text="{Binding UserName}" Watermark="admin" />

        <TextBlock Text="密码:" />
        <TextBox Text="{Binding Password}"
                 Watermark="请输入密码"
                 PasswordChar="●" />

        <TextBlock Text="端口:" />
        <TextBox Text="{Binding Port}" Watermark="8000" />

        <TextBlock Text="通道:" />
        <TextBox Text="{Binding Channel}"
                 IsEnabled="False"
                 Background="LightGray">
            <TextBox.ToolTip>
                <ToolTip Content="通道号固定为 1,无需修改" />
            </TextBox.ToolTip>
        </TextBox>
    </StackPanel>
</StackPanel>
```

**设计理由**:
- **条件显示**: 使用 `IsVisible` 绑定到 `ShowHikvisionLprFields`
- **视觉区分**: 使用 `Separator` 和 `FontWeight="Bold"` 区分海康威示字段组
- **只读字段**: `Channel` 字段显示为灰色且禁用,表示不可编辑
- **用户提示**: 使用 `Watermark` 和 `ToolTip` 提供上下文帮助
- **密码保护**: 使用 `PasswordChar="●"` 隐藏密码输入

---

### 3. 服务接口设计

#### 3.1 IHikvisionLprService 接口

**接口定义**：
```csharp
namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别服务接口
/// </summary>
public interface IHikvisionLprService
{
    /// <summary>
    ///     连接到海康威视 LPR 设备
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>连接是否成功</returns>
    Task<bool> ConnectAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    ///     开始监听车牌识别事件
    /// </summary>
    Task StartListeningAsync();

    /// <summary>
    ///     停止监听
    /// </summary>
    Task StopListeningAsync();

    /// <summary>
    ///     车牌识别事件流
    /// </summary>
    IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }

    /// <summary>
    ///     连接状态
    /// </summary>
    bool IsConnected { get; }
}
```

**事件类型定义（临时）**：
```csharp
namespace MaterialClient.Common.Events;

/// <summary>
///     车牌识别事件(临时定义,后续提案完善)
/// </summary>
public class LicensePlateRecognizedEvent
{
    public string PlateNumber { get; set; } = string.Empty;
    public LicensePlateDirection Direction { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string DeviceName { get; set; } = string.Empty;
    // 后续可能添加: 图片路径、置信度、车牌颜色等
}
```

**设计理由**:
- **接口隔离**: 仅定义方法签名,不包含实现细节
- **ReactiveUI 集成**: 使用 `IObservable<T>` 定义事件流,符合项目 ReactiveUI 模式
- **异步设计**: 所有操作使用 `Task` 异步模式
- **生命周期管理**: 明确定义连接、监听、断开的生命周期方法
- **状态查询**: 提供 `IsConnected` 属性查询连接状态

**实现说明**：本提案仅定义接口，具体实现（含 HCNetSDK 集成、Rx 订阅管理、内存泄漏预防等）在后续提案中完成。

---

## Data Flow

### Load Configuration Flow

```
用户打开设置窗口
    ↓
SettingsWindowViewModel.LoadSettingsAsync()
    ↓
从 SettingsEntity.LicensePlateRecognitionConfigsJson 读取 JSON
    ↓
System.Text.Json.Deserialize<List<LicensePlateRecognitionConfig>>()
    ↓
每个 LicensePlateRecognitionConfig 映射到 LicensePlateRecognitionConfigViewModel
    ↓
旧数据兼容性处理:
  - UserName = config.UserName (旧数据为 null)
  - Password = config.Password (旧数据为 null)
  - Port = config.Port (旧数据为 null)
  - Channel = config.Channel ?? "1" (旧数据为 null 时使用默认值)
    ↓
ViewModel 属性更新触发 UI 刷新
    ↓
UI 显示配置列表(根据 LprDeviceType 显示/隐藏海康威示字段)
```

### Save Configuration Flow

```
用户点击保存按钮
    ↓
SettingsWindowViewModel.SaveAsync()
    ↓
将 LicensePlateRecognitionConfigViewModel 集合映射回 LicensePlateRecognitionConfig 实体
    ↓
保存到 SettingsEntity
    ↓
SettingsEntity.LicensePlateRecognitionConfigsJson 自动序列化为 JSON
    ↓
System.Text.Json.Serialize<List<LicensePlateRecognitionConfig>>()
    ↓
保存到 SQLite 数据库(通过 ISettingsService)
    ↓
发送 SettingsSavedMessage 消息
    ↓
关闭窗口
```

### Device Type Switch Flow

```
用户在设置窗口修改 LprDeviceType
    ↓
SettingsWindowViewModel.LprDeviceType 属性变化
    ↓
RaiseAndSetIfChanged 触发 PropertyChanged 事件
    ↓
ShowHikvisionLprFields 计算属性重新计算
    ↓
UI 通过绑定收到 ShowHikvisionLprFields 变化通知
    ↓
海康威示字段组显示/隐藏动画执行
```

---

## 验证策略

### 配置模型验证

**验证范围**：
- ✅ 字段类型正确性
- ✅ 可空性约束
- ✅ `IsValid()` 方法向后兼容性

**验证方法**：单元测试

### ViewModel 验证

**验证范围**：
- ✅ `ShowHikvisionLprFields` 计算逻辑
- ✅ 属性变化通知触发
- ✅ `LprDeviceType` 切换响应

**验证方法**：单元测试

### JSON 序列化验证

**验证范围**：
- ✅ 序列化包含所有新字段
- ✅ 反序列化旧 JSON(缺少字段)不会抛出异常
- ✅ 反序列化后新字段为 null
- ✅ 往返序列化(serialize → deserialize)数据完整性

**验证方法**：集成测试（新增关键测试）

### UI 验证

**验证范围**：
- ✅ 字段显示/隐藏逻辑
- ✅ 用户输入绑定
- ✅ 设备类型切换动画
- ✅ 旧数据加载不崩溃

**验证方法**：手动 UI 测试

---

## 错误处理

### JSON 反序列化错误

**场景**： 旧 JSON 数据格式损坏或无法解析

**处理策略**：
1. 在 `SettingsEntity.LicensePlateRecognitionConfigs` 属性的 `get` 方法中捕获异常
2. 返回空列表 `new List<LicensePlateRecognitionConfig>()`
3. 记录错误日志
4. 向用户显示友好错误消息

**当前实现**（已具备安全处理）：
```csharp
public List<LicensePlateRecognitionConfig> LicensePlateRecognitionConfigs
{
    get
    {
        if (string.IsNullOrEmpty(LicensePlateRecognitionConfigsJson))
            return new List<LicensePlateRecognitionConfig>();

        try
        {
            return JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(
                LicensePlateRecognitionConfigsJson) ?? new List<LicensePlateRecognitionConfig>();
        }
        catch
        {
            return new List<LicensePlateRecognitionConfig>();  // ✅ 已有异常处理
        }
    }
    set => LicensePlateRecognitionConfigsJson = JsonSerializer.Serialize(value);
}
```

### 无效用户输入

**场景**： 用户输入无效端口号(如非数字字符)

**处理策略**：
1. 在 UI 层使用输入验证(如 `NumericValidation`)
2. 在保存时验证端口格式
3. 记录验证失败日志
4. 向用户显示具体错误字段

**未来增强**：
- 添加 `IDataErrorInfo` 或 `INotifyDataErrorInfo` 实现
- 实时验证反馈(红色边框 + 错误消息)

---

## 性能考虑

### JSON 序列化性能

**影响**： 低 - 配置数据量小(通常 < 10 条记录)

**优化**：不需要

**说明**： `System.Text.Json` 性能优异,对于小配置列表序列化/反序列化耗时 < 1ms。

### UI 渲染性能

**影响**： 低 - 条件显示仅影响少量控件(4 个字段)

**Optimization**: 不需要优化

**潜在问题**：
- 频繁切换 `LprDeviceType` 可能导致重复布局计算
- **缓解**： 使用 `IsVisible` 而非 `Visibility` 属性,避免布局重建

### 内存占用

**影响**： 可忽略 - 每个配置对象增加 ~100 字节(4 个字符串引用)

**Optimization**: 不需要优化

---

## 安全考虑

### 密码存储

**风险**： 密码以明文形式存储在 JSON 中

**当前状态**：
- ✅ 与现有 `CameraConfig.Password` 保持一致(明文存储)
- ⚠️ JSON 文件可能被其他工具读取

**缓解**：
- 当前提案保持现状,不改变密码存储方式
- 与项目现有安全模型一致

**未来增强**：
- 使用 Windows DPAPI 加密密码字段
- 使用 `ProtectedData` 类保护敏感信息
- 参考 `CameraConfig` 的加密实现(如果存在)

### 输入验证

**风险**： JSON 注入或 XSS 攻击(通过用户输入)

**缓解**：
- ✅ `System.Text.Json` 自动转义特殊字符
- ✅ Avalonia XAML 自动转义文本内容
- ✅ 不使用 `eval` 或动态代码执行
- ⚠️ **Future**: 添加输入长度限制,防止 DoS 攻击

---

## 未来增强

### 短期（未来 3 个月）

1. **配置验证增强**: 为海康威示字段添加设备类型特定的验证逻辑
2. **连接测试**: 添加"测试连接"按钮,验证海康威示设备可达性
3. **配置导入/导出**: 支持配置文件的导入和导出,便于批量部署
4. **密码加密**: 使用 DPAPI 加密所有密码字段

### 长期（6–12 个月）

1. **配置模板**: 预定义常见设备的配置模板
2. **设备发现**: 自动发现网络中的海康威示设备
3. **华夏智信支持**: 扩展 `Huaxiazhixin` 设备类型的配置支持
4. **配置版本控制**: 添加配置版本号,处理未来格式变更

### 超出范围（需单独提案）

1. **海康威示 LPR 监听服务实现**: 需要技术评审和架构设计
2. **HCNetSDK LPR 组件集成**: 需要评估 SDK 能力和接口
3. **车牌识别事件流实现**: 需要设计 ReactiveUI 事件流架构
4. **设备状态监控**: 需要设计健康检查和重连机制

---

## 参考

- **Avalonia XAML Data Binding**: https://docs.avaloniaui.net/docs/data-binding/
- **ReactiveUI Property Change Notifications**: https://www.reactiveui.net/docs/handboook/view-models/property-change-notifications
- **System.Text.Json Documentation**: https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview
- **Project Conventions**: `openspec/project.md`
- **Existing Config**: `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs`
- **SettingsEntity**: `MaterialClient.Common/Entities/SettingsEntity.cs`
- **HCNetSDK Documentation**: `MaterialClient.Common/HCNetSDK/README.md`
