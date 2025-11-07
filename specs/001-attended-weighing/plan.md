# Implementation Plan: 有人值守功能实现

**Branch**: `001-attended-weighing` | **Date**: 2025-11-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-attended-weighing/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

实现有人值守功能，包括：
1. 为Avalonia应用集成ABP HTTP Host（支持Swagger）和AutofacServiceFactory
2. 创建有人值守页面UI，显示未完成的称重记录和已完成的运单
3. 实现自动称重检测逻辑，监测地磅重量变化并自动创建称重记录
4. 实现称重记录自动匹配和运单生成功能
5. 实现硬件接口模拟（地磅重量、车牌抓拍、车辆拍照、票据拍照），返回固定测试值并支持HTTP接口修改

技术方案：在现有ABP框架基础上，添加ABP HTTP Host集成，使用Avalonia UI框架实现前端界面，使用ABP领域服务和仓储模式实现业务逻辑，使用SQLite持久化数据。

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: 
- ABP Framework 9.3.6 (Volo.Abp.Core, Volo.Abp.Ddd.Domain, Volo.Abp.EntityFrameworkCore.Sqlite)
- Avalonia UI 11.3.6 (Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent)
- Entity Framework Core 9.0.10 (Microsoft.EntityFrameworkCore.Sqlite)
- SQLite (Microsoft.Data.Sqlite, SQLitePCLRaw.bundle_zetetic)
- AutoConstructor 5.6.0 (source generator)
- Refit.HttpClientFactory 8.0.0 (HTTP client)
- CommunityToolkit.Mvvm 8.2.1 (MVVM pattern)

**Storage**: SQLite (embedded database, stored locally)  
**Testing**: 
- NUnit (test framework)
- Reqnroll.NUnit (BDD testing)
- NSubstitute (mocking)
- Shouldly (assertions)
- ABP TestBase (integration testing infrastructure)
- In-memory SQLite for testing

**Target Platform**: Windows Desktop (win-x64)  
**Project Type**: Desktop application (Avalonia) with ABP framework integration  
**Performance Goals**: 
- 称重记录创建和持久化在2秒内完成
- 页面响应时间在1秒内（支持100条记录列表）
- 连续车辆称重场景，车辆驶离后5秒内恢复到可接受下一车辆状态

**Constraints**: 
- Windows桌面客户端，仅限Windows平台
- 本地SQLite数据库持久化
- 硬件接口当前阶段返回固定测试值（待对接设备）
- 代码中变量名和字段必须是英文字符，禁止使用中文字符
- 命名约定：使用MaterialClient前缀替代未知前缀如My

**Scale/Scope**: 
- 支持至少100条未匹配称重记录和100条已完成运单的列表显示
- 单用户操作（当前阶段不需要多用户并发控制）
- 本地数据存储，无需网络同步（当前阶段）

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Architecture-First
- **Status**: PASS
- **Compliance**: 采用清晰的分层架构：UI（Avalonia）、应用服务、领域、基础设施（EF Core/SQLite）
- **Notes**: 使用ABP框架提供的基础设施（依赖注入、领域驱动设计、数据访问）

### ✅ ABP Framework Integration
- **Status**: PASS
- **Compliance**: 统一使用ABP框架9.3.6，依赖注入使用Autofac，数据访问使用ABP EntityFrameworkCore集成
- **Notes**: 需要添加ABP HTTP Host支持（最小功能，支持Swagger）

### ✅ Test-First (NON-NEGOTIABLE)
- **Status**: PASS (必须满足)
- **Compliance**: TDD强制要求，集成测试使用ABP集成测试框架，BDD测试使用Reqnroll.NUnit
- **Notes**: 称重逻辑需要完成集成测试（spec要求）

### ✅ Integration Testing
- **Status**: PASS
- **Compliance**: 测试项目统一在MaterialClient.Common.Tests，使用ABP TestBase，内存SQLite测试
- **Notes**: 需要使用测试基础设施进行称重逻辑的集成测试

### ✅ Observability & Simplicity
- **Status**: PASS
- **Compliance**: 关键路径记录结构化日志（称重失败日志），遵循YAGNI原则
- **Notes**: 称重失败场景需要记录日志

### ✅ Code Character Constraints (NON-NEGOTIABLE)
- **Status**: PASS (必须满足)
- **Compliance**: 代码中变量名和字段必须是英文字符，禁止使用中文字符
- **Notes**: 所有代码实现必须使用英文命名

### ✅ Naming Convention (NON-NEGOTIABLE)
- **Status**: PASS (必须满足)
- **Compliance**: 使用MaterialClient前缀替代未知前缀
- **Notes**: 所有类名、接口名、命名空间必须遵循此约定

**Overall Status**: ✅ PASS - All gates passed. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
MaterialClient/                          # Main Avalonia application
├── Views/
│   └── AttendedWeighingWindow.axaml    # New: 有人值守页面 (参考assets/中的设计文件)
├── ViewModels/
│   └── AttendedWeighingViewModel.cs    # New: 有人值守页面ViewModel
├── Api/
│   └── HardwareApi.cs                  # New: 硬件接口API（Refit）
├── App.axaml.cs                         # Modify: 集成ABP HTTP Host
└── Program.cs                           # Modify: 配置ABP和HTTP Host

MaterialClient.Common/                   # Common library
├── Entities/
│   ├── WeighingRecord.cs               # Modify: 添加WeighingRecordType字段
│   └── Enums/
│       ├── VehicleWeightStatus.cs      # New: 车辆重量状态枚举
│       ├── DeliveryType.cs             # New: 配送类型枚举
│       └── WeighingRecordType.cs       # New: 称重记录类型枚举
├── Services/
│   ├── WeighingService.cs              # New: 称重服务（监测地磅、创建记录）
│   ├── WeighingMatchingService.cs      # New: 称重记录匹配服务
│   └── Hardware/
│       ├── ITruckScaleWeightService.cs # New: 地磅重量接口
│       ├── TruckScaleWeightService.cs  # New: 地磅重量服务（模拟实现）
│       ├── IPlateNumberCaptureService.cs # New: 车牌抓拍接口
│       ├── PlateNumberCaptureService.cs # New: 车牌抓拍服务（模拟实现）
│       ├── IVehiclePhotoService.cs     # New: 车辆拍照接口
│       ├── VehiclePhotoService.cs      # New: 车辆拍照服务（模拟实现）
│       ├── IBillPhotoService.cs        # New: 票据拍照接口
│       └── BillPhotoService.cs         # New: 票据拍照服务（模拟实现）
├── EntityFrameworkCore/
│   └── MaterialClientDbContext.cs      # Modify: 添加新实体配置
└── MaterialClientCommonModule.cs        # Modify: 注册新服务

MaterialClient.Common.Tests/             # Test project
├── WeighingServiceTests.cs             # New: 称重服务集成测试
├── WeighingMatchingServiceTests.cs      # New: 匹配服务集成测试
└── Hardware/
    └── HardwareServiceTests.cs         # New: 硬件接口测试
```

**Structure Decision**: 采用现有项目结构（MaterialClient主应用 + MaterialClient.Common公共库 + MaterialClient.Common.Tests测试项目）。新功能主要在MaterialClient.Common中添加领域服务和实体扩展，在MaterialClient中添加UI页面，保持清晰的分层架构。

## Complexity Tracking

> **No violations** - All Constitution Check gates passed. No complexity justification needed.
