# MaterialClient Constitution

## Core Principles

### I. Architecture-First
- 采用清晰的分层架构：UI（Avalonia）、应用服务、领域、基础设施（EF Core/SQLite、Refit 客户端）。
- 各层职责明确，禁止跨层依赖（除经 DTO/接口透传）。
- 使用 ABP 框架提供的基础设施（依赖注入、领域驱动设计、数据访问等）。

### II. ABP Framework Integration
- 统一使用 ABP 框架（版本 9.3.6）提供的核心功能。
- 依赖注入使用 Autofac（通过 ABP Autofac 模块）。
- 数据访问使用 ABP EntityFrameworkCore 集成。
- 领域驱动设计使用 ABP Domain 包。

### III. Test-First (NON-NEGOTIABLE)
- TDD 强制要求：测试先行编写 → 用户批准 → 测试失败 → 然后实现；严格遵循 Red-Green-Refactor 循环。
- 集成测试风格：采用 ABP 集成测试框架，使用内存 SQLite 进行数据库测试，支持事务隔离与数据种子。
- BDD 测试：使用 Reqnroll.NUnit 进行行为驱动开发，通过 `.feature` 文件和 `Steps.cs` 定义测试场景。
- **Feature Background 数据初始化（NON-NEGOTIABLE）**：
  - Feature 中的 Background 最好初始化当前 feature 需要用到的一些通用数据，如 Material、MaterialUnit、Provider 等环境数据。
  - 避免在业务测试中找不到对应的环境数据，确保测试场景的完整性和可重复性。

### IV. Integration Testing
- 集成测试重点领域：新库契约测试、契约变更、服务间通信、共享模式。
- 测试项目结构：所有测试统一在 `MaterialClient.Common.Tests` 项目中，包含 TestBase、EntityFrameworkCore、Domain 三个测试层次。
- 测试基础设施：基于 ABP TestBase 模块，提供统一的测试环境、配置和基类。
- **数据持久化操作封装（NON-NEGOTIABLE）**：
  - 集成测试中，所有涉及数据持久化的操作尽量封装到其对应的 DomainService 中。
  - 避免在测试步骤中直接操作仓储或 DbContext，通过领域服务进行数据操作，确保测试代码与生产代码的一致性。
  - 如果业务中没用到仅测试中使用到的接口，必须显式使用 `ITestService` 接口实现，表示只能在测试中使用该接口。

### V. Observability & Simplicity
- 可观测性：对关键路径（后台同步、HTTP 调用、数据库写入）记录结构化日志与指标，便于追踪与告警。
- 测试中使用 Serilog 进行日志记录，支持详细调试信息。
- 简单性原则：遵循 YAGNI（You Aren't Gonna Need It）原则，从简单开始，避免过度设计。

## Additional Constraints

- Windows 桌面客户端：采用 WPF 或 Avalonia，目标平台仅限 Windows（不要求跨平台）。
- 本地数据持久化：使用 SQLite 作为嵌入式数据库；若数据库文件不存在，首次启动时应自动创建。
- 后台任务：实现一个后台轮询作业，每 1 分钟批量读取待同步数据并通过 HTTP 客户端上报到 Web 服务端；需具备失败重试与基本可观测性（日志、计数）。
- **代码字符约束（NON-NEGOTIABLE）**：
  - 代码中变量名和字段必须是英文字符，禁止使用中文字符。
  - 代码中除了注释，不能出现中文字符。
  - 遇到中文字符，需要为其转换为相应的英文词汇。
- **命名约定（NON-NEGOTIABLE）**：
  - 当遇到未知命名前缀如`My`时，应将其替换为项目名称`MaterialClient`。
  - 例如：`MyDbContext` 应命名为 `MaterialClientDbContext`。
  - 此规则适用于所有类名、接口名、命名空间等标识符。
- **接口与实现文件组织约定（NON-NEGOTIABLE）**：
  - 如果一个 CS 文件代码行数小于 1000 行，Interface 可以和 Impl 放在同一个 CS 文件里。
  - 文件名为 Impl 的名称（例如：`AuthenticationService.cs` 包含 `IAuthenticationService` 接口和 `AuthenticationService` 实现类）。
  - 在文件里 Interface 应该放在 Impl 前面（先定义接口，后定义实现类）。
- **数据绑定框架约束（NON-NEGOTIABLE）**：
  - 所有数据绑定强制使用 ReactiveUI，不要使用 CommunityToolkit.Mvvm。

## Architecture & Technology Principles

### Technology Stack

- 依赖注入：统一使用 IoC 管理依赖，首选 Autofac；类构造函数依赖可使用 AutoConstructor 源生成器以减少样板代码。
- HTTP 客户端：统一使用 Refit 生成类型安全的 REST 客户端接口，与 `HttpClientFactory` 集成以获得连接复用与可配置的处理管线。
- 数据访问：
  - **ABP EntityFrameworkCore Sqlite 包**：必须引用 `Volo.Abp.EntityFrameworkCore.Sqlite` 包（当前版本：9.3.6），用于提供 SQLite 数据库的 ABP 集成支持。
  - **DbContext 基类**：`DbContext` 应继承自 `Volo.Abp.EntityFrameworkCore.AbpDbContext<TDbContext>`，以获得 ABP 的审计、多租户、软删除等特性支持。
  - **仓储模式**：使用 `Volo.Abp.Domain.Repositories.IRepository<TEntity, TKey>` 接口访问数据，避免直接使用 `DbContext`。通过 ABP 的依赖注入容器自动提供仓储实现。
  - **SQLite 配置**：
    - 使用 `AddAbpDbContext<TDbContext>(options => options.UseSqlite(...))` 进行配置；
    - 数据库文件路径应在应用配置中可配置，默认使用相对路径或用户数据目录；
    - 支持数据库加密（如 SQLCipher），连接字符串中应包含密码配置。
  - **集成测试**：
    - `DbContext` 与仓储均需支持 ABP 风格的集成测试（含内存替身/SQLite 模式、事务隔离与测试基类约定）。
    - 测试项目统一命名为 `MaterialClient.Common.Tests`，合并所有测试类型（Domain、EntityFrameworkCore、TestBase）。
    - 使用 ABP 集成测试框架（`AbpIntegratedTest<TStartupModule>`）作为测试基类。
    - 测试模块应继承自 `MaterialClientTestBaseModule`，提供统一的测试环境配置（禁用后台任务、允许所有授权、数据种子等）。
    - EntityFrameworkCore 测试使用内存 SQLite（`:memory:`），通过 `MaterialClientEntityFrameworkCoreTestModule` 配置。
    - Domain 测试使用 `MaterialClientDomainTestModule`，集成 Serilog 日志记录。
    - 使用 Reqnroll.NUnit 进行 BDD 风格测试，通过 `Steps.cs` 定义测试步骤。
    - 测试中使用 `FakeCurrentPrincipalAccessor` 模拟当前用户上下文。
    - 使用 `WithUnitOfWorkAsync` 方法进行工作单元测试，确保事务隔离。
- 领域驱动设计（DDD）与实体模型：
  - **ABP Domain 包**：必须引用 `Volo.Abp.Ddd.Domain` 包（当前版本：9.3.6），提供领域驱动设计基础设施。
  - **命名空间**：使用 `Volo.Abp.Domain.Entities` 命名空间下的基类。
  - **实体基类**：
    - 普通实体：继承 `Volo.Abp.Domain.Entities.Entity<TKey>`，提供主键（Id）和领域一致性约束；
    - 审计实体：继承 `Volo.Abp.Domain.Entities.Auditing.FullAuditedEntity<TKey>`，提供创建时间、修改时间、删除时间等审计字段；
    - 聚合根：继承 `Volo.Abp.Domain.Entities.Auditing.FullAuditedAggregateRoot<TKey>`，用于需要审计追踪的聚合根实体。
  - **领域服务**：业务逻辑应封装在领域服务中，使用 `Volo.Abp.Domain.Services.DomainService` 基类或实现 `IDomainService` 接口。
  - **领域事件**：使用 `Volo.Abp.Domain.Entities.Events.EntityChangedEventData<TEntity>` 或其派生类来发布领域事件。

### Architecture

- 分层约束：UI（WPF/Avalonia）、应用服务、领域、基础设施（EF Core/SQLite、Refit 客户端）清晰分层，禁止跨层依赖（除经 DTO/接口透传）。
- 后台同步：
  - 轮询协调器负责调度与节流；
  - 同步服务封装读库、状态标记与 Refit 调用；
  - 失败应记录并带指数退避重试，确保至多一次或至少一次语义按业务要求配置。
- 可测试性：
  - 领域与应用服务需具备可替换依赖以便于单元/集成测试；
  - Refit 与仓储接口在测试中可替换为内存实现或测试替身；
  - `DbContext` 测试支持迁移/种子与事务性回滚。
  - 测试基础设施：
    - 测试基类：`MaterialClientTestBase<TStartupModule>` 提供 ABP 集成测试基础功能。
    - EntityFrameworkCore 测试基类：`MaterialClientEntityFrameworkCoreTestBase` 用于数据库相关测试。
    - Domain 测试基类：`MaterialClientDomainTestBase<TStartupModule>` 用于领域层测试。
    - 测试配置文件：`appsettings.json` 和 `appsettings.secrets.json` 用于测试环境配置。
    - 测试依赖项：使用 NSubstitute 进行模拟、Shouldly 进行断言、Reqnroll.NUnit 进行 BDD 测试。
- 可观测性：对关键路径（后台同步、HTTP 调用、数据库写入）记录结构化日志与指标，便于追踪与告警。

### Design Patterns

- **意图揭示接口（Intention Revealing Interface）**：
  - 接口和方法命名应清晰表达其业务意图，避免使用技术性命名。
  - 方法名应反映业务操作而非实现细节，例如使用 `CalculateTotalPrice()` 而非 `ProcessData()`。
  - 接口设计应面向领域概念，使代码自文档化，降低理解成本。
  - 优先使用领域语言（Ubiquitous Language）进行命名，确保业务人员和技术人员对概念理解一致。

- **信息专家模式（Information Expert Pattern）**：
  - 将职责分配给拥有完成该职责所需信息的类。
  - 业务逻辑应放在最了解相关数据的对象中，减少不必要的依赖和耦合。
  - 领域实体和值对象应封装与其数据相关的行为，而非将逻辑外置到服务类。
  - 遵循"谁拥有数据，谁负责操作"的原则，提高内聚性和可维护性。

- **富模型设计（Rich Domain Model）**：
  - 领域模型应包含业务逻辑和行为，而不仅仅是数据容器。
  - 实体和值对象应封装业务规则、验证逻辑和领域行为。
  - 避免贫血模型（Anemic Domain Model），将业务逻辑从服务层移回领域层。
  - 领域对象应通过方法暴露业务操作，保持封装性和不变性约束。
  - 复杂业务逻辑应通过领域服务协调多个领域对象，而非在应用服务中实现。

- **命令方法模式（Command Method Pattern）**：
  - 方法命名应使用命令式动词，明确表达操作意图，例如 `CreateOrder()`、`CancelOrder()`、`ApproveRequest()`。
  - 命令方法应执行单一职责的业务操作，避免副作用和多重职责。
  - 查询方法应使用查询式命名（如 `GetOrderById()`、`IsOrderValid()`），与命令方法区分。
  - 命令方法应返回操作结果或领域对象，而非 void，以便调用方了解操作状态。
  - 对于可能失败的操作，应通过返回值、异常或结果对象明确表达失败原因。

## Governance

- Constitution 优先于所有其他实践；修改需要文档、批准和迁移计划。
- 所有 PR/审查必须验证合规性；复杂性必须得到合理说明。
- 测试代码必须遵循与生产代码相同的代码字符约束和命名约定。
- 集成测试必须使用统一的测试基础设施，确保测试的一致性和可维护性。

**Version**: 1.3.0 | **Ratified**: 2025-01-27 | **Last Amended**: 2025-01-31
