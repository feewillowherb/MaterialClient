using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Api;

/// <summary>
/// 为材料平台接口添加 Bearer Token 的处理器
/// </summary>
public class MaterialPlatformBearerTokenHandler : DelegatingHandler
{
    private readonly IRepository<UserSession, Guid> _sessionRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<MaterialPlatformBearerTokenHandler> _logger;

    public MaterialPlatformBearerTokenHandler(
        IRepository<UserSession, Guid> sessionRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<MaterialPlatformBearerTokenHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            string? token = null;

            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
            {
                var session = await _sessionRepository.FirstOrDefaultAsync(cancellationToken: cancellationToken);
                token = session?.AccessToken;
                await uow.CompleteAsync();
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _logger.LogWarning("未找到用户会话或访问令牌为空，材料平台请求将不携带认证头。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "为材料平台请求添加 Bearer Token 时发生异常。");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
