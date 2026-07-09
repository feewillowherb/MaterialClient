using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MaterialClient.Recycle.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     §2.2 接口 HMAC-SHA256 签名 <see cref="DelegatingHandler" />。
///     拦截每个出站 HTTP 请求，按 §2.2 文档规范构造签名字符串、计算 HMAC-SHA256、
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

        // GMT+8 时间戳（RFC 1123 格式，形如 "Tue, 08 Jul 2026 08:49:20 GMT"）。
        // §2.2 要求 GMT+8 时区、允许 ±100 秒误差。以 UtcNow+8 的墙钟时间，按 UTC Kind 格式化为 RFC1123，
        // 使输出固定带 "GMT" 后缀（与文档示例一致）。
        var gmtPlus8Stamped = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(8), DateTimeKind.Utc);
        var gmtDateTime = gmtPlus8Stamped.ToString("R", CultureInfo.InvariantCulture);

        var method = request.Method.Method;
        var sortedQuery = BuildSortedQuery(request.RequestUri);

        // 签名字符串：{METHOD}\n{sorted_query}\n{accessKey}\n{gmtDateTime}\n
        var signString = $"{method}\n{sortedQuery}\n{accessKey}\n{gmtDateTime}\n";

        var signature = ComputeHmacSha256Base64(secretKey, signString);

        // 注入四个 X-AKZTJG-* Header
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-SIGNATURE", signature);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-ALGORITHM", Algorithm);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-HMAC-ACCESS-KEY", accessKey);
        request.Headers.TryAddWithoutValidation("X-AKZTJG-DATE-TIME", gmtDateTime);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Recycle HMAC signed: method={Method}, query='{Query}', dateTime={DateTime}",
                method,
                sortedQuery,
                gmtDateTime);
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    ///     构造排序后的查询字符串（按 key 升序拼接为 key=value &amp; key=value）。
    ///     无查询参数时返回空字符串（与 §2.2 POST 无参场景一致）。
    /// </summary>
    private static string BuildSortedQuery(Uri? requestUri)
    {
        if (requestUri == null || string.IsNullOrWhiteSpace(requestUri.Query))
        {
            return string.Empty;
        }

        var query = requestUri.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        // 简单解析 key=value 对，按 key 排序后用 '&' 拼接。
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(pairs, StringComparer.Ordinal);
        return string.Join("&", pairs);
    }

    /// <summary>
    ///     使用 secretKey（UTF-8）对签名字符串计算 HMAC-SHA256，并 Base64 编码。
    /// </summary>
    private static string ComputeHmacSha256Base64(string secretKey, string signString)
    {
#if NET10_0_OR_GREATER
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signString), Encoding.UTF8.GetBytes(secretKey));
#else
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
#endif
        return Convert.ToBase64String(hash);
    }
}
