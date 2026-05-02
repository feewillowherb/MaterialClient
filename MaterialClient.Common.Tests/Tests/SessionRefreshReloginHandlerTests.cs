using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
///     Tests for SessionRefreshRequiredEventHandler - validates re-login logic
///     when a SessionRefreshRequiredEto event is received.
/// </summary>
public class SessionRefreshReloginHandlerTests
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<SessionRefreshRequiredEventHandler> _logger;
    private readonly SessionRefreshRequiredEventHandler _handler;

    public SessionRefreshReloginHandlerTests()
    {
        _authService = Substitute.For<IAuthenticationService>();
        _logger = Substitute.For<ILogger<SessionRefreshRequiredEventHandler>>();
        _handler = new SessionRefreshRequiredEventHandler(_logger, _authService);
    }

    [Fact]
    public async Task HandleEventAsync_ShouldCallLoginAsync_WhenSavedCredentialsExist()
    {
        // Arrange
        _authService.GetSavedCredentialAsync()
            .Returns(("testuser", "testpass"));

        _authService.LoginAsync("testuser", "testpass", true)
            .Returns(new MaterialClient.Common.Entities.UserSession(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "testuser",
                "Test User",
                Guid.NewGuid(),
                "new-token",
                false,
                false,
                1,
                1,
                1,
                "Test Product",
                1,
                "Test Company",
                "http://test.com",
                DateTime.UtcNow.AddHours(1)
            ));

        var eventData = new SessionRefreshRequiredEto("/api/test", 401, DateTime.UtcNow);

        // Act
        await _handler.HandleEventAsync(eventData);

        // Assert
        await _authService.Received(1).GetSavedCredentialAsync();
        await _authService.Received(1).LoginAsync("testuser", "testpass", true);
    }

    [Fact]
    public async Task HandleEventAsync_ShouldNotCallLoginAsync_WhenNoSavedCredentials()
    {
        // Arrange
        _authService.GetSavedCredentialAsync()
            .Returns(((string username, string password)?)null);

        var eventData = new SessionRefreshRequiredEto("/api/test", 401, DateTime.UtcNow);

        // Act
        await _handler.HandleEventAsync(eventData);

        // Assert
        await _authService.Received(1).GetSavedCredentialAsync();
        await _authService.DidNotReceive().LoginAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleEventAsync_ShouldNotThrow_WhenReLoginFails()
    {
        // Arrange
        _authService.GetSavedCredentialAsync()
            .Returns(("testuser", "testpass"));

        _authService.LoginAsync("testuser", "testpass", true)
            .Returns(Task.FromException<MaterialClient.Common.Entities.UserSession>(
                new Exception("Network error")));

        var eventData = new SessionRefreshRequiredEto("/api/test", 401, DateTime.UtcNow);

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => _handler.HandleEventAsync(eventData));
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task HandleEventAsync_ShouldNotThrow_WhenGetCredentialFails()
    {
        // Arrange
        _authService.GetSavedCredentialAsync()
            .Returns(Task.FromException<(string username, string password)?>(new Exception("Database error")));

        var eventData = new SessionRefreshRequiredEto("/api/test", 401, DateTime.UtcNow);

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => _handler.HandleEventAsync(eventData));
        exception.ShouldBeNull();
    }
}
