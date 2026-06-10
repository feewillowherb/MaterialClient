using System.Net;
using System.Net.Http.Headers;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Api;

/// <summary>
///     为材料平台接口添加 Bearer Token 的处理器，
///     并在检测到 401 Unauthorized 响应时发布会话刷新事件。
/// </summary>
public class MaterialPlatformBearerTokenHandler : DelegatingHandler
{
    private readonly ILogger<MaterialPlatformBearerTokenHandler> _logger;
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<UserSession, Guid> _sessionRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public MaterialPlatformBearerTokenHandler(
        IRepository<UserSession, Guid> sessionRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ILocalEventBus localEventBus,
        ILogger<MaterialPlatformBearerTokenHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(true, false);
            var session = await _sessionRepository.FirstOrDefaultAsync(cancellationToken);
            var token = session?.AccessToken;
            await uow.CompleteAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                _logger.LogWarning("未找到用户会话或访问令牌为空，材料平台请求将不携带认证头。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "为材料平台请求添加 Bearer Token 时发生异常。");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                "材料平台 API 返回 401 Unauthorized，端点：{Endpoint}，发布会话刷新事件。",
                request.RequestUri?.AbsolutePath);

            _ = _localEventBus.PublishAsync(new SessionRefreshRequiredEto(
                request.RequestUri?.AbsolutePath ?? "unknown",
                (int)response.StatusCode,
                DateTime.UtcNow));
        }

        return response;
    }
}