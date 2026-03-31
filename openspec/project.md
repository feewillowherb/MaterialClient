# Project Context

## 目的

MaterialClient 是一个用于工业环境材料称重管理的 Windows 桌面应用程序。该系统提供有人值守和无人值守的称重操作，与包括地磅、车牌识别摄像头和安防摄像头在内的硬件设备集成。它管理称重记录，自动匹配入站/出站称重以生成运单，并与远程平台同步数据。

## 技术栈

- **Language**: C# 13 / .NET 10.0
- **Framework**: Avalonia UI 11.3.9（跨平台桌面 UI 框架）
- **Architecture**: MVVM pattern with ReactiveUI 20.1.1
- **Database**: SQLite with Entity Framework Core 10.0.1
- **Dependency Injection**: Volo.Abp Autofac 10.0.1
- **Reactive Extensions**: System.Reactive 7.0.0-preview.1 (Rx.NET)
- **HTTP Client**: Refit 9.0.2 with Polly resilience policies
- **Logging**: Serilog 4.3.0
- **Hardware Integration**:
  - Serial port communication (System.IO.Ports)
  - Camera capture (FlashCap 1.11.0)
  - Hikvision SDK integration (HCNetSDK)
  - License plate recognition (LPRAllInOne)
- **Cloud Storage**: Aliyun OSS SDK 2.14.1
- **ID Generation**: Yitter.IdGenerator 1.0.14 (Snowflake algorithm)

## Architecture Principles

### Core Architecture

- 采用清晰的分层架构：UI（Avalonia）、应用服务、领域、基础设施（EF Core/SQLite、Refit 客户端）。
- 各层职责明确，禁止跨层依赖（除经 DTO/接口透传）。
- 使用 ABP 框架提供的基础设施（依赖注入、领域驱动设计、数据访问等）。

### ABP Framework Integration

- 统一使用 ABP 框架（版本 10.0.1）提供的核心功能。
- 依赖注入使用 Autofac（通过 ABP Autofac 模块）。
- 数据访问使用 ABP EntityFrameworkCore 集成。
- 领域驱动设计使用 ABP Domain 包。

### Dependency Injection

- 统一使用 IoC 管理依赖，首选 Autofac。
- 类构造函数依赖可使用 AutoConstructor 源生成器以减少样板代码。

### HTTP Client

- 统一使用 Refit 生成类型安全的 REST 客户端接口，与 `HttpClientFactory` 集成以获得连接复用与可配置的处理管线。

### Data Access

- **ABP EntityFrameworkCore Sqlite 包**：必须引用 `Volo.Abp.EntityFrameworkCore.Sqlite` 包（版本 10.0.1）。
- **DbContext 基类**：继承自 `Volo.Abp.EntityFrameworkCore.AbpDbContext<TDbContext>`，获得审计、多租户、软删除等特性。
- **仓储模式**：使用 `Volo.Abp.Domain.Repositories.IRepository<TEntity, TKey>` 接口访问数据，避免直接使用 `DbContext`。
- **SQLite 配置**：
  - 使用 `AddAbpDbContext<TDbContext>(options => options.UseSqlite(...))` 进行配置。
  - 数据库文件路径应在应用配置中可配置。
  - 支持数据库加密（如 SQLCipher）。

### Domain-Driven Design (DDD) & Entity Model

- **ABP Domain 包**：必须引用 `Volo.Abp.Ddd.Domain` 包（版本 10.0.1）。
- **实体基类**：
  - 普通实体：继承 `Volo.Abp.Domain.Entities.Entity<TKey>`。
  - 审计实体：继承 `Volo.Abp.Domain.Entities.Auditing.FullAuditedEntity<TKey>`。
  - 聚合根：继承 `Volo.Abp.Domain.Entities.Auditing.FullAuditedAggregateRoot<TKey>`。
- **领域服务**：业务逻辑封装在领域服务中，使用 `Volo.Abp.Domain.Services.DomainService` 基类或实现 `IDomainService` 接口。
- **领域事件**：使用 `Volo.Abp.Domain.Entities.Events.EntityChangedEventData<TEntity>` 或其派生类发布领域事件。

### Background Sync

- 轮询协调器负责调度与节流。
- 同步服务封装读库、状态标记与 Refit 调用。
- 失败应记录并带指数退避重试，确保至多一次或至少一次语义按业务要求配置。

### Observability

- 对关键路径（后台同步、HTTP 调用、数据库写入）记录结构化日志与指标，便于追踪与告警。
- 测试中使用 Serilog 进行日志记录。
- 遵循 YAGNI（You Aren't Gonna Need It）原则，从简单开始，避免过度设计。

## Project Conventions

### 代码风格

- **Nullable Reference Types**: 在整个代码库中启用
- **Implicit Usings**: 为 .NET 10.0 启用
- **Source Generators**:
  - AutoConstructor 5.6.0 用于自动构造函数注入
  - ReactiveUI.SourceGenerators 2.5.1 用于 ReactiveUI 样板代码
- **Naming Conventions**:
  - Async methods end with `Async` suffix
  - Private fields use `_camelCase` notation
  - Internal classes use `InternalsVisibleTo` for test visibility
- **File Organization**:
  - Views: `*.axaml` and `*.axaml.cs` in `Views/` directory
  - ViewModels: Co-located with views or in appropriate feature folders
  - Services: `MaterialClient.Common/Services/`
  - Entities: `MaterialClient.Common/Entities/`
  - DTOs: `MaterialClient.Common/Api/Dtos/`
  - Static Factory Methods: `MaterialClient.Common/Utils/` (e.g., `DatabaseConnectionStringFactory`)
  - Dependency Injection Factory Services: `MaterialClient.Common/Providers/` (e.g., `RecommendPlateNumberService`)

### 构建配置

- **Directory.Build.props**: 应用于所有项目的通用构建设置和包引用
  - AutoConstructor 通过此文件自动对所有项目可用
  - 位于解决方案根目录，由 MSBuild 自动导入
- **Directory.Packages.props**: 用于版本控制的 Central Package Management (CPM)
  - 所有包版本都在此单一文件中定义
  - 项目引用包时不带版本号（版本来自 Directory.Packages.props）
  - 确保所有项目的版本一致性
  - 需要 .NET SDK 6.0+（项目使用 .NET SDK 10.0）

### 架构模式

- **MVVM Pattern**: 使用 Avalonia ReactiveUI 的 View-ViewModel 分离
- **Repository Pattern**: 使用 Volo.Abp 的 `IRepository<TEntity, TKey>` 进行数据访问
- **Unit of Work**: 使用 `IUnitOfWorkManager` 进行事务管理
- **Service Layer**: 服务类中的业务逻辑（例如 `AttendedWeighingService`、`MaterialService`）
- **Rx State Management**: 使用 BehaviorSubject 和 Reactive Extensions 进行状态流管理
- **Hardware Abstraction**: 硬件设备的服务接口（`ITruckScaleWeightService`、`ILPRAllInOneService`、`IHikvisionService`）
- **API Integration**: 使用 Refit 接口与远程平台进行 HTTP 通信

**当前状态管理模式**:
`AttendedWeighingService` 使用受 RxState 启发的模式，具有：
- 统一状态对象（`WeighingServiceState`）
- 纯函数 reducers（`WeighingServiceStateReducer`）
- 副作用分离（reducer 外部的异步操作）
- 基于操作的状态突变

**重要 - 内存泄漏考虑因素**:
- 正确的订阅处置至关重要 - 始终处置订阅
- 避免 Rx 流中的循环引用
- 对热可观察对象使用 `RefCount()`
- 对于待处理操作，优先使用 `ConcurrentQueue` 而不是 `ConcurrentBag`
- 为 `Buffer()` 和 `Replay()` 操作符添加大小限制

### 测试策略

- **Test-First（NON-NEGOTIABLE）**: TDD 强制要求：测试先行编写 → 用户批准 → 测试失败 → 然后实现；严格遵循 Red-Green-Refactor 循环。
- **集成测试风格**：采用 ABP 集成测试框架，使用内存 SQLite 进行数据库测试，支持事务隔离与数据种子。
- **BDD 测试**：使用 Reqnroll.NUnit 进行行为驱动开发，通过 `.feature` 文件和 `Steps.cs` 定义测试场景。
- **Feature Background 数据初始化（NON-NEGOTIABLE）**：
  - Feature 中的 Background 最好初始化当前 feature 需要用到的一些通用数据，如 Material、MaterialUnit、Provider 等环境数据。
  - 避免在业务测试中找不到对应的环境数据。

- **Test Framework**: xUnit（由 `.Tests` 项目结构暗示）
- **Test Categories**:
  - 业务逻辑和 reducers 的单元测试
  - 数据库操作的集成测试
  - 长时间运行服务的内存泄漏测试
  - 用于在没有物理设备的情况下进行测试的硬件 mock 实现
- **Test Visibility**: `InternalsVisibleTo` attribute for testing internal members
- **Key Test Suites**:
  - `AttendedWeighingServiceMemoryLeakTests` - 验证正确的资源清理
  - 所有硬件服务的 mock 实现

#### Integration Test Infrastructure

- **测试项目结构**：所有测试统一在 `MaterialClient.Common.Tests` 项目中，包含 TestBase、EntityFrameworkCore、Domain 三个测试层次。
- **测试基础设施**：基于 ABP TestBase 模块，提供统一的测试环境、配置和基类。
- **数据持久化操作封装（NON-NEGOTIABLE）**：
  - 集成测试中，所有涉及数据持久化的操作尽量封装到其对应的 DomainService 中。
  - 避免在测试步骤中直接操作仓储或 DbContext，通过领域服务进行数据操作。
  - 如果业务中没用到仅测试中使用到的接口，必须显式使用 `ITestService` 接口实现。
- **测试基类**：
  - `MaterialClientTestBase<TStartupModule>` 提供 ABP 集成测试基础功能。
  - `MaterialClientEntityFrameworkCoreTestBase` 用于数据库相关测试。
  - `MaterialClientDomainTestBase<TStartupModule>` 用于领域层测试。
- **测试模块**：
  - 测试模块继承自 `MaterialClientTestBaseModule`，提供统一的测试环境配置（禁用后台任务、允许所有授权、数据种子等）。
  - EntityFrameworkCore 测试使用内存 SQLite（`:memory:`），通过 `MaterialClientEntityFrameworkCoreTestModule` 配置。
  - Domain 测试使用 `MaterialClientDomainTestModule`，集成 Serilog 日志记录。
- **测试工具**：
  - 使用 NSubstitute 进行模拟、Shouldly 进行断言。
  - 使用 `FakeCurrentPrincipalAccessor` 模拟当前用户上下文。
  - 使用 `WithUnitOfWorkAsync` 方法进行工作单元测试，确保事务隔离。
- **测试配置文件**：`appsettings.json` 和 `appsettings.secrets.json` 用于测试环境配置。

#### Integration Test Conventions

- **Test DTO Naming**: All test data transfer objects (records) used in integration tests MUST use the `TestDto` suffix (e.g., `WeighingRecordTestDto`, `WaybillVerifyTestDto`)
- **Step Definition Style**: Integration test step definitions SHOULD prefer table-based data setup over individual parameter-based steps for better readability and maintainability
  - Use `Given [Entity] as below` with Reqnroll `Table` parameter for data setup
  - Use `Then [Entity] as below` with Reqnroll `Table` parameter for verification
  - Individual parameter-based steps are acceptable for simple cases or backward compatibility

### Git Workflow

- **Main Branch**: `main` or `v2` (recent merge from main to v2 recommended)
- **Feature Branches**: Descriptive names (e.g., `feat/weighing-service-v2`, `fix/ui-1366`)
- **Commit Conventions**:
  - `Feat/` - New features
  - `Fix/` - Bug fixes
  - Test-related commits clearly marked
- **Code Review**: Pull requests required for merging

### OpenSpec Workflow

本项目使用 OpenSpec 进行规范驱动的开发：

1. **在进行更改之前**:
   - 阅读 `openspec/project.md`（本文件）
   - 运行 `openspec list` 查看活跃的更改
   - 运行 `openspec list --specs` 查看现有功能
   - 检查 `openspec/specs/[capability]/spec.md` 中的需求

2. **创建提案**:
   - 适用于：新功能、破坏性更改、架构更改、性能优化
   - 不适用于：Bug 修复、拼写错误、格式、非破坏性依赖更新
   - 使用动词引导的变更 ID：`add-*`、`update-*`、`remove-*`、`refactor-*`

3. **提案结构**:
   - `openspec/changes/[change-id]/proposal.md` - 原因、内容、影响
   - `openspec/changes/[change-id]/tasks.md` - 实施检查清单
   - `openspec/changes/[change-id]/design.md` - 技术决策（可选）
   - `openspec/changes/[change-id]/specs/[capability]/spec.md` - Delta 需求

4. **Delta 操作**:
   - `## ADDED Requirements` - 新功能
   - `## MODIFIED Requirements` - 更改的行为（粘贴完整更新的需求）
   - `## REMOVED Requirements` - 已弃用的功能
   - 对 scenario 使用 `#### Scenario:` 格式（4 个井号）

5. **验证**:
   - 在请求批准之前运行 `openspec validate [change-id] --strict`
   - 确保每个需求至少有一个 scenario
   - 在提案获得批准之前不要开始实施

See `AGENTS.md`（项目根目录）for agent behavior rules and OpenSpec workflow documentation.

## Domain Context

### Core Entities

- **WeighingRecord**: Represents a single weighing operation (毛重/Gross weight)
  - Fields: PlateNumber, Weight, DeliveryType (Receiving/Shipping), WeighingRecordType (Unmatch/Join/Out), Timestamp
  - Attachments: Vehicle photos, document photos via `WeighingRecordAttachment`

- **Waybill**: Generated from matched inbound/outbound weighing records
  - Fields: OrderNo (GUID), Provider, Material, JoinRecordId, OutRecordId
  - Auto-matched based on plate number, time window, and weight validation

- **Material & Provider**: Material types and suppliers (synchronized from remote platform)

- **WeighingServiceState**: Unified state for attended weighing service
  - Status: OffScale, OnScale, WeighingComplete
  - Current weight, delivery type, last record ID
  - Action-based state mutations (SetDeliveryTypeAction, WeighingRecordCreatedAction)

### Business Logic

**Attended Weighing Flow**:
1. Vehicle approaches scale → Weight exceeds offset → Status: OnScale
2. Weight stabilizes for threshold duration → Status: WeighingComplete
3. System automatically:
   - Creates `WeighingRecord`
   - Captures license plate via LPR camera
   - Takes 4 vehicle photos via USB/Hikvision camera
   - Records weight and timestamp
4. Vehicle leaves → Weight returns to offset range → Status: OffScale

**Automatic Matching**:
- Match Join (inbound) and Out (outbound) records by:
  1. Same plate number
  2. Created within time window
  3. For Receiving: Join weight > Out weight
  4. For Shipping: Join weight < Out weight
- If multiple pairs match, select shortest time interval
- Auto-generate `Waybill` and update record types

**Hardware Integration**:
- Truck scale via serial port (continuous weight monitoring)
- License plate recognition via specialized camera
- Vehicle photos via USB camera or Hikvision security camera
- Sound broadcast for audio announcements

### Remote Integration

- **Authentication**: License-based authentication with remote platform
- **Synchronization**: Upload weighing records and waybills
- **Master Data**: Download materials, providers, goods types from platform
- **Sound Devices**: Remote control of broadcast devices

## Important Constraints

### Platform Constraints

- **Target Platform**: Windows x64 only (due to HCNetSDK native dependencies)
- **Runtime**: .NET 10.0 desktop runtime required
- **Deployment**: Single-file executable with self-contained deployment
- **HCNetSDK**: Native DLLs must be distributed with application (HCNetSDK/, HCNetSDKCom/)
- **Windows 桌面客户端**：采用 Avalonia，目标平台仅限 Windows（不要求跨平台）。

### Performance Constraints

- **Long-Running Process**: Application designed for 24/7 operation
- **Memory Management**: Critical - must avoid memory leaks in Rx subscriptions
- **Real-Time Weight Monitoring**: High-frequency weight stream processing
- **Stability Detection**: Configurable time window and threshold for weight stability

### Hardware Constraints

- **Serial Port Exclusivity**: Only one process can access serial port at a time
- **Camera Resource Limits**: USB cameras have bandwidth limitations
- **Network Dependency**: LPR and Hikvision services require network connectivity
- **Device Compatibility**: Hardware-specific SDKs (HCNetSDK for Hikvision cameras)

### Data Constraints

- **Local Data Persistence**: 使用 SQLite 作为嵌入式数据库；若数据库文件不存在，首次启动时应自动创建。
- **SQLite Limits**: Suitable for single-user desktop application, not concurrent multi-user
- **Attachment Storage**: Photos stored locally or uploaded to Aliyun OSS
- **ID Generation**: Snowflake IDs require unique worker ID per instance

### Code Organization Constraints

- **Factory Method Pattern (MANDATORY)**: Configuration-unrelated logic (e.g., path resolution, resource creation) MUST be implemented in factory methods, NOT in business code or configuration initialization code
- **Static Factory Methods**: Place in `MaterialClient.Common/Utils/` directory (e.g., `DatabaseConnectionStringFactory.FixConnectionString`)
- **Dependency Injection Factory Services**: Place in `MaterialClient.Common/Providers/` directory (e.g., `RecommendPlateNumberService`)
- **Separation of Concerns**: Business code and configuration initialization code should ONLY call factory methods, not implement path resolution or resource creation logic directly

## External Dependencies

### Hardware Services

- **Truck Scale**: Serial port communication (configurable port, baud rate)
- **LPR Camera**: Network-based license plate recognition service
- **Hikvision Camera**: IP camera with RTSP streaming and SDK integration
- **USB Camera**: DirectShow/USB camera for vehicle photos

### Remote Platform APIs

- **Authentication**: License validation, user login
- **Material Data**: CRUD operations for materials, providers, goods types
- **Synchronization**: Upload weighing records and waybills
- **Sound Devices**: Remote broadcast control

### Cloud Services

- **Aliyun OSS**: Optional cloud storage for photo attachments
- **CDN**: Optional CDN for distributed photo delivery

### Configuration Files

- **appsettings.json**: Application configuration (non-sensitive)
- **appsettings.secret.json**: Sensitive configuration (connection strings, API keys)
- **User Secrets**: Development-time configuration (ID: MaterialClient-UserSecrets)

## Project Structure

```
MaterialClient/
├── MaterialClient/                    # Main Avalonia UI application
│   ├── Views/                        # AXAML views
│   ├── ViewModels/                   # ViewModels
│   └── appsettings.json              # Configuration
├── MaterialClient.Common/            # Core business logic and services
│   ├── Api/                          # Refit interfaces and DTOs
│   ├── Entities/                     # Domain entities
│   ├── EntityFrameworkCore/          # Database context and migrations
│   ├── Services/                     # Business services
│   │   ├── AttendedWeighingService.cs
│   │   ├── Hardware/                 # Hardware service implementations
│   │   ├── Hikvision/                # Camera integration
│   │   └── LPRAllInOne/              # License plate recognition
│   ├── Utils/                        # Static factory methods and utilities
│   │   ├── DatabaseConnectionStringFactory.cs
│   │   └── AttachmentPathUtils.cs
│   ├── Providers/                    # Dependency injection factory services
│   │   ├── RecommendPlateNumberService.cs
│   │   └── PlateNumberValidator.cs
│   └── MaterialClient.Common.csproj  # Dependencies
├── MaterialClient.Common.Tests/      # Unit and integration tests
├── MaterialClientToolkit/            # Utility tools
├── openspec/                         # Specifications and change proposals
│   ├── specs/                        # Current capabilities (truth)
│   ├── changes/                      # Proposed changes
│   └── archive/                      # Completed changes
├── docs/                             # Documentation and analysis reports
└── MaterialClient.sln                # Solution file
```

## Development Guidelines

### When Adding Features

1. Check `openspec list --specs` for existing capabilities
2. Create OpenSpec proposal for non-trivial changes
3. Follow MVVM pattern for UI features
4. Add services to `MaterialClient.Common` for business logic
5. Use dependency injection for service composition
6. Write tests before or alongside implementation
7. Ensure proper disposal of Rx subscriptions
8. **Factory Method Pattern**: If implementing configuration-unrelated logic (path resolution, resource creation), create factory methods:
   - Static factories → `MaterialClient.Common/Utils/`
   - DI factories → `MaterialClient.Common/Providers/`
   - Do NOT implement such logic directly in business code or configuration initialization

### When Fixing Bugs

1. Write reproducing test first
2. Fix bug without breaking existing tests
3. Add regression test if applicable
4. Update documentation if behavior changes
5. No OpenSpec proposal needed for bug fixes

### When Optimizing Performance

1. Profile with dotTrace, dotMemory, or Visual Studio Profiler
2. Create OpenSpec proposal for significant optimizations
3. Add benchmarks before and after optimization
4. Document optimization strategy in code comments or docs/
5. Test with realistic data volumes

### When Working with Rx

1. Always dispose subscriptions (use `DisposeWith()` or `using` blocks)
2. Avoid circular references in stream chains
3. Use `RefCount()` for shared hot observables
4. Add size limits to `Buffer()` and `Replay()`
5. Prefer `ConcurrentQueue` over `ConcurrentBag` for pending operations
6. Test memory leaks explicitly with long-running tests

### When Integrating Hardware

1. Create interface abstraction (`IService`)
2. Provide mock implementation for testing
3. Handle hardware disconnection gracefully
4. Add retry and resilience policies
5. Log hardware operations for debugging
6. Test with real hardware when possible

## Related Documentation

- `AGENTS.md` - Agent 行为准则和 OpenSpec 工作流规范
- `.specify/memory/constitution.md` - 项目宪章（核心原则与强制约束的完整定义）
- `openspec/PROPOSAL_DESIGN_GUIDELINES.md` - UI mockup and diagram guidelines
- `docs/AttendedWeighingService-RxState-Optimization-Report.md` - State management architecture
- `docs/AttendedWeighingService-MemoryLeak-Testing-Guide.md` - Memory leak testing
- `specs/001-attended-weighing/spec.md` - Detailed attended weighing specification
