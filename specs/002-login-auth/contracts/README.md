# API Contracts: 登录和授权

**Feature**: 002-login-auth  
**Date**: 2025-11-07

## Overview

本目录包含登录和授权功能涉及的所有外部API合约定义。采用 OpenAPI 3.0 规范描述基础平台提供的 REST API。

---

## Contract Files

### base-platform-api.yaml
定义基础平台（Base Platform）提供的授权验证和用户登录接口。

**Base URL**: `http://base.publicapi.findong.com` (可在 appsettings.json 配置)  
**Protocol**: HTTP/HTTPS  
**Authentication**: 无（授权接口） / Bearer Token（登录后接口）

---

## API Endpoints Summary

| 端点 | 方法 | 用途 | 认证 |
|------|------|------|------|
| `/api/AuthClientLicense/GetAuthClientLicense` | POST | 验证授权码，获取授权信息 | 无 |
| `/User/UserLogin` | POST | 用户登录，获取Token和用户信息 | 无 |

---

## DTOs and Models

### Request DTOs

#### LicenseRequestDto
```csharp
public class LicenseRequestDto
{
    /// <summary>
    /// 产品代码（固定为"5000"）
    /// </summary>
    public string ProductCode { get; set; }
    
    /// <summary>
    /// 用户输入的授权码
    /// </summary>
    public string Code { get; set; }
}
```

#### LoginRequestDto
```csharp
public class LoginRequestDto
{
    /// <summary>
    /// 用户名（手机号）
    /// </summary>
    public string UserName { get; set; }
    
    /// <summary>
    /// 用户密码
    /// </summary>
    public string UserPwd { get; set; }
    
    /// <summary>
    /// 项目ID
    /// </summary>
    public string ProId { get; set; }
}
```

### Response DTOs

#### HttpResult<T>
所有API响应的统一包装结构：
```csharp
public class HttpResult<T>
{
    /// <summary>
    /// 状态码（0表示成功）
    /// </summary>
    public int Code { get; set; }
    
    /// <summary>
    /// 响应数据
    /// </summary>
    public T Data { get; set; }
    
    /// <summary>
    /// 响应消息
    /// </summary>
    public string Msg { get; set; }
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
}
```

#### LicenseInfoDto
```csharp
public class LicenseInfoDto
{
    /// <summary>
    /// 项目ID
    /// </summary>
    public Guid Proid { get; set; }
    
    /// <summary>
    /// 授权Token
    /// </summary>
    public Guid? AuthToken { get; set; }
    
    /// <summary>
    /// 授权到期时间
    /// </summary>
    public DateTime AuthEndTime { get; set; }
    
    /// <summary>
    /// 机器码
    /// </summary>
    public string MachineCode { get; set; }
}
```

#### LoginUserDto
```csharp
public class LoginUserDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// 用户名（手机号）
    /// </summary>
    public string UserName { get; set; }
    
    /// <summary>
    /// 客户端标识
    /// </summary>
    public Guid ClientId { get; set; }
    
    /// <summary>
    /// 真实姓名
    /// </summary>
    public string TrueName { get; set; }
    
    /// <summary>
    /// 是否管理员
    /// </summary>
    public bool IsAdmin { get; set; }
    
    /// <summary>
    /// 是否企业
    /// </summary>
    public bool IsCompany { get; set; }
    
    /// <summary>
    /// 产品分类（1旧版本，2新版本）
    /// </summary>
    public int ProductType { get; set; }
    
    /// <summary>
    /// 来源产品ID
    /// </summary>
    public long FromProductId { get; set; }
    
    /// <summary>
    /// 产品ID
    /// </summary>
    public long ProductId { get; set; }
    
    /// <summary>
    /// 产品名称
    /// </summary>
    public string ProductName { get; set; }
    
    /// <summary>
    /// 公司ID
    /// </summary>
    public int CoId { get; set; }
    
    /// <summary>
    /// 公司名称
    /// </summary>
    public string CoName { get; set; }
    
    /// <summary>
    /// 产品路径
    /// </summary>
    public string Url { get; set; }
    
    /// <summary>
    /// 登录Token
    /// </summary>
    public string Token { get; set; }
    
    /// <summary>
    /// 授权到期时间
    /// </summary>
    public DateTime? AuthEndTime { get; set; }
}
```

---

## Refit Client Interface

```csharp
using Refit;
using System.Threading;
using System.Threading.Tasks;

namespace MaterialClient.Common.Api
{
    /// <summary>
    /// 基础平台API客户端接口
    /// </summary>
    public interface IBasePlatformApi
    {
        /// <summary>
        /// 获取授权客户端许可证
        /// </summary>
        [Post("/api/AuthClientLicense/GetAuthClientLicense")]
        Task<HttpResult<LicenseInfoDto>> GetAuthClientLicenseAsync(
            [Body] LicenseRequestDto request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 用户登录
        /// </summary>
        [Post("/User/UserLogin")]
        Task<HttpResult<LoginUserDto>> UserLoginAsync(
            [Body] LoginRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
```

---

## Error Handling

### Common Error Codes

| Code | Success | Msg | 说明 |
|------|---------|-----|------|
| 0 | true | "操作成功" | 请求成功 |
| 401 | false | "授权码无效" | 授权码错误或不存在 |
| 402 | false | "授权已过期" | 授权码已过期 |
| 403 | false | "用户名或密码错误" | 登录凭证无效 |
| 500 | false | "服务器内部错误" | 服务端异常 |

### Client-Side Error Handling

```csharp
try
{
    var response = await _basePlatformApi.GetAuthClientLicenseAsync(request);
    
    if (response.Success)
    {
        // 处理成功响应
        return Result<LicenseInfoDto>.Success(response.Data);
    }
    else
    {
        // 处理业务错误
        _logger.LogWarning("Authorization failed: {Message}", response.Msg);
        return Result<LicenseInfoDto>.Failure(response.Msg);
    }
}
catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
{
    // 400 Bad Request
    return Result<LicenseInfoDto>.Failure("请求参数错误");
}
catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    // 401 Unauthorized
    return Result<LicenseInfoDto>.Failure("授权码无效");
}
catch (HttpRequestException ex)
{
    // 网络错误
    _logger.LogError(ex, "Network error during authorization");
    return Result<LicenseInfoDto>.Failure("网络连接失败，请检查网络设置");
}
catch (TaskCanceledException ex)
{
    // 超时
    _logger.LogError(ex, "Authorization request timeout");
    return Result<LicenseInfoDto>.Failure("请求超时，请重试");
}
```

---

## Testing Contracts

### Integration Test Example

```csharp
[Fact]
public async Task GetAuthClientLicense_ValidCode_ShouldReturnLicenseInfo()
{
    // Arrange
    var request = new LicenseRequestDto
    {
        ProductCode = "5000",
        Code = "valid-test-code"
    };
    
    // Act
    var response = await _basePlatformApi.GetAuthClientLicenseAsync(request);
    
    // Assert
    response.ShouldNotBeNull();
    response.Success.ShouldBeTrue();
    response.Data.ShouldNotBeNull();
    response.Data.Proid.ShouldNotBe(Guid.Empty);
    response.Data.AuthEndTime.ShouldBeGreaterThan(DateTime.Now);
}
```

### Mock Setup for Tests

```csharp
// 使用NSubstitute模拟API响应
var mockApi = Substitute.For<IBasePlatformApi>();

mockApi.GetAuthClientLicenseAsync(Arg.Any<LicenseRequestDto>())
    .Returns(Task.FromResult(new HttpResult<LicenseInfoDto>
    {
        Success = true,
        Code = 0,
        Msg = "操作成功",
        Data = new LicenseInfoDto
        {
            Proid = Guid.NewGuid(),
            AuthToken = Guid.NewGuid(),
            AuthEndTime = DateTime.Now.AddYears(1),
            MachineCode = "test-machine-code"
        }
    }));
```

---

## Configuration

### appsettings.json

```json
{
  "BasePlatform": {
    "BaseUrl": "http://base.publicapi.findong.com",
    "Timeout": 30,
    "RetryCount": 3,
    "RetryDelaySeconds": 2
  }
}
```

### ABP Module Configuration

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    var configuration = context.Services.GetConfiguration();
    var basePlatformConfig = configuration.GetSection("BasePlatform");
    
    context.Services.AddRefitClient<IBasePlatformApi>()
        .ConfigureHttpClient(c =>
        {
            c.BaseAddress = new Uri(basePlatformConfig["BaseUrl"]);
            c.Timeout = TimeSpan.FromSeconds(basePlatformConfig.GetValue<int>("Timeout"));
        })
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(
                basePlatformConfig.GetValue<int>("RetryCount"),
                retryAttempt => TimeSpan.FromSeconds(
                    basePlatformConfig.GetValue<int>("RetryDelaySeconds") * Math.Pow(2, retryAttempt - 1)
                )))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(basePlatformConfig.GetValue<int>("Timeout"))));
}
```

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-07 | 1.0.0 | 初始版本：授权验证和用户登录接口 |

---

## References

- [OpenAPI Specification](https://spec.openapis.org/oas/v3.0.3)
- [Refit Documentation](https://github.com/reactiveui/refit)
- [ABP Framework HTTP Client Documentation](https://docs.abp.io/en/abp/latest/API/Dynamic-CSharp-API-Clients)

