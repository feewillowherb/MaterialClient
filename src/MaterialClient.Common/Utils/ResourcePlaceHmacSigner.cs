using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MaterialClient.Common.Utils;

/// <summary>
///     资源化利用厂平台（杭州市渣土管理中心）HMAC-SHA256 签名工具。
///     签名规范：{METHOD}\n{sorted_query}\n{accessKey}\n{gmtDateTime}\n
///     参考文档：杭州市资源化利用厂数据接入接口 V1.0 附录 Header 鉴权参数说明。
/// </summary>
public static class ResourcePlaceHmacSigner
{
    /// <summary>
    ///     构造签名字符串并计算 HMAC-SHA256 Base64 签名。
    /// </summary>
    /// <param name="method">HTTP 方法（大写，如 POST、GET）。</param>
    /// <param name="url">完整请求 URL（含查询参数）。</param>
    /// <param name="accessKey">平台颁发的 accessKey。</param>
    /// <param name="secretKey">平台颁发的 secretKey。</param>
    /// <param name="gmtDateTime">RFC 1123 格式的 GMT 时间戳。</param>
    /// <returns>Base64 编码的 HMAC-SHA256 签名。</returns>
    public static string Sign(
        string method,
        string url,
        string accessKey,
        string secretKey,
        string gmtDateTime)
    {
        var sortedQuery = BuildSortedQuery(new Uri(url));
        var signString = $"{method}\n{sortedQuery}\n{accessKey}\n{gmtDateTime}\n";
        return ComputeHmacSha256Base64(secretKey, signString);
    }

    /// <summary>
    ///     生成当前 UTC 时间的 RFC 1123 格式时间戳（与 PowerShell Get-ResourcePlaceGmtDate 一致）。
    /// </summary>
    public static string GetGmtDateTime()
    {
        return DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     构造排序后的查询字符串（按 key 升序拼接为 key=value&amp;key=value）。
    ///     无查询参数时返回空字符串。
    ///     对 key 和 value 进行 URL 编码，与 PowerShell ResourcePlaceAuth.ps1 保持一致。
    /// </summary>
    public static string BuildSortedQuery(Uri? requestUri)
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

        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var encodedPairs = new List<string>(pairs.Length);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.EscapeDataString(Uri.UnescapeDataString(parts[0]));
            var value = parts.Length > 1
                ? Uri.EscapeDataString(Uri.UnescapeDataString(parts[1]))
                : string.Empty;
            encodedPairs.Add($"{key}={value}");
        }

        encodedPairs.Sort(StringComparer.Ordinal);
        return string.Join("&", encodedPairs);
    }

    /// <summary>
    ///     使用 secretKey（UTF-8）对签名字符串计算 HMAC-SHA256，并 Base64 编码。
    /// </summary>
    public static string ComputeHmacSha256Base64(string secretKey, string signString)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(signString));
        return Convert.ToBase64String(hash);
    }
}
