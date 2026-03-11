using System;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.UI.ViewModels;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Integration;

/// <summary>
/// Integration tests for LoginWindow with mocked dependencies.
/// Tests data flow from UI to ViewModel to Service.
/// </summary>
public class LoginWindowIntegrationTests
{
    private readonly IAuthenticationService _mockAuthService;

    public LoginWindowIntegrationTests()
    {
        // Setup mock authentication service
        _mockAuthService = Substitute.For<IAuthenticationService>();
    }

    private LoginWindowViewModel CreateViewModel()
    {
        return new LoginWindowViewModel(_mockAuthService);
    }

    private UserSession CreateTestUserSession()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var licenseInfoId = Guid.NewGuid();
        var userId = 1L;
        var username = "admin";
        var trueName = string.Empty;
        var clientId = Guid.NewGuid();
        var accessToken = "test-token";
        var isAdmin = true;
        var isCompany = false;
        var productType = 1;
        var fromProductId = 1L;
        var productId = 1L;
        var productName = "Test";
        var companyId = 1;
        var companyName = "Test";
        var apiUrl = "https://test.com";
        var loginTime = DateTime.Now;

        return new UserSession(id, projectId, licenseInfoId, userId, username, trueName, clientId, accessToken, isAdmin, isCompany, productType, fromProductId, productId, productName, companyId, companyName, apiUrl, loginTime);
    }

    [Fact]
    public void LoginViewModel_WithMockService_CanCreateInstance()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoginViewModel_ValidCredentials_LoginSuccess()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Mock successful authentication
        var userSession = CreateTestUserSession();
        _mockAuthService.LoginAsync("admin", "password123", false)
            .Returns(Task.FromResult(userSession));

        // Act
        viewModel.Username = "admin";
        viewModel.Password = "password123";

        // Execute login command
        var execution = viewModel.LoginCommand.Execute();

        // Wait a bit for async operations to complete
        await Task.Delay(100);

        // Assert
        viewModel.IsLoginSuccessful.ShouldBeTrue();
        viewModel.IsLoggingIn.ShouldBeFalse();
        viewModel.HasError.ShouldBeFalse();
        viewModel.ErrorMessage.ShouldBeEmpty();

        // Verify authentication service was called with correct parameters
        _mockAuthService.Received().LoginAsync("admin", "password123", false);
    }

    [Fact]
    public async Task LoginViewModel_EmptyUsername_ValidationError()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Username = string.Empty;
        viewModel.Password = "password123";
        var execution = viewModel.LoginCommand.Execute();

        // Wait for validation to complete
        await Task.Delay(50);

        // Assert
        viewModel.IsLoginSuccessful.ShouldBeFalse();
        viewModel.IsLoggingIn.ShouldBeFalse();
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldBe("请输入用户名");

        // Verify authentication service was NOT called
        _mockAuthService.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task LoginViewModel_EmptyPassword_ValidationError()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Username = "admin";
        viewModel.Password = string.Empty;
        var execution = viewModel.LoginCommand.Execute();

        // Wait for validation to complete
        await Task.Delay(50);

        // Assert
        viewModel.IsLoginSuccessful.ShouldBeFalse();
        viewModel.IsLoggingIn.ShouldBeFalse();
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldBe("请输入密码");

        // Verify authentication service was NOT called
        _mockAuthService.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void LoginViewModel_RememberMeProperty_ManagesState()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        // Test initial state
        viewModel.RememberMe.ShouldBeFalse();

        // Test state change
        viewModel.RememberMe = true;
        viewModel.RememberMe.ShouldBeTrue();

        // Test state change back
        viewModel.RememberMe = false;
        viewModel.RememberMe.ShouldBeFalse();
    }

    [Fact]
    public void LoginViewModel_ErrorHandling_ClearsErrorMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act: Set an error
        viewModel.HasError = true;
        viewModel.ErrorMessage = "Some error";

        // Assert
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldBe("Some error");

        // Act: Clear error by setting it to empty
        viewModel.ErrorMessage = string.Empty;

        // Assert: Error should be cleared
        viewModel.HasError.ShouldBeFalse();
    }

    [Fact]
    public void LoginViewModel_DataBindings_AreCorrect()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Username = "testuser";
        viewModel.Password = "testpass";

        // Assert
        viewModel.Username.ShouldBe("testuser");
        viewModel.Password.ShouldBe("testpass");
    }
}
