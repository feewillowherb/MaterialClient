<!--
DOCUMENT_STATUS: SUPERSEDED
LAST_REVIEWED: 2026-01-15
REVIEWER: Claude (OpenSpec Migration)
NOTES: Legacy specification format. Superseded by OpenSpec workflow adopted 2026-01-15. 
       Content preserved for historical reference. For current specifications, see openspec/specs/.
-->

# Implementation Plan: 登录和授权

**Branch**: `002-login-auth` | **Date**: 2025-11-07 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/002-login-auth/spec.md`

## Summary

实现桌面应用程序的授权激活和用户登录功能，包括：
1. 软件授权码验证（通过基础平台API）
2. 用户账号密码登录
3. "记住密码"功能
4. 授权到期检测与重新验证

技术方案采用 Avalonia UI + ABP Framework + Entity Framework Core + SQLite 的桌面应用架构，使用 Refit 进行 HTTP API 调用。

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: 
- Avalonia 11.3.6 (UI框架)
- ABP Framework 9.3.6 (领域驱动设计和基础设施)
- Entity Framework Core 9.0.10 (数据访问)
- Microsoft.Data.Sqlite 9.0.10 (SQLite数据库)
- Refit 8.0.0 (HTTP客户端)
- CommunityToolkit.Mvvm 8.2.1 (MVVM模式)

**Storage**: SQLite (本地嵌入式数据库，连接字符串：`Data Source=MaterialClient.db`)  
**Testing**: 
- ABP IntegratedTest Framework (集成测试)
- Reqnroll.NUnit (BDD测试)
- NSubstitute (模拟)
- Shouldly (断言)

**Target Platform**: Windows Desktop (win-x64, .NET 9.0 WinExe)  
**Project Type**: Windows Desktop Application (Avalonia UI)  
**Performance Goals**: 
- 授权验证响应时间 < 5秒
- 登录流程完成 < 30秒
- 记住密码自动填充 < 1秒
- UI响应时间 < 100ms

**Constraints**: 
- 必须支持离线数据存储（授权信息、用户凭证）
- 密码必须加密存储（AES对称加密）
- 网络调用必须有重试机制
- 授权到期检测准确率 100%

**Scale/Scope**: 
- 2个主要UI窗口（授权码输入、用户登录）
- 3个实体（LicenseInfo、UserCredential、LoginUserDto）
- 2个外部API调用（授权验证、用户登录）
- 19条功能需求

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Compliance Status

| 原则 | 状态 | 说明 |
|------|------|------|
| **I. Architecture-First** | ✅ PASS | 分层架构清晰：UI (Avalonia) → Services → Domain → Infrastructure (EF Core/SQLite + Refit) |
| **II. ABP Framework Integration** | ✅ PASS | 使用 ABP 9.3.6，包含 Autofac、EntityFrameworkCore.Sqlite、Domain 包 |
| **III. Test-First** | ✅ PASS | 将采用 TDD 流程，使用 ABP 集成测试 + Reqnroll BDD 测试 |
| **IV. Integration Testing** | ✅ PASS | 测试在 MaterialClient.Common.Tests 项目中，使用内存 SQLite |
| **V. Observability** | ✅ PASS | 关键路径（API调用、授权验证、登录）将记录结构化日志 |
| **代码字符约束** | ✅ PASS | 所有代码使用英文命名，注释可用中文 |
| **命名约定** | ✅ PASS | 遵循 MaterialClient 命名前缀（如 MaterialClientDbContext） |
| **SQLite 配置** | ✅ PASS | 使用 Volo.Abp.EntityFrameworkCore.Sqlite 9.3.6 包 |
| **实体基类** | ✅ PASS | 实体将继承 ABP 提供的 Entity<TKey> 或 FullAuditedEntity<TKey> |
| **仓储模式** | ✅ PASS | 使用 IRepository<TEntity, TKey> 接口访问数据 |

### 🎯 Gates Evaluation

**Pre-Phase 0**: ✅ **PASSED**  
- 无宪章违规
- 技术栈符合项目约束
- 测试策略明确

**Post-Phase 1**: ✅ **PASSED**  
- 数据模型符合ABP实体规范（继承FullAuditedEntity）
- API合约遵循Refit最佳实践
- 服务层使用DomainService基类和IRepository接口
- 密码加密采用AES-256-CBC（符合安全要求）
- 机器码生成使用硬件标识（符合授权绑定要求）
- 所有代码使用英文命名（符合宪章字符约束）
- 测试策略完整（单元+集成+BDD三层）
- 无新增复杂度或宪章违规

## Project Structure

### Documentation (this feature)

```text
specs/002-login-auth/
├── spec.md             # 功能规范 (已完成)
├── plan.md             # 本文件 - 实施计划
├── research.md         # Phase 0 输出 - 技术研究
├── data-model.md       # Phase 1 输出 - 数据模型设计
├── quickstart.md       # Phase 1 输出 - 快速入门指南
├── contracts/          # Phase 1 输出 - API合约定义
│   ├── README.md
│   └── base-platform-api.yaml
├── checklists/         # 质量检查清单
│   └── requirements.md
└── tasks.md            # Phase 2 输出 (通过 /speckit.tasks 命令生成)
```

### Source Code (repository root)

```text
MaterialClient/                          # Avalonia 桌面应用项目
├── Views/
│   ├── AuthCodeWindow.axaml            # [NEW] 授权码输入窗口
│   ├── AuthCodeWindow.axaml.cs
│   ├── LoginWindow.axaml               # [NEW] 用户登录窗口
│   ├── LoginWindow.axaml.cs
│   ├── AttendedWeighingWindow.axaml    # [EXISTING] 称重管理主界面
│   └── MainWindow.axaml                # [HIDE/REMOVE] 原有主窗口
├── ViewModels/
│   ├── AuthCodeWindowViewModel.cs      # [NEW] 授权窗口视图模型
│   ├── LoginWindowViewModel.cs         # [NEW] 登录窗口视图模型
│   └── AttendedWeighingViewModel.cs    # [EXISTING] 主界面视图模型
├── Services/
│   ├── ServiceLocator.cs               # [EXISTING] 服务定位器
│   └── IStartupService.cs              # [NEW] 启动服务接口
├── App.axaml                           # [MODIFY] 应用程序入口
├── App.axaml.cs                        # [MODIFY] 启动逻辑修改
├── Program.cs                          # [MODIFY] ABP模块配置
└── appsettings.json                    # [MODIFY] 添加基础平台配置

MaterialClient.Common/                   # 共享业务逻辑库
├── Entities/
│   ├── LicenseInfo.cs                  # [NEW] 授权信息实体
│   ├── UserCredential.cs               # [NEW] 用户凭证实体
│   └── UserSession.cs                  # [NEW] 用户会话实体
├── EntityFrameworkCore/
│   ├── MaterialClientDbContext.cs      # [MODIFY] 添加新实体DbSet
│   └── Migrations/                     # [NEW] 数据库迁移
├── Services/
│   ├── Authorization/
│   │   ├── IAuthorizationService.cs    # [NEW] 授权服务接口
│   │   └── AuthorizationService.cs     # [NEW] 授权服务实现
│   ├── Authentication/
│   │   ├── IAuthenticationService.cs   # [NEW] 认证服务接口
│   │   ├── AuthenticationService.cs    # [NEW] 认证服务实现
│   │   └── PasswordEncryptionService.cs # [NEW] 密码加密服务
│   └── Storage/
│       ├── ILicenseStorage.cs          # [NEW] 授权信息存储接口
│       └── ICredentialStorage.cs       # [NEW] 凭证存储接口
├── Api/
│   ├── IBasePlatformApi.cs             # [NEW] 基础平台API接口(Refit)
│   ├── Dtos/
│   │   ├── HttpResult.cs               # [NEW] HTTP响应包装
│   │   ├── LicenseRequestDto.cs        # [NEW] 授权请求DTO
│   │   ├── LicenseInfoDto.cs           # [NEW] 授权信息DTO
│   │   ├── LoginRequestDto.cs          # [NEW] 登录请求DTO
│   │   └── LoginUserDto.cs             # [NEW] 登录用户DTO
│   └── Extensions/
│       └── RefitExtensions.cs          # [NEW] Refit扩展配置
└── Configuration/
    └── BasePlatformOptions.cs          # [NEW] 基础平台配置选项

MaterialClient.Common.Tests/             # 测试项目
├── Features/
│   └── Authorization.feature           # [NEW] 授权功能BDD测试
│   └── Authentication.feature          # [NEW] 认证功能BDD测试
├── AuthorizationServiceTests.cs        # [NEW] 授权服务集成测试
├── AuthenticationServiceTests.cs       # [NEW] 认证服务集成测试
└── PasswordEncryptionServiceTests.cs   # [NEW] 密码加密服务单元测试
```

**Structure Decision**: 采用 Avalonia 桌面应用 + ABP 共享库的双项目结构。MaterialClient 项目负责 UI 层（MVVM），MaterialClient.Common 负责业务逻辑、数据访问和外部集成。测试统一在 MaterialClient.Common.Tests 项目中。

## Complexity Tracking

> 本功能符合项目宪章的所有约束，无需记录违规说明。

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

---

**下一步**: Phase 0 - Research (生成 research.md)
