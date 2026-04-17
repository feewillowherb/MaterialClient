# Agent 行为准则

## OpenSpec 工作流（可选）

若采用规范驱动流程，可参考：

- **探索** (`/opsx:explore`)：理解问题与上下文
- **提案** (`/opsx:propose`)：产出 proposal、design、tasks、spec delta
- **实施** (`/opsx:apply`)：按 Spec/Design 实施代码
- **归档** (`/opsx:archive`)：变更完成后归档

```
openspec/
├── specs/          ← 功能规范
├── changes/        ← 待实施变更的规范化定义
└── project.md      ← 项目约定和技术栈
```

- 查看活跃变更：`openspec list`
- 查看功能规范：`openspec list --specs`
- 验证变更：`openspec validate [change-id] --strict`

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

### Cursor Plans 约定（NON-NEGOTIABLE）

- 计划文档统一存放在 `.cursor/plans/`，文件后缀必须为 `.plan.md`。
- 文件命名使用 kebab_case + 随机后缀，推荐格式：`<topic>_<8hex>.plan.md`（如 `add_weighingmode_filtering_c451b965.plan.md`）。
- 实施计划文档（如 `plan.md`、`.cursor/plans/*.plan.md`）必须使用英文撰写。
- 每个 plan 文件必须包含 YAML Front Matter，至少包含：
  - `name`
  - `overview`
  - `todos`（每项含 `id`、`content`、`status`）
- `todos.status` 仅允许：`pending`、`in_progress`、`completed`。
- 实施过程中需持续更新 `todos` 状态，确保任一时刻最多只有一个 `in_progress`。
- 代码实现、验收与回归时，应以对应 `.cursor/plans/*.plan.md` 作为执行与追踪依据。

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

## 治理规则

- Constitution 优先于所有其他实践；修改需要文档、批准和迁移计划。
- 所有 PR/审查必须验证合规性；复杂性必须得到合理说明。
- 测试代码必须遵循与生产代码相同的代码字符约束和命名约定。
- 集成测试必须使用统一的测试基础设施，确保测试的一致性和可维护性。
