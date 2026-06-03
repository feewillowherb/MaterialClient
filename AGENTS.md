# Agent 行为准则

## OpenSpec 已迁移

本项目的 OpenSpec 规范驱动流程已迁移至 **MaterialMonospec** 主仓库统一管理。

- **OpenSpec 工作目录**：`MaterialMonospec/openspec/`
- **所有变更提案、设计、规范、任务** 均在主仓库的 `openspec/` 目录中创建和管理
- 本项目（MaterialClient）仅负责代码实现，不再维护独立的 openspec 目录
- 详见主仓库 `AGENTS.md` 中的「OpenSpec 生成位置约束」

## MaterialClient.Urban 上云与附件

- **服务端地址**：`appsettings.json` → `UrbanManagement:BaseUrl`（指向 UrbanManagement 站点，例如 `http://localhost:5000`）。
- **称重上云**：`PollingBackgroundService` → `IUrbanServerUploadService.SubmitRecordAsync`。
- **附件上云**：同一次 `SubmitRecordAsync` 内先调用 `POST /api/app/urban-attachment/upload`（按 `Lrp` / `UrbanPhoto` 分组 Base64），再将返回的 `attachmentIds` 填入 `Receive` 请求。
- **本地路径**：读取 `AttachmentFile.LocalPath` 前使用 `PathManager.ToAbsolutePath`（与主程序附件规范一致）。

## 项目约定（构建验证）

当 `bin/`、`obj/` 下 DLL 被正在运行的应用或测试宿主占用导致 MSB3027 复制失败时，使用**固定**独立输出目录验证编译，勿写入临时随机路径。

- **独立输出目录**：仓库根目录下的 `.build-verify/`（已列入 `.gitignore`，勿提交）
- **命令示例**（在 MaterialClient 仓库根执行）：
  ```bash
  dotnet build MaterialClient.sln -o .build-verify
  ```
  或针对单个项目：
  ```bash
  dotnet build src/MaterialClient.Urban/MaterialClient.Urban.csproj -o .build-verify
  ```
- **约定**：Agent 与本地脚本需要「避开文件锁仅做编译验证」时，一律使用 `-o .build-verify`，不要改用其他目录名。

## 项目约定（服务注册）

- **服务注册**：优先采用 **ABP 集成式 + 隐式 + AutoConstructor**。服务实现类实现 ABP 依赖接口（如 `ITransientDependency`、`ISingletonDependency`）并标注 `[AutoConstructor]`，由 ABP 按约定扫描注册，无需在 Module 或扩展方法中显式注册。参考实现：`SoundDeviceService`（实现 `ISoundDeviceService, ISingletonDependency` + `[AutoConstructor]`）。仅在确有"集成一组服务"的跨模块需求时再考虑扩展方法集中注册。

## 项目约定（应用关闭与资源清理）

本项目涉及大量非托管硬件资源（串口、SDK 句柄、GCHandle），应用关闭时极易导致超时或死锁。

- **SDK 回调禁止调度到 UI 线程**：非托管 SDK 回调中不得使用 `ObserveOn(RxApp.MainThreadScheduler)`。应用关闭时 UI 线程可能不可用，会导致 SDK Close 等待回调线程 → 回调线程等待 UI 线程的死锁。`MessageBus.Current.SendMessage` 本身线程安全，直接在回调线程调用即可。
- **非托管资源单例必须实现 IAsyncDisposable**：所有持有 SDK 句柄、串口、GCHandle 等非托管资源的 `ISingletonDependency` 必须实现 `IAsyncDisposable`，确保 ABP 容器自动清理。
- **SDK P/Invoke 调用必须加超时保护**：`VzLPRClient_Close`、`NET_DVR_StopListen_V30` 等同步 SDK 调用可能无限阻塞，必须用 `Task.Run` + `Task.WhenAny` 加超时。
- **退出顺序**：`App.OnApplicationExit` 必须先停 WebHost → 显式关闭硬件设备 → 再 ABP Shutdown，不能仅依赖 Autofac 自动释放（释放顺序不可控）。
- **StopAsync 超时为秒级**：服务关闭时等待 pending 操作的超时不得超过 5 秒。

## 编码规范（NON-NEGOTIABLE）

### .NET 10 & 现代 C# 语法

- **目标框架**：项目必须使用 .NET 10（`net10.0`）。
- **必须启用**：
  - `ImplicitUsings`：在项目文件中设置 `<ImplicitUsings>enable</ImplicitUsings>`。
  - `Nullable`：在项目文件中设置 `<Nullable>enable</Nullable>`。
- **优先使用最新语法糖**：
  - **File-scoped Namespaces**：优先使用 `namespace MaterialClient.Common;` 而非大括号命名空间。
  - **Collection Expressions（C# 12）**：使用 `[]` 替代 `new List<T>()` 或数组初始化。
  - **Primary Constructors（C# 12）**：简单类优先使用主构造函数。
  - **Using Alias for Types（C# 12）**：使用类型别名简化复杂类型引用。
  - **Record Types**：优先使用 `record` 替代传统类（DTO、值对象、不可变数据）。
  - **Pattern Matching**：充分利用模式匹配简化条件逻辑。
  - **Null-coalescing Operators**：优先使用 `??` 和 `??=`。
  - **Expression-bodied Members**：简单属性和方法使用表达式体。
  - **String Interpolation**：优先使用字符串插值而非字符串拼接。
  - **Async/Await**：异步操作必须使用 `async/await`，避免阻塞调用。
  - **IAsyncDisposable**：需要异步清理的资源实现 `IAsyncDisposable`。
- **源生成器优先**：优先使用 AutoConstructor、ReactiveUI.SourceGenerators 减少样板代码。
- **性能优化语法**：优先使用 `Span<T>`、`Memory<T>`、`ReadOnlySpan<T>`。
- **Struct 优先原则**：小的、不可变的值类型优先使用 `readonly struct`（通常 < 16-24 字节），避免堆分配和 GC 压力。需要多态或继承的场景仍用 `class`。
- **代码风格一致性**：所有新代码必须遵循上述规范，旧代码在重构时逐步迁移。

### 代码字符约束（NON-NEGOTIABLE）

- 变量名和字段必须是英文字符，禁止使用中文字符。
- 代码中除注释外不能出现中文字符。
- 遇到中文字符需转换为相应英文词汇。

### 命名约定（NON-NEGOTIABLE）

- 未知命名前缀如 `My` 应替换为项目名称 `MaterialClient`。
- 例如：`MyDbContext` → `MaterialClientDbContext`。
- 适用于所有类名、接口名、命名空间等标识符。

### 接口与实现文件组织约定（NON-NEGOTIABLE）

- CS 文件 < 1000 行时，Interface 可与 Impl 放在同一文件。
- 文件名为 Impl 名称（如 `AuthenticationService.cs` 包含 `IAuthenticationService` 和 `AuthenticationService`）。
- 文件内 Interface 应放在 Impl 前面。

### Record 替代 Tuple（NON-NEGOTIABLE）

- 与 MaterialMonospec 主仓库「跨子仓库 C# 编码约定」一致；本子仓库细则以下列条文为准。
- 禁止使用 tuple（如 `(string, int)` 或 `ValueTuple`）作为返回值或参数类型。
- 应使用 `record` 类型替代，例如 `record UserInfo(string Name, int Age)`。
- 适用于方法返回值、方法参数、局部变量和字段定义。

### 单一数据源（NON-NEGOTIABLE）

- 配置默认值、业务常量、枚举显示文本等必须在唯一位置定义（静态类、常量类、资源文件），禁止重复字面量或魔法值。
- UI 默认显示值必须与保存/持久化时使用的数据源一致。
- 新增或修改此类数据时，先确认是否已有唯一数据源；若无则新增，再统一引用。

### 数据绑定框架约束（NON-NEGOTIABLE）

- 所有数据绑定强制使用 ReactiveUI，不要使用 CommunityToolkit.Mvvm。

### ViewModel 间通信约定（NON-NEGOTIABLE）

- ViewModel 间通信必须使用 ReactiveUI `MessageBus`，禁止新增 `public event` 声明。
- Message 类型定义在 `MaterialClient.Common/Events/` 目录下，使用 `class` + primary constructor。
- 发布方使用 `MessageBus.Current.SendMessage(new XxxMessage(...))`。
- 订阅方使用 `MessageBus.Current.Listen<XxxMessage>().ObserveOn(RxApp.MainThreadScheduler).Subscribe(...).DisposeWith(_disposables)` 管理生命周期。
- View code-behind 中的 MessageBus 订阅同样使用 `CompositeDisposable` + `DisposeWith`，在 `OnClosed` 中统一 Dispose。

### ABP LocalEventBus 事件约定（跨层通信）

- 用于基础设施层与后台服务之间的跨层异步通信，不替代 ViewModel 间通信（ReactiveUI MessageBus）。
- ETO（Event Transfer Object）类型定义在 `MaterialClient.Common/Events/` 目录下，使用 `class` + primary constructor，命名以 `Eto` 结尾（如 `SessionRefreshRequiredEto`）。
- 发布方使用 `ILocalEventBus`（通过构造函数注入），fire-and-forget 模式：`_ = _localEventBus.PublishAsync(new XxxEto(...))`。
- 订阅方使用独立的 `ILocalEventHandler<XxxEto>` 实现类，标注 `[AutoConstructor]` + `ITransientDependency`，由 ABP 自动发现和注册。参考实现：`TryMatchEventHandler`。

### Token 失效自动刷新模式

- HTTP 管道层（`DelegatingHandler`）检测 401 Unauthorized 响应，通过 `ILocalEventBus` 发布 `SessionRefreshRequiredEto`（fire-and-forget）。
- 401 响应正常传播到调用方，不进行同步重试或异常吞没。
- 后台轮询服务（`AsyncPeriodicBackgroundWorkerBase`）订阅刷新事件，使用保存的凭证调用 `AuthenticationService.LoginAsync` 重新登录。
- 重新登录成功后，下次轮询周期自动使用新 token，无需人工干预。

### 实施计划语言（NON-NEGOTIABLE）

- 实施计划文档（如 plan.md、`.cursor/plans/*.plan.md`）必须使用英文撰写。

## 设计模式原则

### 意图揭示接口（Intention Revealing Interface）

- 接口和方法命名应清晰表达业务意图，避免技术性命名。
- 方法名反映业务操作而非实现细节，如 `CalculateTotalPrice()` 而非 `ProcessData()`。
- 优先使用领域语言（Ubiquitous Language）进行命名。

### 信息专家模式（Information Expert Pattern）

- 将职责分配给拥有完成该职责所需信息的类。
- 业务逻辑应放在最了解相关数据的对象中。
- 领域实体和值对象应封装与其数据相关的行为。
- 遵循"谁拥有数据，谁负责操作"的原则。

### 富模型设计（Rich Domain Model）

- 领域模型应包含业务逻辑和行为，而非仅仅是数据容器。
- 避免贫血模型（Anemic Domain Model），将业务逻辑从服务层移回领域层。
- 领域对象通过方法暴露业务操作，保持封装性和不变性约束。
- 复杂业务逻辑通过领域服务协调多个领域对象。

### 命令方法模式（Command Method Pattern）

- 方法命名使用命令式动词，如 `CreateOrder()`、`CancelOrder()`。
- 命令方法执行单一职责的业务操作。
- 查询方法使用查询式命名（如 `GetOrderById()`、`IsOrderValid()`），与命令方法区分。
- 可能失败的操作应明确表达失败原因（返回值、异常或结果对象）。

### 单一职责原则（NON-NEGOTIABLE）

- 任何代码都要有明确的职责，不能出现职责混乱。
- 每个类、方法、模块都应只有一个明确的职责。
- 职责边界应清晰，避免一个组件同时处理数据访问、业务逻辑、UI 渲染等不同层面的职责。

## MaterialClient.Urban 特殊约定

### 项目概述
- **项目**：`MaterialClient.Urban` — 城管专用单窗口桌面客户端
- **特点**：启动即进入唯一称重主界面，无登录/授权页面
- **配置**：`WeighingMode.UrbanMode = 201`，`ProductCode.Urban = 5030`
- **授权**：使用 `IStaticLicenseChecker` 后台检查（TODO: 当前默认返回成功）
- **UI 来源**：从 `MaterialClient.Demo/Views/WeighingSystemWindow.axaml` 迁移

### UI 主题（与 MaterialClient 一致）
- **NuGet**：`Semi.Avalonia`、`Irihi.Ursa`、`Irihi.Ursa.Themes.Semi`、`Irihi.Avalonia.Shared`（版本与主程序 Central Package Management 一致）
- **App.axaml**：须加载 `semi:SemiTheme` + `u-semi:SemiTheme`（`Locale="zh-CN"`），并包含与主程序相同的 Ursa `MessageBox` 样式及 `MaterialClient.UI` 的 `SharedTheme` / `SharedConverters`
- **禁止**仅引用 `Irihi.Ursa` 而不加载 Semi 主题（会导致 MessageBox 等 Ursa 控件尺寸/样式异常）

### 架构约束
- **无 Generic Host**：使用 Avalonia `ApplicationLifetime`，不注册 ABP 容器
- **无登录模块**：不引用 Account 模块，无 Session 管理
- **顶栏菜单**：仅保留"系统设置"，移除"退出登录"、"数据同步"、"项目信息"
- **静态授权**：`StaticLicenseChecker` 在 `App.OnFrameworkInitializationCompleted` 后台调用，仅记录日志

### Lrp 附件（UrbanMode 专用）
- **AttachType.Lrp = 5**：车牌识别图片附件类型
- **保存条件**：仅 `WeighingMode == UrbanMode (201)` 时保存
- **支持设备**：HikvisionLprService、VzvisionLprService
- **压缩质量**：`JpegCompressionUtil.LrpCompressionQuality = 90`
- **存储路径**：`Lrp/{plate}_{timestamp}.jpg`（相对于应用目录）

### Urban 称重记录扩展模式（Variant Extension Pattern）
- **扩展实体**：`MaterialClient.Common/Entities/Urban/UrbanWeighingExtension.cs`
- **关系**：`WeighingRecord` ← 1:0..1 → `UrbanWeighingExtension`（通过 FK `WeighingRecordId`）
- **用途**：存储 Urban 专用字段（`SyncStatus`、`RetryCount`、LastErrorTime`），与共享实体 `WeighingRecord` 解耦
- **约束**：`WeighingRecordId` 唯一索引 + `(SyncStatus, WeighingRecordId)` 复合索引
- **创建时机**：`WeighingRecordService.CreateWeighingRecordAsync()` 在 `WeighingMode == UrbanMode` 时同一事务内创建
- **查询模式**：Urban ViewModel 使用 `.Include(r => r.UrbanExtension)` LEFT JOIN 模式
- **XAML 绑定**：通过 `SyncStatusMatchConverter` 转换器访问 `UrbanExtension.SyncStatus`
- **扩展指引**：SolidWaste 等其他模式如需变体专属字段，参照此模式创建 `SolidWasteWeighingExtension` 实体

## 治理规则

- Constitution 优先于所有其他实践；修改需要文档、批准和迁移计划。
- 所有 PR/审查必须验证合规性；复杂性必须得到合理说明。
- 测试代码必须遵循与生产代码相同的代码字符约束和命名约定。
- 集成测试必须使用统一的测试基础设施，确保测试的一致性和可维护性。

## 错误样例文档

- **路径**：`docs/error-cases/`
- **用途**：记录代码中发现的设计缺陷、模式错误和潜在 bug，供 Agent 在审查和修改代码时参考。
- **命名**：以 `{module}-{method-or-area}.md` 格式命名（如 `standard-data-management-dialog-loaddata.md`）。
- **内容要求**：每个文件需包含错误位置、问题代码片段、问题分析、修复建议。
