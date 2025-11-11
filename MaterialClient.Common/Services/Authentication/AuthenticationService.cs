using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 用户认证服务实现
/// </summary>
public class AuthenticationService : IAuthenticationService, ITransientDependency
{
    private readonly IBasePlatformApi _basePlatformApi;
    private readonly ILicenseService _licenseService;
    private readonly IPasswordEncryptionService _passwordEncryptionService;
    private readonly IRepository<UserCredential, Guid> _credentialRepository;
    private readonly IRepository<UserSession, Guid> _sessionRepository;

    public AuthenticationService(
        IBasePlatformApi basePlatformApi,
        ILicenseService licenseService,
        IPasswordEncryptionService passwordEncryptionService,
        IRepository<UserCredential, Guid> credentialRepository,
        IRepository<UserSession, Guid> sessionRepository)
    {
        _basePlatformApi = basePlatformApi;
        _licenseService = licenseService;
        _passwordEncryptionService = passwordEncryptionService;
        _credentialRepository = credentialRepository;
        _sessionRepository = sessionRepository;
    }

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

    public async Task<UserSession> GetCurrentSessionAsync()
    {
        return await _sessionRepository.FirstOrDefaultAsync();
    }

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

    public async Task LogoutAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session != null)
        {
            await _sessionRepository.DeleteAsync(session);
        }
    }

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

    public async Task ClearSavedCredentialAsync()
    {
        var credential = await _credentialRepository.FirstOrDefaultAsync();
        if (credential != null)
        {
            await _credentialRepository.DeleteAsync(credential);
        }
    }

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

