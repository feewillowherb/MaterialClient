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

## Architecture & Technology Principles

### Technology Stack

- 依赖注入：统一使用 IoC 管理依赖，首选 Autofac；类构造函数依赖可使用 AutoConstructor 源生成器以减少样板代码。
- HTTP 客户端：统一使用 Refit 生成类型安全的 REST 客户端接口，与 `HttpClientFactory` 集成以获得连接复用与可配置的处理管线。
- 数据访问：
  - 使用 ABP 的 EntityFrameworkCore 集成提供 `DbContext` 与仓储（Repository）模式；
  - `DbContext` 与仓储均需支持 ABP 风格的集成测试（含内存替身/SQLite 模式、事务隔离与测试基类约定）。
- 实体模型：业务实体应继承 ABP 提供的基类（如 `Entity`、`RootAuditEntity`/审计根聚合类型），以获得主键、审计字段与领域一致性约束。

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
