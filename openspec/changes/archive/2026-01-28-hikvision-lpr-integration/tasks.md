# Tasks: 新增海康威视LPR设备支持

**Change ID**: `hikvision-lpr-integration`
**Total Tasks**: 16
**Estimated Duration**: 2-3 days

---

## 任务概览

本变更分四个阶段实施：
1. **阶段1: 配置模型扩展** - 修改 `LicensePlateRecognitionConfig` 实体类
2. **阶段2: 动态UI实现** - 修改 `SettingsWindowViewModel`、`AddLprDialogViewModel` 和相关视图
3. **阶段3: 测试和验证** - 编写单元测试、集成测试和UI测试
4. **阶段4: 服务接口定义** - 定义 `IHikvisionLprService` 接口

**重要说明**:
- ❌ **不涉及数据库迁移**: 配置数据存储在 `SettingsEntity.LicensePlateRecognitionConfigsJson` 字段中(JSON 格式)
- ✅ **JSON 兼容性**: 新增字段为可空类型,旧数据缺少字段时自动使用 null 或默认值
- ⚠️ **监听服务实现**: 本提案仅定义接口,具体实现在后续提案中

---

## Phase 1: 配置模型扩展

### Task 1.1: 扩展 LicensePlateRecognitionConfig 实体类

**Status**: Completed
**Priority**: High
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
为 `LicensePlateRecognitionConfig` 添加海康威视设备所需的认证和连接参数。

**Steps**:
1. 打开 `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs`
2. 添加以下属性(可空字符串类型):
   - `public string? UserName { get; set; }`
   - `public string? Password { get; set; }`
   - `public string? Port { get; set; }`
   - `public string? Channel { get; set; }`
3. 更新 XML 注释,说明这些字段仅在 `LprDeviceType == Hikvision` 时使用
4. 保持 `IsValid()` 方法不变(仅验证 `Name` 和 `Ip`)

**Validation**:
- [x] 新属性已添加到类中
- [x] XML 注释完整且准确
- [x] 代码编译通过

**Output**: 更新的 `LicensePlateRecognitionConfig.cs`

**Note**: 不需要数据库迁移,因为配置数据存储在 JSON 字段中,`System.Text.Json` 会自动处理新增字段(旧数据缺少字段时反序列化为 null)。

---

### Task 1.2: 更新 LicensePlateRecognitionConfigViewModel

**Status**: Completed
**Priority**: High
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
更新 `LicensePlateRecognitionConfigViewModel` 以支持海康威视字段。

**Steps**:
1. 打开 `MaterialClient/ViewModels/SettingsWindowViewModel.cs`
2. 找到 `LicensePlateRecognitionConfigViewModel` 类(在文件底部)
3. 添加以下属性:
   - `[Reactive] private string? _userName;`
   - `[Reactive] private string? _password;`
   - `[Reactive] private string? _port;`
   - `[Reactive] private string? _channel;`
4. 为每个属性创建公共属性包装器

**Validation**:
- [x] 新属性已添加到 ViewModel
- [x] 所有属性使用 `[Reactive]` 标记
- [x] 代码编译通过

**Output**: 更新的 `SettingsWindowViewModel.cs`

---

## Phase 2: 动态UI实现

### Task 2.1: 更新 SettingsWindowViewModel 加载逻辑

**Status**: Completed
**Priority**: High
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
修改 `LoadSettingsAsync()` 方法,从配置中加载海康威视字段,并提供旧数据兼容性处理。

**Steps**:
1. 在 `LoadSettingsAsync()` 方法中找到加载 LPR 配置的代码(约第506-514行)
2. 修改 `LicensePlateRecognitionConfigViewModel` 初始化逻辑:
   ```csharp
   LicensePlateRecognitionConfigs.Add(new LicensePlateRecognitionConfigViewModel
   {
       Name = config.Name,
       Ip = config.Ip,
       Direction = config.Direction,
       UserName = config.UserName,              // 新增,旧数据为 null
       Password = config.Password,              // 新增,旧数据为 null
       Port = config.Port,                      // 新增,旧数据为 null
       Channel = config.Channel ?? "1"          // 新增,旧数据为 null 时使用默认值 "1"
   });
   ```

**Validation**:
- [x] 加载逻辑已更新
- [x] 默认值 "1" 应用于 Channel(当旧数据为 null 时)
- [x] 代码编译通过

**Output**: 更新的 `LoadSettingsAsync()` 方法

**Note**: 由于字段为可空类型,旧 JSON 数据缺少这些字段时,`System.Text.Json` 会自动将其反序列化为 null,不会抛出异常。

---

### Task 2.2: 更新 SettingsWindowViewModel 保存逻辑

**Status**: Completed
**Priority**: High
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
修改 `SaveAsync()` 方法,将海康威视字段保存到 JSON 配置中。

**Steps**:
1. 在 `SaveAsync()` 方法中找到保存 LPR 配置的代码(约第192-197行)
2. 修改 `LicensePlateRecognitionConfig` 映射逻辑:
   ```csharp
   LicensePlateRecognitionConfigs.Select(l => new LicensePlateRecognitionConfig
   {
       Name = l.Name,
       Ip = l.Ip,
       Direction = l.Direction,
       UserName = l.UserName,    // 新增
       Password = l.Password,    // 新增
       Port = l.Port,            // 新增
       Channel = l.Channel       // 新增
   }).ToList(),
   ```

**Validation**:
- [x] 保存逻辑已更新
- [x] 所有字段正确映射到配置对象
- [x] 代码编译通过

**Output**: 更新的 `SaveAsync()` 方法

**Note**: `SettingsEntity` 会自动将配置对象序列化为 JSON,新字段会被包含在 JSON 输出中(null 值字段可选配置)。

---

### Task 2.3: 添加设备类型判断属性到 ViewModel

**Status**: Completed
**Priority**: High
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
在 `SettingsWindowViewModel` 中添加计算属性,用于 UI 字段的条件显示。

**Steps**:
1. 在 `SettingsWindowViewModel` 类中添加以下计算属性:
   ```csharp
   /// <summary>
   /// 是否显示海康威视专用配置字段
   /// </summary>
   public bool ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision;
   ```
2. 确保属性在 `LprDeviceType` 变化时触发通知

**Validation**:
- [x] `ShowHikvisionLprFields` 属性已添加
- [x] 属性在 `LprDeviceType` 变化时自动更新
- [x] 代码编译通过

**Output**: 新增的计算属性

---

### Task 2.4: 更新 AddLprDialogViewModel

**Status**: Completed
**Priority**: High
**Estimated**: 45 minutes
**Completed**: 2026-01-28

**Description**:
修改 `AddLprDialogViewModel`,支持海康威视配置字段。

**Steps**:
1. 打开 `MaterialClient/ViewModels/AddLprDialogViewModel.cs`
2. 添加以下属性:
   ```csharp
   [Reactive] private string? _userName = string.Empty;
   [Reactive] private string? _password = string.Empty;
   [Reactive] private string? _port = string.Empty;
   [Reactive] private string? _channel = "1"; // 默认值
   ```
3. 为每个属性创建公共属性包装器
4. 修改 `Save()` 命令,将新字段保存到 `Result`:
   ```csharp
   Result = new LicensePlateRecognitionConfigViewModel
   {
       Name = Name,
       Ip = Ip,
       Direction = Direction,
       UserName = UserName,
       Password = Password,
       Port = Port,
       Channel = Channel ?? "1"
   };
   ```

**Validation**:
- [x] 新属性已添加
- [x] `Save()` 命令已更新
- [x] 代码编译通过

**Output**: 更新的 `AddLprDialogViewModel.cs`

---

### Task 2.5: 添加 LprDeviceType 参数到 AddLprDialog

**Status**: Completed
**Priority**: Medium
**Estimated**: 30 minutes
**Completed**: 2026-01-28

**Description**:
修改 `AddLprDialogViewModel`,接收 `LprDeviceType` 参数以控制字段显示。

**Steps**:
1. 添加构造函数参数和字段:
   ```csharp
   private readonly LprDeviceType _lprDeviceType;

   public AddLprDialogViewModel(LprDeviceType lprDeviceType)
   {
       _lprDeviceType = lprDeviceType;
       // ... 现有初始化代码
   }
   ```
2. 添加计算属性:
   ```csharp
   public bool ShowHikvisionLprFields => _lprDeviceType == LprDeviceType.Hikvision;
   ```
3. 更新 `SettingsWindowViewModel.AddLicensePlateRecognitionAsync()`,传递 `LprDeviceType`:
   ```csharp
   var dialogViewModel = new AddLprDialogViewModel(LprDeviceType);
   ```

**Validation**:
- [x] 构造函数接受 `LprDeviceType` 参数
- [x] `ShowHikvisionLprFields` 属性已添加
- [x] `AddLicensePlateRecognitionAsync()` 已更新
- [x] 代码编译通过

**Output**: 更新的 `AddLprDialogViewModel` 和调用代码

---

### Task 2.6: 更新 EditLprAsync 命令

**Status**: Completed
**Priority**: Medium
**Estimated**: 20 minutes
**Completed**: 2026-01-28

**Description**:
修改 `EditLprAsync()` 命令,支持编辑海康威视字段。

**Steps**:
1. 在 `SettingsWindowViewModel.EditLprAsync()` 方法中
2. 更新 `AddLprDialogViewModel` 初始化:
   ```csharp
   var dialogViewModel = new AddLprDialogViewModel(LprDeviceType)
   {
       Name = config.Name,
       Ip = config.Ip,
       Direction = config.Direction,
       UserName = config.UserName,    // 新增
       Password = config.Password,    // 新增
       Port = config.Port,            // 新增
       Channel = config.Channel       // 新增
   };
   ```

**Validation**:
- [x] 编辑逻辑已更新
- [x] 所有海康威视字段正确传递
- [x] 代码编译通过

**Output**: 更新的 `EditLprAsync()` 方法

---

### Task 2.7: 更新 SettingsWindow.axaml

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour
**Completed**: 2026-01-28

**Description**:
修改 `SettingsWindow.axaml`,添加海康威视配置字段并实现条件显示。

**Steps**:
1. 打开 `MaterialClient/Views/SettingsWindow.axaml`
2. 找到 LPR 配置部分的 DataTemplate(搜索 `LicensePlateRecognitionConfigViewModel`)
3. 添加海康威视字段组,使用 `IsVisible` 绑定控制显示:
   ```xml
   <!-- 海康威视专用字段 -->
   <StackPanel IsVisible="{Binding $parent[Window].ShowHikvisionLprFields}">
       <TextBlock Text="用户名:" />
       <TextBox Text="{Binding UserName}" Watermark="admin" />

       <TextBlock Text="密码:" />
       <TextBox Text="{Binding Password}" Watermark="请输入密码" />

       <TextBlock Text="端口:" />
       <TextBox Text="{Binding Port}" Watermark="8000" />

       <TextBlock Text="通道:" />
       <TextBox Text="{Binding Channel}" IsEnabled="False" />
   </StackPanel>
   ```
4. 确保 Channel 字段显示为只读(硬编码为 "1")

**Validation**:
- [x] 海康威视字段组已添加
- [x] `IsVisible` 绑定到 `ShowHikvisionLprFields`
- [x] Channel 字段显示为只读
- [x] UI 布局合理,字段对齐

**Output**: 更新的 `SettingsWindow.axaml`

---

### Task 2.8: 更新 AddLprDialog.axaml

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour
**Completed**: 2026-01-28

**Description**:
修改 `AddLprDialog.axaml`,添加海康威视配置字段。

**Steps**:
1. 打开 `MaterialClient/Views/Dialogs/AddLprDialog.axaml`
2. 找到现有的字段(Name, Ip, Direction)
3. 添加海康威视字段组:
   ```xml
   <!-- 海康威视专用字段 -->
   <StackPanel IsVisible="{Binding ShowHikvisionLprFields}">
       <TextBlock Text="用户名:" />
       <TextBox Text="{Binding UserName}" Watermark="admin" />

       <TextBlock Text="密码:" />
       <TextBox Text="{Binding Password}" Watermark="请输入密码" />

       <TextBlock Text="端口:" />
       <TextBox Text="{Binding Port}" Watermark="8000" />

       <TextBlock Text="通道:" />
       <TextBox Text="{Binding Channel}" IsEnabled="False" />
   </StackPanel>
   ```

**Validation**:
- [x] 海康威视字段组已添加
- [x] `IsVisible` 绑定到 `ShowHikvisionLprFields`
- [x] Channel 字段显示为只读
- [x] UI 布局合理

**Output**: 更新的 `AddLprDialog.axaml`

---

## Phase 3: 服务接口定义

### Task 3.1: 定义 IHikvisionLprService 接口

**Status**: Completed
**Priority**: Medium
**Estimated**: 45 minutes
**Completed**: 2026-01-28

**Description**:
创建 `IHikvisionLprService` 接口,定义海康威视 LPR 服务的核心方法。

**Steps**:
1. 创建文件 `MaterialClient.Common/Services/Hikvision/IHikvisionLprService.cs`
2. 定义接口:
   ```csharp
   namespace MaterialClient.Common.Services.Hikvision;

   /// <summary>
   /// 海康威视车牌识别服务接口
   /// </summary>
   public interface IHikvisionLprService
   {
       /// <summary>
       /// 连接到海康威视 LPR 设备
       /// </summary>
       Task<bool> ConnectAsync(LicensePlateRecognitionConfig config);

       /// <summary>
       /// 断开连接
       /// </summary>
       Task DisconnectAsync();

       /// <summary>
       /// 开始监听车牌识别事件
       /// </summary>
       Task StartListeningAsync();

       /// <summary>
       /// 停止监听
       /// </summary>
       Task StopListeningAsync();

       /// <summary>
       /// 车牌识别事件
       /// </summary>
       IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }
   }
   ```
3. 定义事件类型 `LicensePlateRecognizedEvent`(临时定义,后续提案完善)

**Validation**:
- [x] 接口文件已创建
- [x] 核心方法已定义
- [x] XML 注释完整
- [x] 代码编译通过

**Output**: 新建的 `IHikvisionLprService.cs`

**Note**: 本任务仅定义接口和方法签名,不包含具体实现。具体实现(包括 HCNetSDK 集成、ReactiveUI 事件流等)在后续提案中完成。

---

## Phase 4: 测试和验证

### Task 4.1: 编写配置模型单元测试

**Status**: Completed
**Priority**: Medium
**Estimated**: 1 hour
**Completed**: 2026-01-28

**Description**:
编写单元测试,验证 `LicensePlateRecognitionConfig` 海康威视字段的功能。

**Steps**:
1. 打开或创建测试文件(如 `MaterialClient.Common.Tests/Tests/LicensePlateRecognitionConfigTests.cs`)
2. 添加测试用例:
   - `Test_HikvisionFields_CanBeSetAndGet`: 验证字段可以设置和获取
   - `Test_HikvisionFields_DefaultToNull`: 验证字段默认值为 null
   - `Test_IsValid_OnlyRequiresNameAndIp`: 验证 `IsValid()` 不检查海康威视字段
3. 使用 xUnit 的 `[Fact]` 标记测试

**Validation**:
- [x] 测试文件已创建或更新
- [x] 所有测试用例通过
- [x] 测试覆盖率包含新增字段

**Output**: 配置模型单元测试

---

### Task 4.2: 编写 ViewModel 单元测试

**Status**: Pending
**Priority**: Medium
**Estimated**: 1.5 hours

**Description**:
编写单元测试,验证 `ShowHikvisionLprFields` 属性的动态切换逻辑。

**Steps**:
1. 创建测试文件 `MaterialClient.Common.Tests/Tests/SettingsWindowViewModelTests.cs`
2. 添加测试用例:
   - `Test_ShowHikvisionLprFields_WhenDeviceTypeIsHikvision_ReturnsTrue`
   - `Test_ShowHikvisionLprFields_WhenDeviceTypeIsLprAllInOne_ReturnsFalse`
   - `Test_ShowHikvisionLprFields_WhenDeviceTypeIsHuaxiazhixin_ReturnsFalse`
   - `Test_ShowHikvisionLprFields_UpdatesWhenDeviceTypeChanges`

**Validation**:
- [ ] 测试文件已创建
- [ ] 所有测试用例通过
- [ ] 测试覆盖所有设备类型

**Output**: ViewModel 单元测试

---

### Task 4.3: 编写 JSON 序列化兼容性测试

**Status**: Completed
**Priority**: High
**Estimated**: 1.5 hours
**Completed**: 2026-01-28

**Description**:
编写集成测试,验证 JSON 序列化/反序列化的兼容性,特别是旧数据的处理。

**Steps**:
1. 创建测试文件 `MaterialClient.Common.Tests/IntegrationTests/HikvisionLprConfigJsonTests.cs`
2. 添加测试用例:
   - `Test_Deserialize_OldJsonWithoutHikvisionFields`: 反序列化不包含新字段的旧 JSON,验证字段为 null
   - `Test_Serialize_NewConfigWithHikvisionFields`: 序列化包含海康威视字段的配置,验证 JSON 包含所有字段
   - `Test_RoundTrip_ConfigWithNullFields`: 序列化和反序列化 null 字段,验证数据完整性
   - `Test_LoadSettings_MixedOldAndNewConfigs`: 混合旧配置(无新字段)和新配置(有新字段)的加载
3. 使用 `System.Text.Json` 直接测试序列化逻辑

**Validation**:
- [x] 测试文件已创建
- [x] 所有测试用例通过
- [x] 旧数据兼容性验证通过

**Output**: JSON 序列化兼容性测试

**Note**: 这是新增的关键测试,确保旧配置数据不会因为新增字段而无法加载。

---

### Task 4.4: UI 手动测试

**Status**: Pending
**Priority**: High
**Estimated**: 1 hour

**Description**:
手动测试 SettingsWindow 和 AddLprDialog 的 UI 行为。

**Steps**:
1. 启动 MaterialClient 应用
2. 打开设置窗口(SettingsWindow)
3. 测试场景 1: `LprDeviceType = Hikvision`
   - 验证海康威示字段组可见
   - 添加新的 LPR 配置,填写海康威示字段
   - 保存配置
   - 重启应用,验证配置正确加载
4. 测试场景 2: `LprDeviceType = LprAllInOne`
   - 验证海康威示字段组隐藏
   - 添加新的 LPR 配置
   - 保存配置
5. 测试场景 3: 切换设备类型
   - 从 Hikvision 切换到 LprAllInOne,验证字段组隐藏
   - 从 LprAllInOne 切换到 Hikvision,验证字段组显示
6. 测试场景 4: 编辑现有配置
   - 编辑 Hikvision 配置,修改海康威示字段
   - 保存并重新加载,验证更改生效
7. 测试场景 5: 旧数据兼容性(重要)
   - 手动创建不包含海康威示字段的旧 JSON 配置
   - 加载旧配置,验证应用不崩溃
   - 验证新字段显示为 null 或默认值

**Validation**:
- [ ] 所有测试场景通过
- [ ] UI 响应及时,无卡顿
- [ ] 字段显示/隐藏逻辑正确
- [ ] 保存和加载功能正常
- [ ] 旧数据兼容性验证通过

**Output**: UI 测试报告(文档形式)

---

### Task 4.5: 代码审查和重构

**Status**: Pending
**Priority**: Medium
**Estimated**: 1 hour

**Description**:
审查所有代码更改,进行必要的重构和优化。

**Steps**:
1. 审查 `LicensePlateRecognitionConfig.cs`:
   - 检查 XML 注释是否完整
   - 考虑是否需要为海康威示字段添加验证方法
2. 审查 `SettingsWindowViewModel.cs`:
   - 检查代码组织是否清晰
   - 考虑是否可以提取 `ShowHikvisionLprFields` 逻辑到单独方法
3. 审查 `AddLprDialogViewModel.cs`:
   - 检查构造函数参数传递是否合理
4. 审查 XAML 文件:
   - 检查字段布局是否一致
   - 考虑是否可以使用用户控件(UserControl)减少重复
5. 审查 `IHikvisionLprService` 接口:
   - 检查方法签名是否合理
   - 确认接口职责清晰
6. 运行代码格式化工具(如 `dotnet format`)

**Validation**:
- [ ] 代码符合项目编码规范
- [ ] XML 注释完整
- [ ] 无代码重复或可优化的部分
- [ ] 代码格式统一

**Output**: 优化后的代码

---

### Task 4.6: 更新文档

**Status**: Pending
**Priority**: Low
**Estimated**: 30 minutes

**Description**:
更新相关文档,说明海康威示 LPR 配置功能。

**Steps**:
1. 检查是否有用户手册或开发者文档需要更新
2. 添加配置说明:
   - 海康威示设备所需的字段说明
   - 设备类型切换的影响
   - 旧数据兼容性说明
3. 更新 `docs/SDD.md`(如有必要):
   - 添加海康威示 LPR 配置的架构说明
   - 说明 JSON 存储方式和兼容性处理
4. 更新 `CLAUDE.md` 或 `openspec/project.md`(如有必要)

**Validation**:
- [ ] 文档已更新
- [ ] 配置说明清晰准确
- [ ] 文档格式一致

**Output**: 更新的文档

---

## Progress Tracking

**Phase 1 Progress**: 2/2 tasks completed (100%)
**Phase 2 Progress**: 6/6 tasks completed (100%)
**Phase 3 Progress**: 1/1 tasks completed (100%)
**Phase 4 Progress**: 2/5 tasks completed (40%)
**Overall Progress**: 11/16 tasks (69%)

---

## Notes

### 可并行执行的任务

以下任务可以并行执行以提高效率:
- Task 4.1、4.2 和 4.3 可以并行
- Task 2.1 和 2.3 可以并行
- Task 3.1 可以与 Phase 2 任务并行

### 依赖关系

- **Phase 2 依赖 Phase 1**: UI 实现需要配置模型先完成
- **Phase 4 依赖 Phase 1、2、3**: 测试需要代码实现完成
- Task 2.5 依赖 Task 2.3 (需要先定义 `ShowHikvisionLprFields` 属性)

### JSON 存储说明

**为什么不需要数据库迁移**:
- 配置数据存储在 `SettingsEntity.LicensePlateRecognitionConfigsJson` 字段中(NVARCHAR 类型)
- 新增字段不会改变表结构,只是改变 JSON 内容
- `System.Text.Json` 自动处理新增字段:
  - **反序列化**: 旧 JSON 缺少字段时,对应属性为 null
  - **序列化**: null 字段可选配置是否输出到 JSON
- **向后兼容**: 旧配置数据可以正常加载,新字段为 null

**旧数据兼容性处理**:
- `Channel` 字段在 UI 加载时使用 `?? "1"` 提供默认值
- 其他字段(UserName、Password、Port)为 null 时,UI 显示空字符串或占位符

### 未包含的任务

以下任务**不**在本提案范围内:
- 海康威示 LPR 监听服务的具体实现
- HCNetSDK LPR 组件的集成
- 车牌识别事件流的具体实现
- `IHikvisionLprService` 接口的实现类

这些任务需要单独的技术评审和提案。
