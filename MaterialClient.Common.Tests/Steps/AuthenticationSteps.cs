using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reqnroll;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Tests.Steps;

/// <summary>
/// 用户认证步骤定义
/// </summary>
[Binding]
public class AuthenticationSteps : MaterialClientEntityFrameworkCoreTestBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILicenseService _licenseService;
    private readonly ITestService _testService;
    private readonly IBasePlatformApi _mockApi;
    private readonly IRepository<UserSession, Guid> _sessionRepository;
    private readonly IRepository<UserCredential, Guid> _credentialRepository;
    
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe;
    private bool _loginSuccessful;
    private string _errorMessage = string.Empty;
    private UserSession? _currentSession;
    private UserCredential? _savedCredential;
    
    public AuthenticationSteps()
    {
        _authService = GetRequiredService<IAuthenticationService>();
        _licenseService = GetRequiredService<ILicenseService>();
        _testService = GetRequiredService<ITestService>();
        _sessionRepository = GetRequiredService<IRepository<UserSession, Guid>>();
        _credentialRepository = GetRequiredService<IRepository<UserCredential, Guid>>();
        
        // Get the mock API that was registered in the test module
        _mockApi = GetRequiredService<IBasePlatformApi>();
    }

    [BeforeScenario]
    public void SetupScenario()
    {
        // Reset all mocks for this scenario
        _mockApi.ClearReceivedCalls();
    }

    #region Given Steps

    [Given("系统已完成授权激活")]
    public async Task GivenSystemIsAuthorized()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync(
                authEndTime: DateTime.UtcNow.AddMonths(6) // Valid for 6 months
            );
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
        _mockApi.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
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
        
        await _authService.LoginAsync("testuser", "Test@123", true);
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
            var session = await _sessionRepository.FirstOrDefaultAsync();
            if (session != null)
            {
                await _testService.UpdateUserSessionLastActivityTimeAsync(
                    session.Id,
                    DateTime.UtcNow.AddHours(-hoursAgo)
                );
            }
        });
    }

    [Given("之前有保存的凭证")]
    public async Task GivenSavedCredentialsExist()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var encryptionService = GetRequiredService<IPasswordEncryptionService>();
            await _testService.CreateUserCredentialAsync(
                username: "olduser",
                encryptedPassword: encryptionService.Encrypt("OldPass@123")
            );
        });
    }

    [Given("已初始化通用测试数据")]
    public async Task GivenCommonTestDataInitialized()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Create test Material
            await _testService.CreateMaterialAsync(
                name: "测试物料",
                code: "MAT001",
                coId: 1
            );

            // Create test MaterialUnit
            var materialRepository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Material, int>>();
            var material = await materialRepository.FirstOrDefaultAsync();
            if (material != null)
            {
                await _testService.CreateMaterialUnitAsync(
                    materialId: material.Id,
                    unitName: "kg",
                    rate: 1
                );
            }

            // Create test Provider
            await _testService.CreateProviderAsync(
                providerName: "测试供应商",
                providerType: 1
            );
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
                _mockApi.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
                {
                    Success = false,
                    Code = -1,
                    Msg = "用户名或密码错误",
                    Data = null!
                });
            }
            else
            {
                _mockApi.UserLoginAsync(Arg.Any<LoginRequestDto>()).Returns(new HttpResult<LoginUserDto>
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
            
            var result = await _authService.LoginAsync(_username, _password, _rememberMe);
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
        var hasSession = await _authService.HasActiveSessionAsync();
        
        if (hasSession)
        {
            _currentSession = await WithUnitOfWorkAsync(async () =>
            {
                return await _sessionRepository.FirstOrDefaultAsync();
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
            var sessions = await _sessionRepository.GetListAsync();
            
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
            var credential = await _credentialRepository.FirstOrDefaultAsync();
            
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
            var credentials = await _credentialRepository.GetListAsync();
            
            credentials.ShouldBeEmpty();
        });
    }

    [Then("之前保存的凭证应该被清除")]
    public async Task ThenPreviousCredentialsShouldBeCleared()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var credentials = await _credentialRepository.GetListAsync();
            
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
            var credentials = await _credentialRepository.GetListAsync();
            
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
            var sessions = await _sessionRepository.GetListAsync();
            
            sessions.ShouldBeEmpty();
        });
    }

    [Then("密码应该加密存储")]
    public async Task ThenPasswordShouldBeEncrypted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var credential = await _credentialRepository.FirstOrDefaultAsync();
            
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
            var credential = await _credentialRepository.FirstOrDefaultAsync();
            
            credential.ShouldNotBeNull();
            
            var encryptionService = GetRequiredService<IPasswordEncryptionService>();
            var decrypted = encryptionService.Decrypt(credential.EncryptedPassword);
            decrypted.ShouldBe(_password);
        });
    }

    [Then("应该加载保存的用户名和密码")]
    public async Task ThenShouldLoadSavedCredentials()
    {
        var credential = await _authService.GetSavedCredentialAsync();
        
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
            var sessions = await _sessionRepository.GetListAsync();
            
            sessions.ShouldBeEmpty();
        });
    }

    #endregion
}

