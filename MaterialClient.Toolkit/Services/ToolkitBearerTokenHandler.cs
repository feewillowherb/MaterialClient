using System.Net.Http.Headers;

namespace MaterialClient.Toolkit.Services;

/// <summary>
///     轻量 Bearer Token 处理器，从静态字段读取 Token，不依赖数据库
/// </summary>
public class ToolkitBearerTokenHandler : DelegatingHandler
{
    internal static string? CurrentToken;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(CurrentToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);

        return base.SendAsync(request, cancellationToken);
    }
}
