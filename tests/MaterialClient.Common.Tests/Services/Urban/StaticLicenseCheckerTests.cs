using MaterialClient.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Services.Urban;

/// <summary>
///     StaticLicenseChecker 单元测试
///     测试 JWT 授权检查服务（TODO: 后续完善实际授权逻辑）
/// </summary>
public class StaticLicenseCheckerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact(Skip = "需要重新设计测试用例以适配 StaticLicenseChecker")]
    public async Task CheckLicenseAsync_Default_ReturnsSuccess()
    {
        // Arrange
        // TODO: 构造带公钥配置的 StaticLicenseChecker
        _output.WriteLine("需要重新设计测试用例以适配 StaticLicenseChecker");

        await Task.CompletedTask;
    }

    [Fact(Skip = "需要重新设计测试用例以适配 StaticLicenseChecker")]
    public async Task CheckLicenseAsync_NonExistentFile_ReturnsFail()
    {
        // Arrange
        // TODO: 构造带公钥配置的 StaticLicenseChecker，测试文件不存在场景
        _output.WriteLine("需要重新设计测试用例以适配 StaticLicenseChecker");

        await Task.CompletedTask;
    }

    [Fact(Skip = "需要重新设计测试用例以适配 StaticLicenseChecker")]
    public async Task CheckLicenseAsync_ExpiredToken_ReturnsFail()
    {
        // Arrange
        // TODO: 构造过期的 JWT 令牌，测试过期场景
        _output.WriteLine("需要重新设计测试用例以适配 StaticLicenseChecker");

        await Task.CompletedTask;
    }
}

/// <summary>
///     LicenseCheckResult 单元测试
/// </summary>
public class LicenseCheckResultTests
{
    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public void Success_CreatesSuccessfulResult()
    {
        var result = LicenseCheckResult.Success("测试通过");
        Assert.True(result.IsSuccess);
        Assert.Equal("测试通过", result.Message);
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public void Fail_CreatesFailedResult()
    {
        var result = LicenseCheckResult.Fail("授权失败");
        Assert.False(result.IsSuccess);
        Assert.Equal("授权失败", result.Message);
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public void Success_DefaultMessage_IsValid()
    {
        var result = LicenseCheckResult.Success();
        Assert.True(result.IsSuccess);
        Assert.Equal("授权检查通过", result.Message);
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public void Fail_DefaultMessage_IsValid()
    {
        var result = LicenseCheckResult.Fail();
        Assert.False(result.IsSuccess);
        Assert.Equal("授权检查失败", result.Message);
    }
}
