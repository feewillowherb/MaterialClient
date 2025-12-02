using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 用户认证服务接口
/// 负责用户登录、会话管理和凭证管理
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码（明文）</param>
    /// <param name="rememberMe">是否记住密码</param>
    /// <returns>用户会话信息</returns>
    /// <exception cref="Volo.Abp.BusinessException">登录失败</exception>
    Task<UserSession> LoginAsync(string username, string password, bool rememberMe);

    /// <summary>
    /// 测试方法：用户登录（不联网，返回固定有效的会话信息）
    /// </summary>
    /// <param name="username">用户名（测试方法中不进行实际验证）</param>
    /// <param name="password">密码（测试方法中不进行实际验证）</param>
    /// <param name="rememberMe">是否记住密码</param>
    /// <returns>固定的有效会话信息</returns>
    Task<UserSession> LoginTestAsync(string username, string password, bool rememberMe);

    /// <summary>
    /// 获取当前用户会话
    /// </summary>
    /// <returns>用户会话信息，如果不存在则返回 null</returns>
    Task<UserSession> GetCurrentSessionAsync();

    /// <summary>
    /// 检查是否有活跃的会话
    /// </summary>
    /// <returns>true 表示有活跃会话，false 表示无会话或会话已过期</returns>
    Task<bool> HasActiveSessionAsync();

    /// <summary>
    /// 登出当前用户
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 获取保存的用户凭证（用于自动登录）
    /// </summary>
    /// <returns>用户名和密码（明文），如果不存在则返回 null</returns>
    Task<(string username, string password)?> GetSavedCredentialAsync();

    /// <summary>
    /// 清除保存的用户凭证
    /// </summary>
    Task ClearSavedCredentialAsync();

    /// <summary>
    /// 更新会话活动时间
    /// </summary>
    Task UpdateSessionActivityAsync();
}

/// <summary>
/// 用户认证服务实现
/// </summary>
[AutoConstructor]
public partial class AuthenticationService : DomainService, IAuthenticationService
{
    private readonly IBasePlatformApi _basePlatformApi;
    private readonly ILicenseService _licenseService;
    private readonly IPasswordEncryptionService _passwordEncryptionService;
    private readonly IRepository<UserCredential, Guid> _credentialRepository;
    private readonly IRepository<UserSession, Guid> _sessionRepository;

    [UnitOfWork]
    public async Task<UserSession> LoginAsync(string username, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new BusinessException("AUTH:EMPTY_USERNAME", "用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException("AUTH:EMPTY_PASSWORD", "密码不能为空");
        }

        // Get current license
        var license = await _licenseService.GetCurrentLicenseAsync();
        if (license == null)
        {
            throw new BusinessException("AUTH:NO_LICENSE", "未找到授权信息，请先进行授权激活");
        }

        if (license.IsExpired)
        {
            throw new BusinessException("AUTH:LICENSE_EXPIRED", "授权已过期，请重新激活");
        }

        // Call base platform API to login
        var request = new LoginRequestDto
        {
            UserName = username,
            UserPwd = password,
            ProId = license.ProjectId.ToString()
        };

        HttpResult<LoginUserDto> response;
        try
        {
            response = await _basePlatformApi.UserLoginAsync(request);
        }
        catch (Exception ex)
        {
            throw new BusinessException("AUTH:API_ERROR", "无法连接到登录服务器，请检查网络连接", innerException: ex);
        }

        // Check response
        if (response == null || !response.Success || response.Data == null)
        {
            // Clear saved credential on failed login
            await ClearSavedCredentialAsync();

            var errorMsg = response?.Msg ?? "用户名或密码错误";
            throw new BusinessException("AUTH:LOGIN_FAILED", errorMsg);
        }

        var loginData = response.Data;

        // Save or update user credential if rememberMe is true
        if (rememberMe)
        {
            var encryptedPassword = _passwordEncryptionService.Encrypt(password);
            var existingCredential = await _credentialRepository.FirstOrDefaultAsync();

            if (existingCredential != null)
            {
                existingCredential.UpdateCredentials(username, encryptedPassword);
                await _credentialRepository.UpdateAsync(existingCredential);
            }
            else
            {
                var credential = new UserCredential(
                    Guid.NewGuid(),
                    license.ProjectId,
                    username,
                    encryptedPassword
                );
                await _credentialRepository.InsertAsync(credential);
            }
        }
        else
        {
            // Clear saved credential if rememberMe is false
            await ClearSavedCredentialAsync();
        }

        // Delete existing session (only one session per project)
        var existingSession = await _sessionRepository.FirstOrDefaultAsync();
        if (existingSession != null)
        {
            await _sessionRepository.DeleteAsync(existingSession);
        }

        // Create new session
        var session = new UserSession(
            Guid.NewGuid(),
            license.ProjectId,
            loginData.UserId,
            loginData.UserName,
            loginData.TrueName,
            loginData.ClientId,
            loginData.Token,
            loginData.IsAdmin,
            loginData.IsCompany,
            loginData.ProductType,
            loginData.FromProductId,
            loginData.ProductId,
            loginData.ProductName,
            loginData.CoId,
            loginData.CoName,
            loginData.Url,
            loginData.AuthEndTime
        );

        await _sessionRepository.InsertAsync(session);

        return session;
    }

    /// <summary>
    /// 测试方法：用户登录（不联网，返回固定有效的会话信息）
    /// 仅用于测试阶段，总是返回一个固定的有效会话信息
    /// </summary>
    /// <param name="username">用户名（测试方法中不进行实际验证）</param>
    /// <param name="password">密码（测试方法中不进行实际验证）</param>
    /// <param name="rememberMe">是否记住密码</param>
    /// <returns>固定的有效会话信息</returns>
    [UnitOfWork]
    public async Task<UserSession> LoginTestAsync(string username, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new BusinessException("AUTH:EMPTY_USERNAME", "用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException("AUTH:EMPTY_PASSWORD", "密码不能为空");
        }

        // 固定的测试 ProjectId，与 VerifyAuthorizationCodeTestAsync 保持一致
        var testProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // 获取或创建授权信息（确保外键约束满足）
        var license = await _licenseService.GetCurrentLicenseAsync();
        if (license == null)
        {
            // 如果不存在授权信息，提示用户先进行授权验证
            // 这里不自动创建，而是要求用户先调用 VerifyAuthorizationCodeTestAsync
            throw new BusinessException("AUTH:NO_LICENSE", "未找到授权信息，请先进行授权激活");
        }

        // 确保使用固定的测试 ProjectId（与 VerifyAuthorizationCodeTestAsync 一致）
        var projectId = testProjectId;

        // 保存或更新用户凭证（如果需要记住密码）
        if (rememberMe)
        {
            var encryptedPassword = _passwordEncryptionService.Encrypt(password);
            var existingCredential = await _credentialRepository.FirstOrDefaultAsync();

            if (existingCredential != null)
            {
                existingCredential.UpdateCredentials(username, encryptedPassword);
                await _credentialRepository.UpdateAsync(existingCredential);
            }
            else
            {
                var credential = new UserCredential(
                    Guid.NewGuid(),
                    projectId,
                    username,
                    encryptedPassword
                );
                await _credentialRepository.InsertAsync(credential);
            }
        }
        else
        {
            await ClearSavedCredentialAsync();
        }

        // 删除已存在的会话（每个项目只有一个会话）
        var existingSession = await _sessionRepository.FirstOrDefaultAsync();
        if (existingSession != null)
        {
            await _sessionRepository.DeleteAsync(existingSession);
        }

        // 创建固定的测试会话信息
        var testUserId = 10001L; // 固定的测试用户ID
        var testTrueName = "测试用户"; // 固定的真实姓名
        var testClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"); // 固定的客户端ID
        var testAccessToken = "TEST_ACCESS_TOKEN_" + Guid.NewGuid().ToString(); // 固定的访问令牌模式
        var testIsAdmin = true; // 管理员权限
        var testIsCompany = false; // 不是公司账号
        var testProductType = 1; // 产品类型
        var testFromProductId = 1000L; // 来源产品ID
        var testProductId = 2000L; // 产品ID
        var testProductName = "物资管理系统"; // 产品名称
        var testCompanyId = 1; // 公司ID
        var testCompanyName = "测试公司"; // 公司名称
        var testApiUrl = "http://localhost:5000"; // API URL
        var testAuthEndTime = DateTime.Now.AddYears(1); // 授权结束时间（1年后）

        // 创建新的测试会话，使用固定的测试 ProjectId
        var session = new UserSession(
            Guid.NewGuid(),
            projectId, // 使用固定的测试 ProjectId
            testUserId,
            username, // 使用输入的用户名
            testTrueName,
            testClientId,
            testAccessToken,
            testIsAdmin,
            testIsCompany,
            testProductType,
            testFromProductId,
            testProductId,
            testProductName,
            testCompanyId,
            testCompanyName,
            testApiUrl,
            testAuthEndTime
        );

        await _sessionRepository.InsertAsync(session);

        return session;
    }

    public async Task<UserSession> GetCurrentSessionAsync()
    {
        return await _sessionRepository.FirstOrDefaultAsync();
    }

    [UnitOfWork]
    public async Task<bool> HasActiveSessionAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session == null)
        {
            return false;
        }

        // Check if session is expired (24 hours of inactivity)
        if (session.IsExpired)
        {
            await LogoutAsync();
            return false;
        }

        return true;
    }

    [UnitOfWork]
    public async Task LogoutAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session != null)
        {
            await _sessionRepository.DeleteAsync(session);
        }
    }

    [UnitOfWork]
    public async Task<(string username, string password)?> GetSavedCredentialAsync()
    {
        var credential = await _credentialRepository.FirstOrDefaultAsync();
        if (credential == null)
        {
            return null;
        }

        try
        {
            var decryptedPassword = _passwordEncryptionService.Decrypt(credential.EncryptedPassword);
            return (credential.Username, decryptedPassword);
        }
        catch
        {
            // If decryption fails, clear the credential
            await ClearSavedCredentialAsync();
            return null;
        }
    }

    [UnitOfWork]
    public async Task ClearSavedCredentialAsync()
    {
        var credential = await _credentialRepository.FirstOrDefaultAsync();
        if (credential != null)
        {
            await _credentialRepository.DeleteAsync(credential);
        }
    }

    [UnitOfWork]
    public async Task UpdateSessionActivityAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session != null)
        {
            session.UpdateActivity();
            await _sessionRepository.UpdateAsync(session);
        }
    }
}