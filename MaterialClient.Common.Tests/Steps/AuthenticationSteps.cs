using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reqnroll;
using Shouldly;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Tests.Steps;

/// <summary>
/// 用户认证步骤定义
/// </summary>
[Binding]
public class AuthenticationSteps : MaterialClientTestBase<MaterialClientCommonModule>
{
    private IAuthenticationService? _authService;
    private ILicenseService? _licenseService;
    private IBasePlatformApi? _mockApi;
    
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe;
    private bool _loginSuccessful;
    private string _errorMessage = string.Empty;
    private UserSession? _currentSession;
    private UserCredential? _savedCredential;
    
    public AuthenticationSteps()
    {
    }

    [BeforeScenario]
    public void SetupServices()
    {
        _authService = GetRequiredService<IAuthenticationService>();
        _licenseService = GetRequiredService<ILicenseService>();
        
        // Get the mock API that was registered in the test module
        _mockApi = GetRequiredService<IBasePlatformApi>();
        
        // Reset all mocks for this scenario
        _mockApi.ClearReceivedCalls();
    }

    #region Given Steps

    [Given("系统已完成授权激活")]
    public async Task GivenSystemIsAuthorized()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            
            // Create a valid license
            var license = new LicenseInfo(
                id: Guid.NewGuid(),
                projectId: Guid.NewGuid(),
                authToken: Guid.NewGuid(),
                authEndTime: DateTime.UtcNow.AddMonths(6), // Valid for 6 months
                machineCode: "test-machine-code"
            );
            
            await dbContext.LicenseInfos.AddAsync(license);
            await dbContext.SaveChangesAsync();
        });
    }

    [Given("授权未过期")]
    public void GivenAuthorizationNotExpired()
    {
        // Already handled in GivenSystemIsAuthorized
    }

    [Given("用户在登录页面")]
    public void GivenUserIsOnLoginPage()
    {
        // UI state - no action needed in integration test
    }

    [Given("用户已成功登录")]
    public async Task GivenUserHasLoggedIn()
    {
        await GivenSystemIsAuthorized();
        
        // Setup mock API response
        _mockApi!.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
        {
            Success = true,
            Code = 0,
            Msg = "成功",
            Data = new LoginUserDto
            {
                UserId = 1,
                UserName = "testuser",
                Token = "test-access-token",
                TrueName = "测试用户",
                ProductName = "测试产品",
                CoName = "测试公司",
                Url = "http://test.com"
            }
        });
        
        await _authService!.LoginAsync("testuser", "Test@123", true);
    }

    [Given("用户会话存在于数据库")]
    public void GivenUserSessionExistsInDatabase()
    {
        // Already handled in GivenUserHasLoggedIn
    }

    [Given(@"用户会话的最后活动时间是(\d+)小时前")]
    public async Task GivenUserSessionLastActivityTime(int hoursAgo)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            
            var session = await dbContext.UserSessions.FindAsync((long)1);
            if (session != null)
            {
                session.LastActivityTime = DateTime.UtcNow.AddHours(-hoursAgo);
                await dbContext.SaveChangesAsync();
            }
        });
    }

    [Given("之前有保存的凭证")]
    public async Task GivenSavedCredentialsExist()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            
            // Get the license first
            var license = await dbContext.LicenseInfos.FirstAsync();
            
            var encryptionService = GetRequiredService<IPasswordEncryptionService>();
            var credential = new UserCredential(
                id: Guid.NewGuid(),
                projectId: license.ProjectId,
                username: "olduser",
                encryptedPassword: encryptionService.Encrypt("OldPass@123")
            );
            
            await dbContext.UserCredentials.AddAsync(credential);
            await dbContext.SaveChangesAsync();
        });
    }

    #endregion

    #region When Steps

    [When(@"用户输入用户名 ""([^""]*)"" 和密码 ""([^""]*)""")]
    public void WhenUserEntersUsernameAndPassword(string username, string password)
    {
        _username = username;
        _password = password;
    }

    [When(@"用户勾选""([^""]*)""选项")]
    public void WhenUserChecksOption(string option)
    {
        if (option == "记住密码")
        {
            _rememberMe = true;
        }
    }

    [When(@"用户不勾选""([^""]*)""选项")]
    public void WhenUserDoesNotCheckOption(string option)
    {
        if (option == "记住密码")
        {
            _rememberMe = false;
        }
    }

    [When("用户点击登录按钮")]
    public async Task WhenUserClicksLoginButton()
    {
        try
        {
            // Validate inputs first
            if (string.IsNullOrEmpty(_username))
            {
                _errorMessage = "用户名不能为空";
                return;
            }
            
            if (string.IsNullOrEmpty(_password))
            {
                _errorMessage = "密码不能为空";
                return;
            }
            
            // Setup mock API response
            if (_password == "wrongpassword")
            {
                _mockApi!.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
                {
                    Success = false,
                    Code = -1,
                    Msg = "用户名或密码错误",
                    Data = null!
                });
            }
            else
            {
                _mockApi!.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
                {
                    Success = true,
                    Code = 0,
                    Msg = "成功",
                    Data = new LoginUserDto
                    {
                        UserId = 1,
                        UserName = _username,
                        Token = "test-access-token",
                        TrueName = "测试用户",
                        ProductName = "测试产品",
                        CoName = "测试公司",
                        Url = "http://test.com"
                    }
                });
            }
            
            var result = await _authService!.LoginAsync(_username, _password, _rememberMe);
            _loginSuccessful = result != null;
            
            if (!_loginSuccessful)
            {
                _errorMessage = "登录失败";
            }
        }
        catch (Exception ex)
        {
            _loginSuccessful = false;
            _errorMessage = ex.Message;
        }
    }

    [When("检查是否有活跃会话")]
    public async Task WhenCheckingForActiveSession()
    {
        var hasSession = await _authService!.HasActiveSessionAsync();
        
        if (hasSession)
        {
            _currentSession = await WithUnitOfWorkAsync(async () =>
            {
                var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
                return await dbContext.UserSessions.FindAsync((long)1);
            });
        }
    }

    #endregion

    #region Then Steps

    [Then("登录应该成功")]
    public void ThenLoginShouldSucceed()
    {
        _loginSuccessful.ShouldBeTrue("Login should succeed");
        _errorMessage.ShouldBeNullOrEmpty();
    }

    [Then("登录应该失败")]
    public void ThenLoginShouldFail()
    {
        _loginSuccessful.ShouldBeFalse("Login should fail");
    }

    [Then(@"应该显示错误消息 ""([^""]*)""")]
    public void ThenShouldShowErrorMessage(string expectedMessage)
    {
        _errorMessage.ShouldContain(expectedMessage);
    }

    [Then(@"应该显示验证错误 ""([^""]*)""")]
    public void ThenShouldShowValidationError(string expectedError)
    {
        _errorMessage.ShouldBe(expectedError);
    }

    [Then("用户会话应该被创建")]
    public async Task ThenUserSessionShouldBeCreated()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var sessions = dbContext.UserSessions.ToList();
            
            sessions.ShouldNotBeEmpty();
            var session = sessions.First();
            session.AccessToken.ShouldNotBeNullOrEmpty();
        });
    }

    [Then("用户凭证应该被保存")]
    public async Task ThenUserCredentialsShouldBeSaved()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credential = dbContext.UserCredentials.FirstOrDefault();
            
            credential.ShouldNotBeNull();
            credential.Username.ShouldBe(_username);
            credential.EncryptedPassword.ShouldNotBeNullOrEmpty();
        });
    }

    [Then("用户凭证应该不被保存")]
    public async Task ThenUserCredentialsShouldNotBeSaved()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credentials = dbContext.UserCredentials.ToList();
            
            credentials.ShouldBeEmpty();
        });
    }

    [Then("之前保存的凭证应该被清除")]
    public async Task ThenPreviousCredentialsShouldBeCleared()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credentials = dbContext.UserCredentials.ToList();
            
            // Should either be empty or not contain the old credentials
            var oldCredential = credentials.FirstOrDefault(c => c.Username == "olduser");
            oldCredential.ShouldBeNull();
        });
    }

    [Then("保存的凭证应该被清除")]
    public async Task ThenSavedCredentialsShouldBeCleared()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credentials = dbContext.UserCredentials.ToList();
            
            credentials.ShouldBeEmpty();
        });
    }

    [Then("用户应该进入主界面")]
    public void ThenUserShouldEnterMainWindow()
    {
        // UI state - verified by successful login
        _loginSuccessful.ShouldBeTrue();
    }

    [Then("不应该调用登录API")]
    public void ThenShouldNotCallLoginApi()
    {
        _mockApi.DidNotReceive().UserLoginAsync(Arg.Any<LoginRequestDto>());
    }

    [Then("应该返回true")]
    public void ThenShouldReturnTrue()
    {
        _currentSession.ShouldNotBeNull();
    }

    [Then("应该返回false")]
    public void ThenShouldReturnFalse()
    {
        _currentSession.ShouldBeNull();
    }

    [Then("应该返回有效的会话信息")]
    public void ThenShouldReturnValidSessionInfo()
    {
        _currentSession.ShouldNotBeNull();
        _currentSession.AccessToken.ShouldNotBeNullOrEmpty();
        _currentSession.Username.ShouldNotBeNullOrEmpty();
    }

    [Then("会话应该被自动清除")]
    public async Task ThenSessionShouldBeAutomaticallyCleared()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var sessions = dbContext.UserSessions.ToList();
            
            sessions.ShouldBeEmpty();
        });
    }

    [Then("密码应该加密存储")]
    public async Task ThenPasswordShouldBeEncrypted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credential = dbContext.UserCredentials.FirstOrDefault();
            
            credential.ShouldNotBeNull();
            credential.EncryptedPassword.ShouldNotBe(_password);
            
            // Verify we can decrypt it
            var encryptionService = GetRequiredService<IPasswordEncryptionService>();
            var decrypted = encryptionService.Decrypt(credential.EncryptedPassword);
            decrypted.ShouldBe(_password);
        });
    }

    [Then("密码可以被正确解密")]
    public async Task ThenPasswordCanBeDecrypted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var credential = dbContext.UserCredentials.FirstOrDefault();
            
            credential.ShouldNotBeNull();
            
            var encryptionService = GetRequiredService<IPasswordEncryptionService>();
            var decrypted = encryptionService.Decrypt(credential.EncryptedPassword);
            decrypted.ShouldBe(_password);
        });
    }

    [Then("应该加载保存的用户名和密码")]
    public async Task ThenShouldLoadSavedCredentials()
    {
        var credential = await _authService!.GetSavedCredentialAsync();
        
        credential.ShouldNotBeNull();
        credential.Value.username.ShouldNotBeNullOrEmpty();
        credential.Value.password.ShouldNotBeNullOrEmpty();
    }

    [Then(@"应该显示友好的错误消息 ""([^""]*)""")]
    public void ThenShouldShowFriendlyErrorMessage(string expectedMessage)
    {
        _errorMessage.ShouldContain(expectedMessage);
    }

    [Then("用户会话应该被清除")]
    public async Task ThenUserSessionShouldBeCleared()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var sessions = dbContext.UserSessions.ToList();
            
            sessions.ShouldBeEmpty();
        });
    }

    #endregion
}

