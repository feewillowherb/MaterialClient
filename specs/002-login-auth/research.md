# Research: 登录和授权

**Feature**: 002-login-auth  
**Date**: 2025-11-07  
**Status**: Complete

## Overview

本文档记录登录和授权功能实施过程中的技术研究决策，包括密码加密、HTTP客户端配置、应用启动流程和数据库设计等关键技术选型。

---

## R1: 密码加密方案

### Decision
使用 **AES-256-CBC** 对称加密算法加密"记住密码"功能中的密码。

### Rationale
1. **对称加密适合此场景**：需要解密密码用于自动登录，不能使用单向哈希
2. **AES-256安全性高**：NIST批准的标准，业界广泛使用
3. **.NET内置支持**：`System.Security.Cryptography.Aes` 提供高质量实现
4. **CBC模式带IV**：每次加密使用随机IV，相同密码产生不同密文

### Implementation Details
```csharp
// 使用 System.Security.Cryptography.Aes
public class PasswordEncryptionService
{
    private readonly byte[] _key; // 256-bit key from configuration
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // 随机IV
        
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // 返回 IV + 密文 的Base64编码
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
        
        // 提取IV
        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;
        
        // 提取密文
        var cipher = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
        
        var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

### Key Management
- 加密密钥存储在 `appsettings.json` 中（开发环境）
- 生产环境建议使用 Windows DPAPI 或 Azure Key Vault
- 密钥应为32字节（256位）随机生成

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| DPAPI (Data Protection API) | 仅Windows平台，未来可能跨平台需求 |
| RSA非对称加密 | 性能较差，密钥管理复杂，不适合本地存储场景 |
| 明文存储 | 严重安全风险，违反宪章安全要求 |

---

## R2: Refit HTTP 客户端配置

### Decision
使用 **Refit** 生成类型安全的 REST 客户端，配合 `IHttpClientFactory` 管理连接池和重试策略。

### Rationale
1. **类型安全**：编译时检查API接口，减少运行时错误
2. **声明式API**：通过特性定义接口，代码简洁易维护
3. **HttpClientFactory集成**：自动管理连接生命周期，避免socket耗尽
4. **Polly策略支持**：内置重试、超时、熔断等弹性策略
5. **符合宪章要求**：Constitution明确要求使用Refit

### Implementation Details
```csharp
// API接口定义
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

// ABP模块配置
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"] 
            ?? "http://base.publicapi.findong.com";
        
        // 注册Refit客户端
        context.Services.AddRefitClient<IBasePlatformApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(basePlatformUrl))
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(30)));
    }
}
```

### Retry Strategy
- **重试次数**: 3次
- **退避策略**: 指数退避（2^n秒：2s, 4s, 8s）
- **超时时间**: 30秒
- **可重试错误**: 
  - 5xx服务器错误
  - 网络连接失败（HttpRequestException）
  - 超时（TaskCanceledException）

### Error Handling
```csharp
public async Task<Result<LicenseInfoDto>> VerifyAuthCodeAsync(string code)
{
    try
    {
        var response = await _basePlatformApi.GetAuthClientLicenseAsync(
            new LicenseRequestDto 
            { 
                ProductCode = "5000", 
                Code = code 
            });
        
        if (response.Success)
        {
            return Result<LicenseInfoDto>.Success(response.Data);
        }
        else
        {
            _logger.LogWarning("Authorization failed: {Message}", response.Msg);
            return Result<LicenseInfoDto>.Failure(response.Msg);
        }
    }
    catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
        return Result<LicenseInfoDto>.Failure("授权码无效");
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Network error during authorization");
        return Result<LicenseInfoDto>.Failure("网络连接失败，请检查网络设置");
    }
    catch (TaskCanceledException ex)
    {
        _logger.LogError(ex, "Authorization request timeout");
        return Result<LicenseInfoDto>.Failure("请求超时，请重试");
    }
}
```

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| HttpClient直接使用 | 代码冗长，缺少类型安全，不符合宪章要求 |
| RestSharp | 功能重叠，Refit更现代化，与HttpClientFactory集成更好 |
| gRPC | 过度复杂，基础平台提供的是REST API |

---

## R3: Avalonia 应用启动流程设计

### Decision
实现 **自定义启动服务 + 窗口路由** 机制，在 App.xaml.cs 中控制启动流程。

### Rationale
1. **灵活的启动逻辑**：可在启动时检查授权状态，决定显示哪个窗口
2. **符合MVVM模式**：窗口通过ViewModel驱动，业务逻辑与UI分离
3. **可测试性**：启动逻辑封装在服务中，可单独测试
4. **Avalonia最佳实践**：通过ApplicationLifetime控制窗口生命周期

### Implementation Details
```csharp
// 启动服务接口
public interface IStartupService
{
    Task<Window> DetermineStartupWindowAsync();
}

// 启动服务实现
public class StartupService : IStartupService
{
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<Window> DetermineStartupWindowAsync()
    {
        // 检查授权状态
        var license = await _licenseRepository
            .OrderByDescending(l => l.CreationTime)
            .FirstOrDefaultAsync();
        
        if (license == null || license.AuthEndTime < DateTime.Now)
        {
            // 无授权或已过期 → 授权窗口
            return _serviceProvider.GetRequiredService<AuthCodeWindow>();
        }
        else
        {
            // 有效授权 → 登录窗口
            return _serviceProvider.GetRequiredService<LoginWindow>();
        }
    }
}

// App.xaml.cs
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
            // 初始化ABP应用
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
```

### Window Navigation Flow
```
启动应用
    ↓
检查授权状态
    ↓
┌───────────┴───────────┐
│                       │
无授权/已过期          有效授权
│                       │
AuthCodeWindow         LoginWindow
│                       │
输入授权码             输入用户名密码
│                       │
验证成功               登录成功
│                       │
└───────→ AttendedWeighingWindow ←───────┘
           (称重管理主界面)
```

### Window Transition
```csharp
// 授权成功后切换到登录窗口
private async void OnAuthorizationSuccess()
{
    var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
    
    if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        this.Close(); // 关闭授权窗口
    }
}

// 登录成功后切换到主界面
private async void OnLoginSuccess()
{
    var mainWindow = _serviceProvider.GetRequiredService<AttendedWeighingWindow>();
    
    if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        this.Close(); // 关闭登录窗口
    }
}
```

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| 单窗口+多View切换 | 需要保持窗口状态，复杂度高，不符合独立测试要求 |
| WPF Frame导航 | Avalonia不支持Frame，且导航历史管理复杂 |
| ReactiveUI Router | 引入额外依赖，过度工程化 |

---

## R4: 数据库设计与实体映射

### Decision
使用 **ABP Entity Framework Core** 集成，实体继承 `FullAuditedEntity<Guid>`，通过 EF Core Migrations 管理架构变更。

### Rationale
1. **审计追踪**：FullAuditedEntity提供创建/修改/删除时间和用户信息
2. **软删除支持**：IsDeleted字段支持逻辑删除，保留数据历史
3. **Guid主键**：分布式友好，避免自增ID竞争
4. **迁移管理**：Migrations提供版本化架构变更，支持升级和回滚
5. **符合宪章要求**：遵循ABP Domain实体基类约定

### Entity Design
```csharp
// LicenseInfo 实体
public class LicenseInfo : FullAuditedEntity<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid? AuthToken { get; set; }
    public DateTime AuthEndTime { get; set; }
    public string MachineCode { get; set; }
}

// UserCredential 实体
public class UserCredential : FullAuditedEntity<Guid>
{
    public string UserName { get; set; }
    public string EncryptedPassword { get; set; }
    public bool RememberPassword { get; set; }
    public Guid ProjectId { get; set; }
}

// UserSession 实体
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
}
```

### DbContext Configuration
```csharp
public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
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
        
        builder.Entity<LicenseInfo>(b =>
        {
            b.ToTable("LicenseInfos");
            b.Property(x => x.MachineCode).IsRequired().HasMaxLength(128);
            b.HasIndex(x => x.ProjectId);
        });
        
        builder.Entity<UserCredential>(b =>
        {
            b.ToTable("UserCredentials");
            b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
            b.Property(x => x.EncryptedPassword).IsRequired().HasMaxLength(512);
            b.HasIndex(x => new { x.UserName, x.ProjectId }).IsUnique();
        });
        
        builder.Entity<UserSession>(b =>
        {
            b.ToTable("UserSessions");
            b.Property(x => x.UserName).IsRequired().HasMaxLength(64);
            b.Property(x => x.Token).IsRequired().HasMaxLength(512);
            b.HasIndex(x => x.ProjectId);
        });
    }
}
```

### Migration Strategy
```bash
# 创建新迁移
dotnet ef migrations add AddAuthenticationEntities -p MaterialClient.Common

# 应用迁移
dotnet ef database update -p MaterialClient.Common

# 首次启动自动应用迁移
public class MaterialClientCommonModule : AbpModule
{
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.ServiceProvider
            .GetRequiredService<MaterialClientDbContext>()
            .Database.MigrateAsync();
    }
}
```

### Data Isolation by ProjectId
- 所有实体包含 `ProjectId` 字段
- 当项目切换时（授权码对应的ProjectId变更），清除旧项目数据：
```csharp
public async Task ClearProjectDataAsync(Guid newProjectId)
{
    // 清除旧项目的凭证
    var oldCredentials = await _credentialRepository
        .GetListAsync(c => c.ProjectId != newProjectId);
    await _credentialRepository.DeleteManyAsync(oldCredentials);
    
    // 清除旧项目的会话
    var oldSessions = await _sessionRepository
        .GetListAsync(s => s.ProjectId != newProjectId);
    await _sessionRepository.DeleteManyAsync(oldSessions);
}
```

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| Entity继承Entity<Guid> | 缺少审计字段，不符合业务需求 |
| int自增主键 | 分布式场景不友好，Guid更通用 |
| 单表存储所有配置 | 结构混乱，查询性能差，不符合DDD |
| Dapper + SQL | 缺少变更跟踪，迁移管理复杂，不符合宪章 |

---

## R5: 机器码生成策略

### Decision
使用 **CPU ID + 主板序列号 + MAC地址** 组合生成机器码，通过 SHA256 哈希确保唯一性。

### Rationale
1. **硬件唯一性**：CPU和主板硬件标识在设备生命周期内不变
2. **防止授权转移**：机器码绑定硬件，无法轻易复制到其他设备
3. **跨重装系统**：不依赖操作系统信息，系统重装后机器码保持一致
4. **哈希保护隐私**：不暴露原始硬件信息，仅保存哈希值

### Implementation Details
```csharp
using System.Management; // NuGet: System.Management.Compatibility
using System.Security.Cryptography;

public class MachineCodeService
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
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessorId FROM Win32_Processor");
        
        foreach (var obj in searcher.Get())
        {
            return obj["ProcessorId"]?.ToString() ?? string.Empty;
        }
        
        return string.Empty;
    }
    
    private string GetBoardSerialNumber()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT SerialNumber FROM Win32_BaseBoard");
        
        foreach (var obj in searcher.Get())
        {
            return obj["SerialNumber"]?.ToString() ?? string.Empty;
        }
        
        return string.Empty;
    }
    
    private string GetFirstMacAddress()
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
        
        return string.Empty;
    }
}
```

### Error Handling
- 如果某个硬件信息获取失败，使用空字符串
- 至少保证一个硬件信息可用（通常MAC地址最可靠）
- 记录日志但不阻止授权流程

### Windows Compatibility
- 需要 NuGet 包：`System.Management` (版本 9.0.0)
- 仅支持 Windows 平台（符合项目约束）
- 需要适当的WMI权限（通常默认可用）

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| Windows Product ID | 系统重装后变化，不稳定 |
| 硬盘序列号 | 更换硬盘后机器码变化，影响用户体验 |
| 仅MAC地址 | 网卡更换或虚拟网卡可能导致变化 |
| GUID生成 | 无法绑定硬件，失去授权保护意义 |

---

## R6: UI窗口设计参考

### Decision
基于 `/assets/AuthCode.xaml` 和 `/assets/Login.xaml` 的设计参考，使用 Avalonia UI 重新实现。

### Rationale
1. **设计一致性**：保持与参考设计相同的视觉风格和交互流程
2. **Avalonia兼容性**：将WPF/XAML语法转换为Avalonia AXAML
3. **响应式布局**：适配不同屏幕分辨率
4. **MVVM绑定**：所有UI逻辑通过ViewModel驱动，支持数据绑定和命令

### Key Conversion Points
| WPF/XAML | Avalonia AXAML | Notes |
|----------|----------------|-------|
| `Window` | `Window` | 基本一致，命名空间不同 |
| `TextBox.Text` | `TextBox.Text` | 完全兼容 |
| `PasswordBox` | `TextBox PasswordChar="*"` | Avalonia使用TextBox+PasswordChar |
| `Button.Click` | `Button.Command` | 优先使用Command绑定 |
| `MessageBox.Show` | `Window.ShowDialog` | Avalonia无MessageBox，需自定义 |

### AuthCodeWindow Layout
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MaterialClient.ViewModels"
        x:DataType="vm:AuthCodeWindowViewModel"
        Title="软件授权" Width="500" Height="300"
        WindowStartupLocation="CenterScreen"
        CanResize="False">
    
    <Window.DataContext>
        <vm:AuthCodeWindowViewModel />
    </Window.DataContext>
    
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

### LoginWindow Layout
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MaterialClient.ViewModels"
        x:DataType="vm:LoginWindowViewModel"
        Title="用户登录" Width="600" Height="400"
        WindowStartupLocation="CenterScreen"
        CanResize="False">
    
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="250"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        
        <!-- 左侧Logo区域 -->
        <Border Grid.Column="0" Background="#2196F3">
            <StackPanel VerticalAlignment="Center">
                <Image Source="/Assets/Indexlogo.png" Width="150" Margin="20"/>
                <TextBlock Text="称重管理系统" Foreground="White" 
                           FontSize="16" TextAlignment="Center"/>
            </StackPanel>
        </Border>
        
        <!-- 右侧登录表单 -->
        <StackPanel Grid.Column="1" Margin="40" VerticalAlignment="Center">
            <TextBlock Text="用户登录" FontSize="20" FontWeight="Bold" 
                       Margin="0,0,0,30"/>
            
            <TextBox Text="{Binding UserName}" 
                     Watermark="请输入用户名（手机号）"
                     FontSize="14" Padding="10" Margin="0,0,0,15"/>
            
            <TextBox Text="{Binding Password}" PasswordChar="●"
                     Watermark="请输入密码"
                     FontSize="14" Padding="10" Margin="0,0,0,15"/>
            
            <CheckBox Content="记住密码" 
                      IsChecked="{Binding RememberPassword}"
                      Margin="0,0,0,20"/>
            
            <Button Content="登录" Command="{Binding LoginCommand}"
                    HorizontalAlignment="Stretch" Height="40"
                    FontSize="14"/>
            
            <TextBlock Text="{Binding ErrorMessage}" 
                       Foreground="Red" FontSize="12"
                       Margin="0,10,0,0" TextAlignment="Center"
                       IsVisible="{Binding HasError}"/>
        </StackPanel>
    </Grid>
</Window>
```

### ViewModels with CommunityToolkit.Mvvm
```csharp
public partial class AuthCodeWindowViewModel : ObservableObject
{
    private readonly IAuthorizationService _authService;
    
    [ObservableProperty]
    private string _authCode = string.Empty;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool _hasError;
    
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
        
        var result = await _authService.VerifyAuthCodeAsync(AuthCode);
        
        if (result.IsSuccess)
        {
            // 切换到登录窗口
            NavigateToLoginWindow();
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
            HasError = true;
        }
    }
}
```

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| 完全自定义设计 | 需要额外的UI/UX设计工作，延长开发周期 |
| Material Design | 与现有设计风格不一致 |
| WPF迁移到Avalonia工具 | 自动转换质量不高，手工调整工作量相当 |

---

## R7: 测试策略

### Decision
采用 **三层测试金字塔**：单元测试（服务逻辑） + 集成测试（数据库+API） + BDD测试（用户场景）。

### Rationale
1. **快速反馈**：单元测试快速验证核心逻辑
2. **真实环境**：集成测试使用真实数据库和HTTP客户端
3. **需求可追溯**：BDD测试直接映射功能需求
4. **符合宪章**：TDD强制要求，Red-Green-Refactor循环

### Test Pyramid
```
        /\
       /  \  BDD Tests (Reqnroll)
      /    \  - Authorization.feature
     /------\  - Authentication.feature
    /        \
   / Integration Tests (ABP)
  /  - AuthorizationServiceTests
 /   - AuthenticationServiceTests
/____________________________\
      Unit Tests
    - PasswordEncryptionServiceTests
    - MachineCodeServiceTests
```

### BDD Feature Example
```gherkin
# Authorization.feature
Feature: 软件授权验证
    作为一个新用户
    我想输入授权码激活软件
    以便能够使用系统功能

Scenario: 首次使用输入有效授权码
    Given 本地数据库中没有授权信息
    When 用户启动应用程序
    Then 系统显示授权码输入窗口
    When 用户输入有效授权码 "1234"
    And 点击确认按钮
    Then 系统调用基础平台API验证授权码
    And 保存授权信息到数据库
    And 进入登录页面

Scenario: 输入无效授权码
    Given 本地数据库中没有授权信息
    When 用户在授权窗口输入无效授权码 "invalid"
    And 点击确认按钮
    Then 系统显示错误提示 "授权码无效"
    And 不进入登录页面

Scenario: 授权已过期
    Given 本地数据库中存在已过期的授权信息
    When 用户启动应用程序
    Then 系统检测到授权已过期
    And 显示授权码输入窗口
```

### Integration Test Example
```csharp
public class AuthorizationServiceTests : MaterialClientDomainTestBase
{
    private readonly IAuthorizationService _authService;
    private readonly IRepository<LicenseInfo, Guid> _licenseRepo;
    
    public AuthorizationServiceTests()
    {
        _authService = GetRequiredService<IAuthorizationService>();
        _licenseRepo = GetRequiredService<IRepository<LicenseInfo, Guid>>();
    }
    
    [Fact]
    public async Task VerifyAuthCode_ValidCode_ShouldSaveLicenseInfo()
    {
        // Arrange
        var authCode = "test1234";
        
        // Act
        var result = await _authService.VerifyAuthCodeAsync(authCode);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var license = await _licenseRepo
            .FirstOrDefaultAsync(l => l.MachineCode == _machineCode);
        
        license.ShouldNotBeNull();
        license.AuthEndTime.ShouldBeGreaterThan(DateTime.Now);
    }
    
    [Fact]
    public async Task CheckLicenseStatus_ExpiredLicense_ShouldReturnFalse()
    {
        // Arrange
        await WithUnitOfWorkAsync(async () =>
        {
            await _licenseRepo.InsertAsync(new LicenseInfo
            {
                ProjectId = Guid.NewGuid(),
                AuthEndTime = DateTime.Now.AddDays(-1), // 已过期
                MachineCode = "test-machine"
            });
        });
        
        // Act
        var isValid = await _authService.CheckLicenseStatusAsync();
        
        // Assert
        isValid.ShouldBeFalse();
    }
}
```

### Unit Test Example
```csharp
public class PasswordEncryptionServiceTests
{
    private readonly PasswordEncryptionService _service;
    
    public PasswordEncryptionServiceTests()
    {
        var key = new byte[32]; // 256-bit key
        RandomNumberGenerator.Fill(key);
        _service = new PasswordEncryptionService(key);
    }
    
    [Fact]
    public void Encrypt_PlainText_ShouldReturnBase64String()
    {
        // Arrange
        var plainText = "MyPassword123";
        
        // Act
        var encrypted = _service.Encrypt(plainText);
        
        // Assert
        encrypted.ShouldNotBeNullOrEmpty();
        encrypted.ShouldNotBe(plainText);
        Convert.FromBase64String(encrypted); // Should not throw
    }
    
    [Fact]
    public void Decrypt_EncryptedText_ShouldReturnOriginalPlainText()
    {
        // Arrange
        var plainText = "MyPassword123";
        var encrypted = _service.Encrypt(plainText);
        
        // Act
        var decrypted = _service.Decrypt(encrypted);
        
        // Assert
        decrypted.ShouldBe(plainText);
    }
    
    [Fact]
    public void Encrypt_SamePlainText_ShouldGenerateDifferentCipherText()
    {
        // Arrange
        var plainText = "MyPassword123";
        
        // Act
        var encrypted1 = _service.Encrypt(plainText);
        var encrypted2 = _service.Encrypt(plainText);
        
        // Assert
        encrypted1.ShouldNotBe(encrypted2); // 不同的IV
    }
}
```

### Test Execution Order
1. **Red**: 编写失败的测试（功能尚未实现）
2. **Green**: 实现最少代码使测试通过
3. **Refactor**: 重构代码保持测试通过
4. **User Approval**: 用户审查测试场景和实现

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| 仅单元测试 | 无法验证集成点（数据库、HTTP调用） |
| 仅集成测试 | 运行慢，反馈周期长，调试困难 |
| 手工测试 | 不可重复，不符合TDD要求 |
| E2E UI测试 | 脆弱性高，维护成本大，不适合初期 |

---

## Summary Table

| 主题 | 决策 | 关键依赖 |
|------|------|----------|
| 密码加密 | AES-256-CBC | System.Security.Cryptography |
| HTTP客户端 | Refit + HttpClientFactory | Refit 8.0.0, Polly |
| 启动流程 | 自定义StartupService | Avalonia ApplicationLifetime |
| 数据库设计 | ABP EF Core + Migrations | Volo.Abp.EntityFrameworkCore.Sqlite 9.3.6 |
| 机器码生成 | CPU+Board+MAC哈希 | System.Management |
| UI设计 | 基于参考设计的Avalonia实现 | Avalonia 11.3.6 |
| 测试策略 | 单元+集成+BDD三层 | Reqnroll.NUnit, ABP TestBase |

---

**Status**: ✅ All research completed  
**Next Phase**: Phase 1 - Design (data-model.md, contracts/, quickstart.md)

