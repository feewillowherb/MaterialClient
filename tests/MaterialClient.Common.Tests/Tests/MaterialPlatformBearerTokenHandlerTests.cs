using System.Net;
using System.Net.Http;
using MaterialClient.Common.Api;
using MaterialClient.Common.Entities;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
///     Integration tests for MaterialPlatformBearerTokenHandler 401 detection and event publishing.
///     Uses ABP test infrastructure with real SQLite database for repository operations.
/// </summary>
public class MaterialPlatformBearerTokenHandlerTests : MaterialClientEntityFrameworkCoreTestBase
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<UserSession, Guid> _sessionRepository;
    private readonly ITestService _testService;
    private readonly ILogger<MaterialPlatformBearerTokenHandler> _logger;

    public MaterialPlatformBearerTokenHandlerTests()
    {
        _localEventBus = Substitute.For<ILocalEventBus>();
        _sessionRepository = GetRequiredService<IRepository<UserSession, Guid>>();
        _testService = GetRequiredService<ITestService>();
        _logger = GetRequiredService<ILogger<MaterialPlatformBearerTokenHandler>>();
    }

    private MaterialPlatformBearerTokenHandler CreateHandler(
        HttpMessageHandler innerHandler)
    {
        var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();

        var handler = new MaterialPlatformBearerTokenHandler(
            _sessionRepository,
            uowManager,
            _localEventBus,
            _logger);

        handler.InnerHandler = innerHandler;
        return handler;
    }

    [Fact]
    public async Task SendAsync_ShouldPublishEvent_WhenResponseIs401()
    {
        // Arrange - seed a session in the database
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync();
            await _testService.CreateUserSessionAsync(accessToken: "test-token");
        });

        var expectedPath = "/api/Order/SynchronizationOrder";
        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.Unauthorized);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"http://localhost{expectedPath}");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _localEventBus.Received(1).PublishAsync(Arg.Is<SessionRefreshRequiredEto>(eto =>
            eto.ApiEndpoint == expectedPath &&
            eto.StatusCode == (int)HttpStatusCode.Unauthorized &&
            eto.OccurredAtUtc <= DateTime.UtcNow));
    }

    [Fact]
    public async Task SendAsync_ShouldNotPublishEvent_WhenResponseIs200()
    {
        // Arrange - seed a session in the database
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync();
            await _testService.CreateUserSessionAsync(accessToken: "test-token");
        });

        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.OK);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _localEventBus.DidNotReceive().PublishAsync(Arg.Any<SessionRefreshRequiredEto>());
    }

    [Fact]
    public async Task SendAsync_ShouldNotPublishEvent_WhenResponseIs500()
    {
        // Arrange - seed a session in the database
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync();
            await _testService.CreateUserSessionAsync(accessToken: "test-token");
        });

        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.InternalServerError);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await _localEventBus.DidNotReceive().PublishAsync(Arg.Any<SessionRefreshRequiredEto>());
    }

    [Fact]
    public async Task SendAsync_ShouldStillReturn401_ToCaller()
    {
        // Arrange - seed a session with expired token
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync();
            await _testService.CreateUserSessionAsync(accessToken: "expired-token");
        });

        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.Unauthorized);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert - 401 should propagate to caller, and event should be published
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _localEventBus.Received(1).PublishAsync(Arg.Any<SessionRefreshRequiredEto>());
    }

    [Fact]
    public async Task SendAsync_ShouldAddBearerToken_WhenSessionExists()
    {
        // Arrange - seed a session in the database
        await WithUnitOfWorkAsync(async () =>
        {
            await _testService.CreateLicenseInfoAsync();
            await _testService.CreateUserSessionAsync(accessToken: "valid-token");
        });

        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.OK);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert - verify the inner handler received the request with Authorization header
        innerHandler.ReceivedRequest.ShouldNotBeNull();
        innerHandler.ReceivedRequest.Headers.Authorization.ShouldNotBeNull();
        innerHandler.ReceivedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        innerHandler.ReceivedRequest.Headers.Authorization.Parameter.ShouldBe("valid-token");
    }

    [Fact]
    public async Task SendAsync_ShouldNotAddBearerToken_WhenNoSessionExists()
    {
        // Arrange - no session seeded, database is empty

        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.OK);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_ShouldPublishEvent_WithCorrectEndpointPath()
    {
        // Arrange - no session needed, we just test the 401 event content
        var innerHandler = new TestHttpMessageHandler(HttpStatusCode.Unauthorized);
        var handler = CreateHandler(innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _localEventBus.Received(1).PublishAsync(Arg.Is<SessionRefreshRequiredEto>(eto =>
            eto.ApiEndpoint == "/api/test"));
    }

    /// <summary>
    ///     Test HttpMessageHandler that returns a configurable response
    ///     and captures the received request for assertion
    /// </summary>
    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public TestHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public HttpRequestMessage? ReceivedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ReceivedRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
