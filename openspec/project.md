# Project Context

## Purpose

MaterialClient is a Windows desktop application for material weighing management in industrial settings. The system provides attended (有人值守) and unattended weighing operations, integrating with hardware devices including truck scales, license plate recognition cameras, and security cameras. It manages weighing records, automatically matches inbound/outbound weighings to generate waybills, and synchronizes data with a remote platform.

## Tech Stack

- **Language**: C# 13 / .NET 10.0
- **Framework**: Avalonia UI 11.3.9 (Cross-platform desktop UI framework)
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

### Code Style

- **Nullable Reference Types**: Enabled throughout the codebase
- **Implicit Usings**: Enabled for .NET 10.0
- **Source Generators**:
  - AutoConstructor 5.6.0 for automatic constructor injection
  - ReactiveUI.SourceGenerators 2.5.1 for ReactiveUI boilerplate
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

### Architecture Patterns

- **MVVM Pattern**: View-ViewModel separation using Avalonia ReactiveUI
- **Repository Pattern**: `IRepository<TEntity, TKey>` from Volo.Abp for data access
- **Unit of Work**: `IUnitOfWorkManager` for transaction management
- **Service Layer**: Business logic in service classes (e.g., `AttendedWeighingService`, `MaterialService`)
- **Rx State Management**: Using BehaviorSubject and Reactive Extensions for state streams
- **Hardware Abstraction**: Service interfaces for hardware devices (`ITruckScaleWeightService`, `ILPRAllInOneService`, `IHikvisionService`)
- **API Integration**: Refit interfaces for HTTP communication with remote platform

**Current State Management Pattern**:
The `AttendedWeighingService` uses an RxState-inspired pattern with:
- Unified state object (`WeighingServiceState`)
- Pure function reducers (`WeighingServiceStateReducer`)
- Side-effect separation (async operations outside reducer)
- Action-based state mutations

**Important - Memory Leak Considerations**:
- Proper subscription disposal is critical - always dispose subscriptions
- Avoid circular references in Rx streams
- Use `RefCount()` for hot observables
- Prefer `ConcurrentQueue` over `ConcurrentBag` for pending operations
- Add size limits to `Buffer()` and `Replay()` operators

### Testing Strategy

- **Test Framework**: xUnit (implied by `.Tests` project structure)
- **Test Categories**:
  - Unit tests for business logic and reducers
  - Integration tests for database operations
  - Memory leak tests for long-running services
  - Hardware mock implementations for testing without physical devices
- **Test Visibility**: `InternalsVisibleTo` attribute for testing internal members
- **Key Test Suites**:
  - `AttendedWeighingServiceMemoryLeakTests` - Verifies proper resource cleanup
  - Mock implementations for all hardware services

### Git Workflow

- **Main Branch**: `main` or `v2` (recent merge from main to v2 recommended)
- **Feature Branches**: Descriptive names (e.g., `feat/weighing-service-v2`, `fix/ui-1366`)
- **Commit Conventions**:
  - `Feat/` - New features
  - `Fix/` - Bug fixes
  - Test-related commits clearly marked
- **Code Review**: Pull requests required for merging

### OpenSpec Workflow

This project uses OpenSpec for specification-driven development:

1. **Before Making Changes**:
   - Read `openspec/project.md` (this file)
   - Run `openspec list` to see active changes
   - Run `openspec list --specs` to see existing capabilities
   - Check `openspec/specs/[capability]/spec.md` for requirements

2. **Creating Proposals**:
   - Required for: New features, breaking changes, architecture changes, performance optimizations
   - NOT required for: Bug fixes, typos, formatting, non-breaking dependency updates
   - Use verb-led change IDs: `add-*`, `update-*`, `remove-*`, `refactor-*`

3. **Proposal Structure**:
   - `openspec/changes/[change-id]/proposal.md` - Why, what, impact
   - `openspec/changes/[change-id]/tasks.md` - Implementation checklist
   - `openspec/changes/[change-id]/design.md` - Technical decisions (optional)
   - `openspec/changes/[change-id]/specs/[capability]/spec.md` - Delta requirements

4. **Delta Operations**:
   - `## ADDED Requirements` - New capabilities
   - `## MODIFIED Requirements` - Changed behavior (paste full updated requirement)
   - `## REMOVED Requirements` - Deprecated features
   - Use `#### Scenario:` format (4 hashtags) for scenarios

5. **Validation**:
   - Run `openspec validate [change-id] --strict` before requesting approval
   - Ensure every requirement has at least one scenario
   - Do not start implementation until proposal is approved

See `openspec/AGENTS.md` for complete OpenSpec workflow documentation.

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

- `openspec/AGENTS.md` - OpenSpec workflow and conventions
- `openspec/PROPOSAL_DESIGN_GUIDELINES.md` - UI mockup and diagram guidelines
- `docs/AttendedWeighingService-RxState-Optimization-Report.md` - State management architecture
- `docs/AttendedWeighingService-MemoryLeak-Testing-Guide.md` - Memory leak testing
- `specs/001-attended-weighing/spec.md` - Detailed attended weighing specification
