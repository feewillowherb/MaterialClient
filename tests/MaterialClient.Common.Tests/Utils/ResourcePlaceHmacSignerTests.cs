using System.Globalization;
using MaterialClient.Common.Utils;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Utils;

/// <summary>
///     验证 ResourcePlaceHmacSigner 与 PowerShell ResourcePlaceAuth.ps1 签名结果一致。
///     测试向量由 _temp/resource-place-api-test/generate-test-vectors.ps1 生成。
/// </summary>
public class ResourcePlaceHmacSignerTests
{
    [Theory]
    [InlineData(
        "POST",
        "https://gzt.cgw.hangzhou.gov.cn/muckmanage/addmtd0p1q/api/zhztc-module-exapi/dataCenter/resourcePlace/productTransportRecord/v1/addBatch",
        "",
        "AK20260707FB2RI2TA",
        "etvmuulfYEMXKXkhK7KzsTh6HDtozghY",
        "Fri, 10 Jul 2026 02:55:50 GMT",
        "ixE1qMlumGWX8QGc49mGBJv6ziPjc8aJaYJeq5iyfU0=")]
    [InlineData(
        "POST",
        "https://gzt.cgw.hangzhou.gov.cn/muckmanage/addmtd0p1q/api/zhztc-module-exapi/dataCenter/resourcePlace/productTransportRecord/v1/addBatch",
        "",
        "AK20260707FB2RI2TA",
        "etvmuulfYEMXKXkhK7KzsTh6HDtozghY",
        "Fri, 10 Jul 2026 02:50:11 GMT",
        "Nt0Ami/0z0Ev677OKIVA/kjDREUMc3GkdtaeMaO+oe4=")]
    public void Sign_ShouldMatchPowerShellReference(
        string method,
        string url,
        string expectedSortedQuery,
        string accessKey,
        string secretKey,
        string gmtDateTime,
        string expectedSignature)
    {
        // Act
        var signature = ResourcePlaceHmacSigner.Sign(method, url, accessKey, secretKey, gmtDateTime);

        // Assert
        signature.ShouldBe(expectedSignature);
    }

    [Fact]
    public void BuildSortedQuery_NoQuery_ReturnsEmpty()
    {
        var uri = new Uri("https://example.com/api/test");
        ResourcePlaceHmacSigner.BuildSortedQuery(uri).ShouldBeEmpty();
    }

    [Fact]
    public void BuildSortedQuery_SingleParam_ReturnsEncodedPair()
    {
        var uri = new Uri("https://example.com/api/detail?dataNo=f47ac10b-58cc-4372-a567-0e02b2c3d479");
        ResourcePlaceHmacSigner.BuildSortedQuery(uri)
            .ShouldBe("dataNo=f47ac10b-58cc-4372-a567-0e02b2c3d479");
    }

    [Fact]
    public void BuildSortedQuery_MultipleParams_SortsByKeyAsc()
    {
        var uri = new Uri("https://example.com/api/add?status=active&page=2");
        ResourcePlaceHmacSigner.BuildSortedQuery(uri)
            .ShouldBe("page=2&status=active");
    }

    [Fact]
    public void BuildSortedQuery_AlreadyEncodedValues_RoundTripsCorrectly()
    {
        var uri = new Uri("https://example.com/api/search?q=hello%20world&lang=%E4%B8%AD%E6%96%87");
        var result = ResourcePlaceHmacSigner.BuildSortedQuery(uri);
        // 先解码再编码：hello world → hello%20world, 中文 → %E4%B8%AD%E6%96%87
        result.ShouldBe("lang=%E4%B8%AD%E6%96%87&q=hello%20world");
    }

    [Fact]
    public void BuildSortedQuery_NullUri_ReturnsEmpty()
    {
        ResourcePlaceHmacSigner.BuildSortedQuery(null).ShouldBeEmpty();
    }

    [Fact]
    public void GetGmtDateTime_ShouldReturnRfc1123Format()
    {
        var result = ResourcePlaceHmacSigner.GetGmtDateTime();
        // RFC 1123: "Tue, 08 Jul 2026 08:49:20 GMT"
        result.ShouldEndWith(" GMT");
        result.Length.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void ComputeHmacSha256Base64_KnownInput_ReturnsExpectedHash()
    {
        // HMAC-SHA256(key="", data="") — verified via PowerShell
        var result = ResourcePlaceHmacSigner.ComputeHmacSha256Base64("", "");
        result.ShouldBe("thNnmggU2ex3L5XXeMNfxf8Wl8STcVZTxscSFEKSxa0=");
    }

    [Fact]
    public void Sign_ConstructsSignStringCorrectly()
    {
        // Verify the sign string format: METHOD\n{sorted_query}\n{accessKey}\n{date}\n
        var method = "POST";
        var url = "https://example.com/api/test";
        var accessKey = "AK";
        var secretKey = "SK";
        var gmtDateTime = "Fri, 10 Jul 2026 02:55:50 GMT";

        // Build the expected sign string manually
        var sortedQuery = ResourcePlaceHmacSigner.BuildSortedQuery(new Uri(url));
        var expectedSignString = $"{method}\n{sortedQuery}\n{accessKey}\n{gmtDateTime}\n";

        // Verify sign string content
        expectedSignString.ShouldBe("POST\n\nAK\nFri, 10 Jul 2026 02:55:50 GMT\n");

        // Verify the full sign produces the same result as direct ComputeHmacSha256Base64
        var viaSign = ResourcePlaceHmacSigner.Sign(method, url, accessKey, secretKey, gmtDateTime);
        var viaDirect = ResourcePlaceHmacSigner.ComputeHmacSha256Base64(secretKey, expectedSignString);
        viaSign.ShouldBe(viaDirect);
    }
}
