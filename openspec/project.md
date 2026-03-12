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
- `openspec/PROPOSAL_DESIGN_GUIDELINES.md` - UI mockup and diagram guidelines
- `docs/AttendedWeighingService-RxState-Optimization-Report.md` - State management architecture
- `docs/AttendedWeighingService-MemoryLeak-Testing-Guide.md` - Memory leak testing
- `specs/001-attended-weighing/spec.md` - Detailed attended weighing specification
