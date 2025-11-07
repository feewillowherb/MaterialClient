# Quick Start Guide: 登录和授权

**Feature**: 002-login-auth  
**Date**: 2025-11-07  
**Target Audience**: 开发人员

## Overview

本指南帮助开发人员快速理解并开始实施登录和授权功能。涵盖环境配置、代码结构、关键组件和常见问题解决。

---

## Prerequisites

### System Requirements
- Windows 10/11 (x64)
- .NET 9.0 SDK
- Visual Studio 2022 / JetBrains Rider 2024
- SQLite (嵌入式，无需单独安装)

### NuGet Packages
```xml
<!-- 已安装的包（验证版本） -->
<PackageReference Include="Avalonia" Version="11.3.6" />
<PackageReference Include="Volo.Abp.EntityFrameworkCore.Sqlite" Version="9.3.6" />
<PackageReference Include="Refit.HttpClientFactory" Version="8.0.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.1" />
<PackageReference Include="System.Management" Version="9.0.0" /> <!-- [NEW] 用于机器码 -->
```

### Configuration
确保 `appsettings.json` 包含基础平台配置：
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=MaterialClient.db"
  },
  "BasePlatform": {
    "BaseUrl": "http://base.publicapi.findong.com",
    "ProductCode": "5000"
  },
  "Encryption": {
    "AesKey": "your-256-bit-key-here-32-bytes" // Base64编码的32字节密钥
  }
}
```

---

## Project Structure Overview

```
MaterialClient.sln
│
├── MaterialClient/                    # Avalonia UI 项目
│   ├── Views/
│   │   ├── AuthCodeWindow.axaml      # [NEW] 授权码窗口
│   │   ├── LoginWindow.axaml         # [NEW] 登录窗口
│   │   └── AttendedWeighingWindow    # [EXISTING] 主界面
│   ├── ViewModels/
│   │   ├── AuthCodeWindowViewModel   # [NEW] 授权窗口VM
│   │   └── LoginWindowViewModel      # [NEW] 登录窗口VM
│   └── App.axaml.cs                  # [MODIFY] 启动流程
│
└── MaterialClient.Common/             # 业务逻辑库
    ├── Entities/
    │   ├── LicenseInfo.cs            # [NEW] 授权信息
    │   ├── UserCredential.cs         # [NEW] 用户凭证
    │   └── UserSession.cs            # [NEW] 用户会话
    ├── Services/
    │   ├── Authorization/
    │   │   ├── IAuthorizationService # [NEW] 授权服务接口
    │   │   └── AuthorizationService  # [NEW] 授权服务实现
    │   ├── Authentication/
    │   │   ├── IAuthenticationService # [NEW] 认证服务接口
    │   │   ├── AuthenticationService  # [NEW] 认证服务实现
    │   │   ├── IPasswordEncryptionService # [NEW] 密码加密接口
    │   │   ├── PasswordEncryptionService # [NEW] 密码加密实现
    │   │   ├── IMachineCodeService    # [NEW] 机器码服务接口
    │   │   └── MachineCodeService     # [NEW] 机器码服务实现
    │   └── IStartupService           # [NEW] 启动服务
    └── Api/
        ├── IBasePlatformApi.cs       # [NEW] 基础平台API（Refit）
        └── Dtos/                     # [NEW] API DTO定义
```

---

## Step-by-Step Implementation

### Step 1: 创建数据实体

**Location**: `MaterialClient.Common/Entities/`

#### 1.1 LicenseInfo.cs
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    public class LicenseInfo : FullAuditedEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Guid? AuthToken { get; set; }
        public DateTime AuthEndTime { get; set; }
        public string MachineCode { get; set; }
    }
}
```

#### 1.2 UserCredential.cs
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    public class UserCredential : FullAuditedEntity<Guid>
    {
        public string UserName { get; set; }
        public string EncryptedPassword { get; set; }
        public bool RememberPassword { get; set; }
        public Guid ProjectId { get; set; }
    }
}
```

#### 1.3 UserSession.cs
```csharp
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities
{
    public class UserSession : FullAuditedEntity<Guid>
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string TrueName { get; set; }
        public bool IsAdmin { get; set; }
        public string Token { get; set; }
        public DateTime? AuthEndTime { get; set; }
        public Guid ProjectId { get; set; }
        public int CoId { get; set; }
        public string CoName { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; }
    }
}
```

**Action**: 创建这3个实体文件

---

### Step 2: 更新 DbContext

**Location**: `MaterialClient.Common/EntityFrameworkCore/MaterialClientDbContext.cs`

#### 2.1 添加 DbSet
```csharp
public DbSet<LicenseInfo> LicenseInfos { get; set; }
public DbSet<UserCredential> UserCredentials { get; set; }
public DbSet<UserSession> UserSessions { get; set; }
```

#### 2.2 配置实体映射
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    
    builder.Entity<LicenseInfo>(b =>
    {
        b.ToTable("LicenseInfos");
        b.ConfigureByConvention();
        b.Property(x => x.MachineCode).IsRequired().HasMaxLength(128);
        b.HasIndex(x => x.ProjectId);
    });
    
    builder.Entity<UserCredential>(b =>
    {
        b.ToTable("UserCredentials");
        b.ConfigureByConvention();
        b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
        b.Property(x => x.EncryptedPassword).IsRequired().HasMaxLength(512);
        b.HasIndex(x => new { x.UserName, x.ProjectId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    });
    
    builder.Entity<UserSession>(b =>
    {
        b.ToTable("UserSessions");
        b.ConfigureByConvention();
        b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
        b.Property(x => x.Token).IsRequired().HasMaxLength(512);
        b.HasIndex(x => x.ProjectId);
    });
}
```

**Action**: 修改 MaterialClientDbContext.cs

---

### Step 3: 创建数据库迁移

```bash
# 进入Common项目目录
cd MaterialClient.Common

# 创建迁移
dotnet ef migrations add AddAuthenticationEntities --context MaterialClientDbContext

# 应用迁移（或在应用启动时自动应用）
dotnet ef database update --context MaterialClientDbContext
```

**验证**: 检查生成的迁移文件和数据库表

---

### Step 4: 定义 API 合约

**Location**: `MaterialClient.Common/Api/`

#### 4.1 创建 DTOs
在 `Api/Dtos/` 目录下创建：
- `HttpResult.cs` - 统一响应包装
- `LicenseRequestDto.cs` - 授权请求
- `LicenseInfoDto.cs` - 授权信息
- `LoginRequestDto.cs` - 登录请求
- `LoginUserDto.cs` - 登录用户信息

参考 [contracts/README.md](./contracts/README.md) 中的完整定义。

#### 4.2 创建 Refit 接口
**File**: `Api/IBasePlatformApi.cs`
```csharp
using Refit;
using MaterialClient.Common.Api.Dtos;

namespace MaterialClient.Common.Api
{
    public interface IBasePlatformApi
    {
        [Post("/api/AuthClientLicense/GetAuthClientLicense")]
        Task<HttpResult<LicenseInfoDto>> GetAuthClientLicenseAsync(
            [Body] LicenseRequestDto request,
            CancellationToken cancellationToken = default);
        
        [Post("/User/UserLogin")]
        Task<HttpResult<LoginUserDto>> UserLoginAsync(
            [Body] LoginRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
```

**Action**: 创建所有DTO文件和Refit接口

---

### Step 5: 实现服务层

#### 5.1 密码加密服务
**File**: `Services/Authentication/PasswordEncryptionService.cs`
```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MaterialClient.Common.Services.Authentication
{
    public class PasswordEncryptionService : IPasswordEncryptionService
    {
        private readonly byte[] _key;
        
        public PasswordEncryptionService(IConfiguration configuration)
        {
            var keyString = configuration["Encryption:AesKey"];
            _key = Convert.FromBase64String(keyString);
            
            if (_key.Length != 32)
                throw new ArgumentException("AES key must be 256 bits (32 bytes)");
        }
        
        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        
        public string Decrypt(string cipherText)
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = _key;
            
            var iv = new byte[aes.IV.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;
            
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
            
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
```

#### 5.2 机器码服务
**File**: `Services/Authentication/MachineCodeService.cs`
```csharp
using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace MaterialClient.Common.Services.Authentication
{
    public class MachineCodeService : IMachineCodeService
    {
        public string GetMachineCode()
        {
            var cpuId = GetCpuId();
            var boardSerial = GetBoardSerialNumber();
            var macAddress = GetFirstMacAddress();
            
            var combined = $"{cpuId}|{boardSerial}|{macAddress}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            
            return Convert.ToBase64String(hash);
        }
        
        private string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessorId FROM Win32_Processor");
                
                foreach (var obj in searcher.Get())
                {
                    return obj["ProcessorId"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // 忽略错误，返回空字符串
            }
            
            return string.Empty;
        }
        
        private string GetBoardSerialNumber()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT SerialNumber FROM Win32_BaseBoard");
                
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return string.Empty;
        }
        
        private string GetFirstMacAddress()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.OperationalStatus == OperationalStatus.Up)
                    .OrderBy(n => n.Name)
                    .ToList();
                
                if (nics.Any())
                {
                    return nics.First().GetPhysicalAddress().ToString();
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return string.Empty;
        }
    }
}
```

#### 5.3 授权服务
**File**: `Services/Authorization/AuthorizationService.cs`
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;

namespace MaterialClient.Common.Services.Authorization
{
    public class AuthorizationService : DomainService, IAuthorizationService
    {
        private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
        private readonly IBasePlatformApi _basePlatformApi;
        private readonly IMachineCodeService _machineCodeService;
        private readonly ILogger<AuthorizationService> _logger;
        
        public AuthorizationService(
            IRepository<LicenseInfo, Guid> licenseRepository,
            IBasePlatformApi basePlatformApi,
            IMachineCodeService machineCodeService,
            ILogger<AuthorizationService> logger)
        {
            _licenseRepository = licenseRepository;
            _basePlatformApi = basePlatformApi;
            _machineCodeService = machineCodeService;
            _logger = logger;
        }
        
        public async Task<bool> CheckLicenseStatusAsync()
        {
            var latestLicense = await _licenseRepository
                .OrderByDescending(l => l.CreationTime)
                .FirstOrDefaultAsync();
            
            if (latestLicense == null)
            {
                _logger.LogInformation("No license found in database");
                return false;
            }
            
            if (latestLicense.AuthEndTime < DateTime.Now)
            {
                _logger.LogWarning("License expired: {AuthEndTime}", latestLicense.AuthEndTime);
                return false;
            }
            
            return !latestLicense.IsDeleted;
        }
        
        public async Task<(bool Success, string ErrorMessage, LicenseInfoDto Data)> VerifyAuthCodeAsync(string authCode)
        {
            try
            {
                var request = new LicenseRequestDto
                {
                    ProductCode = "5000",
                    Code = authCode
                };
                
                var response = await _basePlatformApi.GetAuthClientLicenseAsync(request);
                
                if (response.Success)
                {
                    // 保存授权信息
                    var machineCode = _machineCodeService.GetMachineCode();
                    
                    var license = new LicenseInfo
                    {
                        Id = GuidGenerator.Create(),
                        ProjectId = response.Data.Proid,
                        AuthToken = response.Data.AuthToken,
                        AuthEndTime = response.Data.AuthEndTime,
                        MachineCode = machineCode
                    };
                    
                    await _licenseRepository.InsertAsync(license, autoSave: true);
                    
                    _logger.LogInformation("License saved successfully: ProjectId={ProjectId}", license.ProjectId);
                    
                    return (true, string.Empty, response.Data);
                }
                else
                {
                    _logger.LogWarning("Authorization failed: {Message}", response.Msg);
                    return (false, response.Msg, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization");
                return (false, "网络连接失败，请检查网络设置", null);
            }
        }
    }
}
```

**Action**: 实现所有服务类和接口

---

### Step 6: 配置 ABP 模块

**Location**: `MaterialClient.Common/MaterialClientCommonModule.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Volo.Abp.Modularity;
using MaterialClient.Common.Api;
using MaterialClient.Common.Services.Authorization;
using MaterialClient.Common.Services.Authentication;

[DependsOn(
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpDddDomainModule),
    typeof(AbpAutofacModule)
)]
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"];
        
        // 注册Refit客户端
        context.Services.AddRefitClient<IBasePlatformApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(basePlatformUrl))
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        
        // 注册服务
        context.Services.AddTransient<IAuthorizationService, AuthorizationService>();
        context.Services.AddTransient<IAuthenticationService, AuthenticationService>();
        context.Services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
        context.Services.AddSingleton<IMachineCodeService, MachineCodeService>();
    }
}
```

**Action**: 更新模块配置

---

### Step 7: 创建 UI 窗口

#### 7.1 AuthCodeWindow (授权码窗口)
**File**: `MaterialClient/Views/AuthCodeWindow.axaml`
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MaterialClient.ViewModels"
        x:DataType="vm:AuthCodeWindowViewModel"
        x:Class="MaterialClient.Views.AuthCodeWindow"
        Title="软件授权" Width="500" Height="300"
        WindowStartupLocation="CenterScreen"
        CanResize="False">
    
    <Grid>
        <StackPanel Margin="40" VerticalAlignment="Center">
            <TextBlock Text="请输入授权码" FontSize="18" FontWeight="Bold" 
                       Margin="0,0,0,20"/>
            
            <TextBox Text="{Binding AuthCode}" 
                     Watermark="请输入授权码"
                     FontSize="14" Padding="10" Margin="0,0,0,20"/>
            
            <Button Content="确认" Command="{Binding VerifyCommand}"
                    HorizontalAlignment="Center" Width="120" Height="40"
                    FontSize="14"/>
            
            <TextBlock Text="{Binding ErrorMessage}" 
                       Foreground="Red" FontSize="12"
                       Margin="0,10,0,0" TextAlignment="Center"
                       IsVisible="{Binding HasError}"/>
        </StackPanel>
    </Grid>
</Window>
```

**File**: `MaterialClient/ViewModels/AuthCodeWindowViewModel.cs`
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Services.Authorization;

namespace MaterialClient.ViewModels
{
    public partial class AuthCodeWindowViewModel : ObservableObject
    {
        private readonly IAuthorizationService _authService;
        
        [ObservableProperty]
        private string _authCode = string.Empty;
        
        [ObservableProperty]
        private string _errorMessage = string.Empty;
        
        [ObservableProperty]
        private bool _hasError;
        
        public AuthCodeWindowViewModel(IAuthorizationService authService)
        {
            _authService = authService;
        }
        
        [RelayCommand]
        private async Task VerifyAsync()
        {
            HasError = false;
            
            if (string.IsNullOrWhiteSpace(AuthCode))
            {
                ErrorMessage = "请输入授权码";
                HasError = true;
                return;
            }
            
            var (success, errorMessage, _) = await _authService.VerifyAuthCodeAsync(AuthCode);
            
            if (success)
            {
                // TODO: 导航到登录窗口
                // NavigateToLoginWindow();
            }
            else
            {
                ErrorMessage = errorMessage;
                HasError = true;
            }
        }
    }
}
```

#### 7.2 LoginWindow (登录窗口)
参考 [research.md R6节](./research.md#r6-ui窗口设计参考) 完整实现。

**Action**: 创建所有窗口和ViewModel

---

### Step 8: 修改应用启动流程

**Location**: `MaterialClient/App.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using MaterialClient.Views;
using MaterialClient.Services;

namespace MaterialClient
{
    public partial class App : Application
    {
        private IAbpApplicationWithInternalServiceProvider _abpApplication;
        
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // 初始化ABP
                _abpApplication = await AbpApplicationFactory
                    .CreateAsync<MaterialClientAvaloniaModule>(options =>
                    {
                        options.UseAutofac();
                    });
                
                await _abpApplication.InitializeAsync();
                
                // 确定启动窗口
                var startupService = _abpApplication.ServiceProvider
                    .GetRequiredService<IStartupService>();
                
                desktop.MainWindow = await startupService.DetermineStartupWindowAsync();
            }
            
            base.OnFrameworkInitializationCompleted();
        }
        
        public override async void OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            await _abpApplication?.ShutdownAsync();
            _abpApplication?.Dispose();
            base.OnExit(sender, e);
        }
    }
}
```

**Action**: 修改App.axaml.cs

---

## Testing

### Run Unit Tests
```bash
cd MaterialClient.Common.Tests
dotnet test --filter "Category=Unit"
```

### Run Integration Tests
```bash
cd MaterialClient.Common.Tests
dotnet test --filter "Category=Integration"
```

### Run BDD Tests
```bash
cd MaterialClient.Common.Tests
dotnet test --filter "Category=BDD"
```

### Manual Testing Checklist
- [ ] 首次启动显示授权窗口
- [ ] 输入有效授权码成功进入登录页面
- [ ] 输入无效授权码显示错误提示
- [ ] 登录成功进入主界面
- [ ] "记住密码"功能正常工作
- [ ] 网络失败显示重试按钮
- [ ] 授权过期重新显示授权窗口

---

## Troubleshooting

### 问题1：数据库迁移失败
**症状**: `dotnet ef migrations add` 失败
**解决**:
```bash
# 确保安装了EF Core工具
dotnet tool install --global dotnet-ef

# 清理并重建
dotnet clean
dotnet build

# 重新尝试迁移
dotnet ef migrations add AddAuthenticationEntities
```

### 问题2：Refit客户端注册失败
**症状**: 运行时找不到 `IBasePlatformApi`
**解决**:
- 检查 `appsettings.json` 中的 `BasePlatform:BaseUrl` 配置
- 确保在ABP模块中正确注册了Refit客户端
- 验证 `Refit.HttpClientFactory` 包版本

### 问题3：机器码服务异常
**症状**: `System.Management` 相关异常
**解决**:
- 确保已安装 `System.Management` NuGet包
- 检查是否有WMI访问权限
- 在catch块中忽略错误，返回空字符串

### 问题4：密码加密失败
**症状**: 加密/解密抛出异常
**解决**:
- 检查 `appsettings.json` 中的 `Encryption:AesKey`
- 确保密钥是32字节（256位）的Base64编码
- 生成新密钥：
```csharp
var key = new byte[32];
RandomNumberGenerator.Fill(key);
var keyString = Convert.ToBase64String(key);
Console.WriteLine(keyString);
```

---

## Next Steps

完成快速入门后：

1. **编写测试**: 参考 [research.md R7](./research.md#r7-测试策略) 编写TDD测试
2. **任务分解**: 运行 `/speckit.tasks` 生成详细任务列表
3. **开始开发**: 按照TDD流程实施功能

---

## Additional Resources

- [ABP Framework Documentation](https://docs.abp.io/)
- [Avalonia UI Documentation](https://docs.avaloniaui.net/)
- [Refit Documentation](https://github.com/reactiveui/refit)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core/)

---

**Last Updated**: 2025-11-07  
**Maintained By**: MaterialClient Development Team

