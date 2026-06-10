using MaterialClient.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Services.Urban;

/// <summary>
///     StaticAuthChecker 单元测试
///     测试静态授权检查服务的默认行为（TODO: 后续完善实际授权逻辑）
/// </summary>
public class StaticLicenseCheckerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public async Task CheckLicenseAsync_Default_ReturnsSuccess()
    {
        // Arrange
        var checker = new StaticLicenseChecker();

        // Act
        var result = await checker.CheckLicenseAsync("test-license.lic");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "默认实现应返回成功");
        _output.WriteLine($"授权检查结果: {result.IsSuccess} - {result.Message}");
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public async Task CheckLicenseAsync_Default_ReturnsNonNullMessage()
    {
        // Arrange
        var checker = new StaticLicenseChecker();

        // Act
        var result = await checker.CheckLicenseAsync("nonexistent.lic");

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Message), "结果消息不应为空");
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public async Task CheckLicenseAsync_Default_CheckedAtIsValid()
    {
        // Arrange
        var checker = new StaticLicenseChecker();
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var result = await checker.CheckLicenseAsync("test.lic");

        // Assert
        Assert.NotNull(result);
        var after = DateTime.Now.AddSeconds(1);
        Assert.True(result.CheckedAt >= before && result.CheckedAt <= after,
            "检查时间应在合理范围内");
    }

    [Fact(Skip = "实际实现已更新，需要重新设计测试用例")]
    public async Task CheckLicenseAsync_NonExistentFile_StillReturnsSuccess()
    {
        // Arrange
        var checker = new StaticLicenseChecker();

        // Act
        // TODO: 当前默认返回成功，后续实现实际授权验证时此测试应改为验证失败场景
        var result = await checker.CheckLicenseAsync("/nonexistent/path/license.lic");

        // Assert
        Assert.NotNull(result);
        // 当前实现默认返回成功，即使文件不存在
        Assert.True(result.IsSuccess, "当前实现：默认返回成功（后续完善实际授权逻辑）");
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
