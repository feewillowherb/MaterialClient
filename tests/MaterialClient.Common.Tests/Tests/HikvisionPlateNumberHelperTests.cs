using MaterialClient.Common.Services.Hikvision;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class HikvisionPlateNumberHelperTests
{
    [Theory]
    [InlineData("浙A12345", "浙A12345")]
    [InlineData("蓝浙A12345", "浙A12345")]
    [InlineData("黄色浙A12345", "浙A12345")]
    [InlineData("  绿浙AD12345  ", "浙AD12345")]
    [InlineData("黄WJ京0001警", "WJ京0001警")]
    [InlineData("WJ京0001警", "WJ京0001警")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StripColorPrefix_ShouldRemoveEmbeddedColorPrefix(string? raw, string expected)
    {
        var result = HikvisionPlateNumberHelper.StripColorPrefix(raw);

        Assert.Equal(expected, result);
    }
}
