using MaterialClient.Common.Utils;
using MaterialClient.Recycle.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     §2.2 接口 HMAC-SHA256 签名 <see cref="DelegatingHandler" />。
///     拦截每个出站 HTTP 请求，委托 <see cref="ResourcePlaceHmacSigner" /> 计算签名、
///     并注入四个 X-AKZTJG-* 自定义 Header，与 Refit 接口签名解耦。
/// </summary>
public class RecycleHmacDelegatingHandler : DelegatingHandler
{
    /// <summary>固定算法标识 Header 值。</summary>
    private const string Algorithm = "hmac-sha256";

    private readonly IOptionsMonitor<RecycleSyncOptions> _optionsMonitor;
    private readonly ILogger<RecycleHmacDelegatingHandler> _logger;

    public RecycleHmacDelegatingHandler(
        IOptionsMonitor<RecycleSyncOptions> optionsMonitor,
        ILogger<RecycleHmacDelegatingHandler> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        var accessKey = options.AccessKey;
        var secretKey = options.SecretKey;

        if (string.IsNullOrWhiteSpace(accessKey))
        {
            _logger.LogError("Recycle HMAC accessKey not configured");
            throw new InvalidOperationException("Recycle HMAC accessKey not configured");
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("Recycle HMAC secretKey not configured");
            throw new InvalidOperationException("Recycle HMAC secretKey not configured");
        }

        var gmtDateTime = ResourcePlaceHmacSigner.GetGmtDateTime();
        var method = request.Method.Method;
        var signature = ResourcePlaceHmacSigner.Sign(method, request.RequestUri!.ToString(), accessKey, secretKey, gmtDateTime);

        _logger.LogInformation("=== 签名生成过程 ===");
        _logger.LogInformation("URL: {Url}", request.RequestUri);
        _logger.LogInformation("Method: {Method}", method.ToUpperInvariant());
        _logger.LogInformation("时间戳 (GMT): {DateTime}", gmtDateTime);
        _logger.LogInformation("AccessKey: {AccessKey}", accessKey);
        _logger.LogInformation("签名 (Base64): {Signature}", signature);

        // 注入四个 X-AKZTJG-* Header
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-SIGNATURE", signature);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-ALGORITHM", Algorithm);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-ACCESS-KEY", accessKey);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-DATE-TIME", gmtDateTime);

        return base.SendAsync(request, cancellationToken);
    }
}
