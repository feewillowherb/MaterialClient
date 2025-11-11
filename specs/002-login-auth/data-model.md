# Data Model: 登录和授权

**Feature**: 002-login-auth  
**Date**: 2025-11-07  
**Status**: Complete

## Overview

本文档定义登录和授权功能所需的数据模型，包括实体设计、数据库表结构、关系映射和验证规则。

---

## Entity Diagram

```
┌─────────────────────────────────┐
│       LicenseInfo               │
│  (软件授权信息)                  │
├─────────────────────────────────┤
│ Id: Guid                        │
│ ProjectId: Guid                 │
│ AuthToken: Guid?                │
│ AuthEndTime: DateTime           │
│ MachineCode: string             │
│ CreationTime: DateTime          │
│ CreatorId: Guid?                │
│ LastModificationTime: DateTime? │
│ LastModifierId: Guid?           │
│ IsDeleted: bool                 │
│ DeletionTime: DateTime?         │
│ DeleterId: Guid?                │
└─────────────────────────────────┘
          │
          │ ProjectId
          ↓
┌─────────────────────────────────┐
│      UserCredential             │
│  (用户登录凭证)                  │
├─────────────────────────────────┤
│ Id: Guid                        │
│ UserName: string                │
│ EncryptedPassword: string       │
│ RememberPassword: bool          │
│ ProjectId: Guid                 │
│ CreationTime: DateTime          │
│ CreatorId: Guid?                │
│ LastModificationTime: DateTime? │
│ LastModifierId: Guid?           │
│ IsDeleted: bool                 │
│ DeletionTime: DateTime?         │
│ DeleterId: Guid?                │
└─────────────────────────────────┘
          │
          │ ProjectId
          ↓
┌─────────────────────────────────┐
│       UserSession               │
│  (用户会话信息)                  │
├─────────────────────────────────┤
│ Id: Guid                        │
│ UserId: long                    │
│ UserName: string                │
│ TrueName: string                │
│ IsAdmin: bool                   │
│ Token: string                   │
│ AuthEndTime: DateTime?          │
│ ProjectId: Guid                 │
│ CoId: int                       │
│ CoName: string                  │
│ ProductId: long                 │
│ ProductName: string             │
│ CreationTime: DateTime          │
│ CreatorId: Guid?                │
│ LastModificationTime: DateTime? │
│ LastModifierId: Guid?           │
│ IsDeleted: bool                 │
│ DeletionTime: DateTime?         │
│ DeleterId: Guid?                │
└─────────────────────────────────┘
```

---

## Entity Definitions

### 1. LicenseInfo (软件授权信息)

**Purpose**: 存储软件的授权状态和有效期信息，用于控制软件的合法使用。

**C# Class**:
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    /// <summary>
    /// 软件授权信息实体
    /// </summary>
    public class LicenseInfo : FullAuditedEntity<Guid>
    {
        /// <summary>
        /// 项目ID（来自基础平台）
        /// </summary>
        public Guid ProjectId { get; set; }
        
        /// <summary>
        /// 授权Token（可选）
        /// </summary>
        public Guid? AuthToken { get; set; }
        
        /// <summary>
        /// 授权到期时间
        /// </summary>
        public DateTime AuthEndTime { get; set; }
        
        /// <summary>
        /// 机器码（用于绑定硬件）
        /// </summary>
        public string MachineCode { get; set; }
    }
}
```

**Table Schema**:
```sql
CREATE TABLE LicenseInfos (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT NOT NULL,
    AuthToken TEXT,
    AuthEndTime TEXT NOT NULL,
    MachineCode TEXT NOT NULL,
    CreationTime TEXT NOT NULL,
    CreatorId TEXT,
    LastModificationTime TEXT,
    LastModifierId TEXT,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    DeletionTime TEXT,
    DeleterId TEXT
);

CREATE INDEX IX_LicenseInfos_ProjectId ON LicenseInfos(ProjectId);
CREATE INDEX IX_LicenseInfos_IsDeleted ON LicenseInfos(IsDeleted);
```

**Validation Rules**:
- `ProjectId`: 必填，GUID格式
- `AuthEndTime`: 必填，必须是未来时间（创建时）
- `MachineCode`: 必填，最大长度128字符
- `MachineCode`: 同一设备上多次授权应保持一致

**Business Rules**:
- 授权有效性检查：`AuthEndTime > DateTime.Now && !IsDeleted`
- 同一ProjectId只保留最新的授权记录（软删除旧记录）
- ProjectId变更时清除旧项目的授权记录

**Lifecycle**:
```
创建 → 使用中 → 过期（软删除） → 重新授权（创建新记录）
```

---

### 2. UserCredential (用户登录凭证)

**Purpose**: 存储"记住密码"功能的用户凭证，用于自动填充登录表单。

**C# Class**:
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    /// <summary>
    /// 用户登录凭证实体
    /// </summary>
    public class UserCredential : FullAuditedEntity<Guid>
    {
        /// <summary>
        /// 用户名（手机号）
        /// </summary>
        public string UserName { get; set; }
        
        /// <summary>
        /// 加密后的密码（AES-256）
        /// </summary>
        public string EncryptedPassword { get; set; }
        
        /// <summary>
        /// 是否记住密码
        /// </summary>
        public bool RememberPassword { get; set; }
        
        /// <summary>
        /// 所属项目ID
        /// </summary>
        public Guid ProjectId { get; set; }
    }
}
```

**Table Schema**:
```sql
CREATE TABLE UserCredentials (
    Id TEXT PRIMARY KEY NOT NULL,
    UserName TEXT NOT NULL,
    EncryptedPassword TEXT NOT NULL,
    RememberPassword INTEGER NOT NULL DEFAULT 0,
    ProjectId TEXT NOT NULL,
    CreationTime TEXT NOT NULL,
    CreatorId TEXT,
    LastModificationTime TEXT,
    LastModifierId TEXT,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    DeletionTime TEXT,
    DeleterId TEXT
);

CREATE UNIQUE INDEX UX_UserCredentials_UserName_ProjectId 
    ON UserCredentials(UserName, ProjectId) 
    WHERE IsDeleted = 0;

CREATE INDEX IX_UserCredentials_ProjectId ON UserCredentials(ProjectId);
```

**Validation Rules**:
- `UserName`: 必填，最大长度64字符，手机号格式（11位数字）
- `EncryptedPassword`: 必填，最大长度512字符（Base64编码）
- `ProjectId`: 必填，GUID格式
- 同一ProjectId下UserName唯一（通过唯一索引强制）

**Business Rules**:
- 登录失败时自动清除凭证（`IsDeleted = true`）
- ProjectId变更时清除旧项目的凭证
- `RememberPassword = false` 时不创建凭证记录
- 密码使用AES-256-CBC加密，密钥存储在配置文件中

**Security Considerations**:
- 密码必须加密存储，禁止明文
- 加密密钥应使用环境变量或安全配置管理
- 建议定期清理过期凭证（超过30天未使用）

---

### 3. UserSession (用户会话信息)

**Purpose**: 存储用户登录后的会话信息和Token，用于后续API调用的身份验证。

**C# Class**:
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    /// <summary>
    /// 用户会话信息实体
    /// </summary>
    public class UserSession : FullAuditedEntity<Guid>
    {
        /// <summary>
        /// 用户ID（来自基础平台）
        /// </summary>
        public long UserId { get; set; }
        
        /// <summary>
        /// 用户名（手机号）
        /// </summary>
        public string UserName { get; set; }
        
        /// <summary>
        /// 真实姓名
        /// </summary>
        public string TrueName { get; set; }
        
        /// <summary>
        /// 是否管理员
        /// </summary>
        public bool IsAdmin { get; set; }
        
        /// <summary>
        /// 登录Token（用于API调用）
        /// </summary>
        public string Token { get; set; }
        
        /// <summary>
        /// Token授权到期时间
        /// </summary>
        public DateTime? AuthEndTime { get; set; }
        
        /// <summary>
        /// 所属项目ID
        /// </summary>
        public Guid ProjectId { get; set; }
        
        /// <summary>
        /// 公司ID
        /// </summary>
        public int CoId { get; set; }
        
        /// <summary>
        /// 公司名称
        /// </summary>
        public string CoName { get; set; }
        
        /// <summary>
        /// 产品ID
        /// </summary>
        public long ProductId { get; set; }
        
        /// <summary>
        /// 产品名称
        /// </summary>
        public string ProductName { get; set; }
    }
}
```

**Table Schema**:
```sql
CREATE TABLE UserSessions (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId INTEGER NOT NULL,
    UserName TEXT NOT NULL,
    TrueName TEXT NOT NULL,
    IsAdmin INTEGER NOT NULL DEFAULT 0,
    Token TEXT NOT NULL,
    AuthEndTime TEXT,
    ProjectId TEXT NOT NULL,
    CoId INTEGER NOT NULL,
    CoName TEXT NOT NULL,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    CreationTime TEXT NOT NULL,
    CreatorId TEXT,
    LastModificationTime TEXT,
    LastModifierId TEXT,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    DeletionTime TEXT,
    DeleterId TEXT
);

CREATE INDEX IX_UserSessions_ProjectId ON UserSessions(ProjectId);
CREATE INDEX IX_UserSessions_UserId ON UserSessions(UserId);
CREATE INDEX IX_UserSessions_IsDeleted ON UserSessions(IsDeleted);
```

**Validation Rules**:
- `UserId`: 必填，正整数
- `UserName`: 必填，最大长度64字符
- `TrueName`: 必填，最大长度64字符
- `Token`: 必填，最大长度512字符
- `CoName`: 必填，最大长度128字符
- `ProductName`: 必填，最大长度128字符

**Business Rules**:
- 登录成功时创建新会话记录
- 登出时软删除会话记录
- ProjectId变更时清除旧项目的会话
- Token有效期由基础平台控制，本地仅存储
- 同一用户可有多个会话（支持多设备登录）

**Token Management**:
- Token用于后续API调用的Authorization Header
- 格式：`Authorization: Bearer {Token}`
- Token过期时需要重新登录
- Token应安全存储，防止泄露

---

## Entity Relationships

### ProjectId as Tenant Discriminator

所有实体通过 `ProjectId` 关联，实现多项目数据隔离：

```csharp
// 查询当前项目的授权信息
var license = await _licenseRepository
    .FirstOrDefaultAsync(l => l.ProjectId == currentProjectId && !l.IsDeleted);

// 查询当前项目的用户凭证
var credential = await _credentialRepository
    .FirstOrDefaultAsync(c => 
        c.UserName == userName && 
        c.ProjectId == currentProjectId && 
        !c.IsDeleted);

// 查询当前项目的用户会话
var session = await _sessionRepository
    .FirstOrDefaultAsync(s => 
        s.UserId == userId && 
        s.ProjectId == currentProjectId && 
        !c.IsDeleted);
```

### Cross-Entity Operations

#### 项目切换时的数据清理
```csharp
public async Task SwitchProjectAsync(Guid newProjectId)
{
    // 1. 清除其他项目的凭证
    var oldCredentials = await _credentialRepository
        .GetListAsync(c => c.ProjectId != newProjectId);
    await _credentialRepository.DeleteManyAsync(oldCredentials, autoSave: true);
    
    // 2. 清除其他项目的会话
    var oldSessions = await _sessionRepository
        .GetListAsync(s => s.ProjectId != newProjectId);
    await _sessionRepository.DeleteManyAsync(oldSessions, autoSave: true);
    
    // 3. 更新当前ProjectId上下文
    _currentProjectId = newProjectId;
}
```

#### 登录流程的数据流
```
1. CheckLicenseStatus()
   ↓
2. 查询 LicenseInfo (ProjectId, AuthEndTime)
   ↓
3. 如果有效 → 显示 LoginWindow
   ↓
4. 查询 UserCredential (UserName, ProjectId)
   ↓
5. 自动填充用户名和密码（如果 RememberPassword = true）
   ↓
6. 用户点击登录 → 调用基础平台API
   ↓
7. 登录成功 → 创建 UserSession (UserId, Token, ProjectId)
   ↓
8. 如果勾选"记住密码" → 创建/更新 UserCredential
```

---

## Database Migrations

### Migration: AddAuthenticationEntities

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    public partial class AddAuthenticationEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create LicenseInfos table
            migrationBuilder.CreateTable(
                name: "LicenseInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    AuthToken = table.Column<Guid>(nullable: true),
                    AuthEndTime = table.Column<DateTime>(nullable: false),
                    MachineCode = table.Column<string>(maxLength: 128, nullable: false),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    CreatorId = table.Column<Guid>(nullable: true),
                    LastModificationTime = table.Column<DateTime>(nullable: true),
                    LastModifierId = table.Column<Guid>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                    DeletionTime = table.Column<DateTime>(nullable: true),
                    DeleterId = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInfos_ProjectId",
                table: "LicenseInfos",
                column: "ProjectId");

            // Create UserCredentials table
            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserName = table.Column<string>(maxLength: 64, nullable: false),
                    EncryptedPassword = table.Column<string>(maxLength: 512, nullable: false),
                    RememberPassword = table.Column<bool>(nullable: false, defaultValue: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    CreatorId = table.Column<Guid>(nullable: true),
                    LastModificationTime = table.Column<DateTime>(nullable: true),
                    LastModifierId = table.Column<Guid>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                    DeletionTime = table.Column<DateTime>(nullable: true),
                    DeleterId = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserCredentials_UserName_ProjectId",
                table: "UserCredentials",
                columns: new[] { "UserName", "ProjectId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserCredentials_ProjectId",
                table: "UserCredentials",
                column: "ProjectId");

            // Create UserSessions table
            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<long>(nullable: false),
                    UserName = table.Column<string>(maxLength: 64, nullable: false),
                    TrueName = table.Column<string>(maxLength: 64, nullable: false),
                    IsAdmin = table.Column<bool>(nullable: false, defaultValue: false),
                    Token = table.Column<string>(maxLength: 512, nullable: false),
                    AuthEndTime = table.Column<DateTime>(nullable: true),
                    ProjectId = table.Column<Guid>(nullable: false),
                    CoId = table.Column<int>(nullable: false),
                    CoName = table.Column<string>(maxLength: 128, nullable: false),
                    ProductId = table.Column<long>(nullable: false),
                    ProductName = table.Column<string>(maxLength: 128, nullable: false),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    CreatorId = table.Column<Guid>(nullable: true),
                    LastModificationTime = table.Column<DateTime>(nullable: true),
                    LastModifierId = table.Column<Guid>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                    DeletionTime = table.Column<DateTime>(nullable: true),
                    DeleterId = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ProjectId",
                table: "UserSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LicenseInfos");
            migrationBuilder.DropTable(name: "UserCredentials");
            migrationBuilder.DropTable(name: "UserSessions");
        }
    }
}
```

### Migration Command
```bash
# 创建迁移
cd MaterialClient.Common
dotnet ef migrations add AddAuthenticationEntities --context MaterialClientDbContext

# 应用迁移
dotnet ef database update --context MaterialClientDbContext

# 或在应用启动时自动应用
// Program.cs
await dbContext.Database.MigrateAsync();
```

---

## DbContext Configuration

```csharp
public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
    // 新增实体
    public DbSet<LicenseInfo> LicenseInfos { get; set; }
    public DbSet<UserCredential> UserCredentials { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    
    // 已有实体...
    public DbSet<WeighingRecord> WeighingRecords { get; set; }
    public DbSet<Material> Materials { get; set; }
    // ...
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // LicenseInfo 配置
        builder.Entity<LicenseInfo>(b =>
        {
            b.ToTable("LicenseInfos");
            b.ConfigureByConvention(); // ABP审计字段自动配置
            
            b.Property(x => x.ProjectId).IsRequired();
            b.Property(x => x.AuthEndTime).IsRequired();
            b.Property(x => x.MachineCode).IsRequired().HasMaxLength(128);
            
            b.HasIndex(x => x.ProjectId);
        });
        
        // UserCredential 配置
        builder.Entity<UserCredential>(b =>
        {
            b.ToTable("UserCredentials");
            b.ConfigureByConvention();
            
            b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
            b.Property(x => x.EncryptedPassword).IsRequired().HasMaxLength(512);
            b.Property(x => x.RememberPassword).IsRequired();
            b.Property(x => x.ProjectId).IsRequired();
            
            b.HasIndex(x => new { x.UserName, x.ProjectId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_UserCredentials_UserName_ProjectId");
            
            b.HasIndex(x => x.ProjectId);
        });
        
        // UserSession 配置
        builder.Entity<UserSession>(b =>
        {
            b.ToTable("UserSessions");
            b.ConfigureByConvention();
            
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
            b.Property(x => x.TrueName).IsRequired().HasMaxLength(64);
            b.Property(x => x.IsAdmin).IsRequired();
            b.Property(x => x.Token).IsRequired().HasMaxLength(512);
            b.Property(x => x.ProjectId).IsRequired();
            b.Property(x => x.CoId).IsRequired();
            b.Property(x => x.CoName).IsRequired().HasMaxLength(128);
            b.Property(x => x.ProductId).IsRequired();
            b.Property(x => x.ProductName).IsRequired().HasMaxLength(128);
            
            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.UserId);
        });
    }
}
```

---

## Data Access Patterns

### Repository Usage (ABP Style)

```csharp
public class AuthorizationService : DomainService, IAuthorizationService
{
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    
    public AuthorizationService(IRepository<LicenseInfo, Guid> licenseRepository)
    {
        _licenseRepository = licenseRepository;
    }
    
    public async Task<bool> CheckLicenseStatusAsync()
    {
        var latestLicense = await _licenseRepository
            .OrderByDescending(l => l.CreationTime)
            .FirstOrDefaultAsync();
        
        if (latestLicense == null)
        {
            return false;
        }
        
        return latestLicense.AuthEndTime > DateTime.Now && !latestLicense.IsDeleted;
    }
    
    public async Task SaveLicenseInfoAsync(LicenseInfoDto dto, string machineCode)
    {
        var license = new LicenseInfo
        {
            Id = GuidGenerator.Create(),
            ProjectId = dto.Proid,
            AuthToken = dto.AuthToken,
            AuthEndTime = dto.AuthEndTime,
            MachineCode = machineCode
        };
        
        await _licenseRepository.InsertAsync(license, autoSave: true);
    }
}
```

### Querying with Specifications

```csharp
// 规范模式（可选，用于复杂查询）
public class ActiveLicenseSpecification : Specification<LicenseInfo>
{
    private readonly Guid _projectId;
    
    public ActiveLicenseSpecification(Guid projectId)
    {
        _projectId = projectId;
    }
    
    public override Expression<Func<LicenseInfo, bool>> ToExpression()
    {
        return license => 
            license.ProjectId == _projectId &&
            license.AuthEndTime > DateTime.Now &&
            !license.IsDeleted;
    }
}

// 使用规范
var activeLicense = await _licenseRepository
    .FirstOrDefaultAsync(new ActiveLicenseSpecification(currentProjectId));
```

---

## Summary

| 实体 | 用途 | 关键字段 | 索引 |
|------|------|----------|------|
| LicenseInfo | 授权管理 | ProjectId, AuthEndTime, MachineCode | ProjectId |
| UserCredential | 记住密码 | UserName, EncryptedPassword, ProjectId | (UserName, ProjectId) UNIQUE |
| UserSession | 登录会话 | UserId, Token, ProjectId | ProjectId, UserId |

**下一步**: contracts/ - API合约定义

