# [PROJECT_NAME] Constitution
<!-- Example: Spec Constitution, TaskFlow Constitution, etc. -->

## Core Principles

### [PRINCIPLE_1_NAME]
<!-- Example: I. Library-First -->
[PRINCIPLE_1_DESCRIPTION]
<!-- Example: Every feature starts as a standalone library; Libraries must be self-contained, independently testable, documented; Clear purpose required - no organizational-only libraries -->

### [PRINCIPLE_2_NAME]
<!-- Example: II. CLI Interface -->
[PRINCIPLE_2_DESCRIPTION]
<!-- Example: Every library exposes functionality via CLI; Text in/out protocol: stdin/args → stdout, errors → stderr; Support JSON + human-readable formats -->

### [PRINCIPLE_3_NAME]
<!-- Example: III. Test-First (NON-NEGOTIABLE) -->
[PRINCIPLE_3_DESCRIPTION]
<!-- Example: TDD mandatory: Tests written → User approved → Tests fail → Then implement; Red-Green-Refactor cycle strictly enforced -->

### [PRINCIPLE_4_NAME]
<!-- Example: IV. Integration Testing -->
[PRINCIPLE_4_DESCRIPTION]
<!-- Example: Focus areas requiring integration tests: New library contract tests, Contract changes, Inter-service communication, Shared schemas -->

### [PRINCIPLE_5_NAME]
<!-- Example: V. Observability, VI. Versioning & Breaking Changes, VII. Simplicity -->
[PRINCIPLE_5_DESCRIPTION]
<!-- Example: Text I/O ensures debuggability; Structured logging required; Or: MAJOR.MINOR.BUILD format; Or: Start simple, YAGNI principles -->

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
  - **集成测试**：`DbContext` 与仓储均需支持 ABP 风格的集成测试（含内存替身/SQLite 模式、事务隔离与测试基类约定）。
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
- 可观测性：对关键路径（后台同步、HTTP 调用、数据库写入）记录结构化日志与指标，便于追踪与告警。

## Governance
<!-- Example: Constitution supersedes all other practices; Amendments require documentation, approval, migration plan -->

[GOVERNANCE_RULES]
<!-- Example: All PRs/reviews must verify compliance; Complexity must be justified; Use [GUIDANCE_FILE] for runtime development guidance -->

**Version**: [CONSTITUTION_VERSION] | **Ratified**: [RATIFICATION_DATE] | **Last Amended**: [LAST_AMENDED_DATE]
<!-- Example: Version: 2.1.1 | Ratified: 2025-06-13 | Last Amended: 2025-07-16 -->
