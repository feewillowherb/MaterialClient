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

    [Theory]
    [InlineData("蓝色", "蓝")]
    [InlineData("黄色", "黄")]
    [InlineData("黄绿色", "黄绿")]
    [InlineData("渐变绿色", "渐变绿")]
    [InlineData("蓝", "蓝")]
    [InlineData("其他", "其他")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void NormalizePlateColor_ShouldRemoveSeCharacter(string? input, string? expected)
    {
        Assert.Equal(expected, HikvisionPlateNumberHelper.NormalizePlateColor(input));
    }

    [Theory]
    [InlineData("黄色浙A12345", null, "浙A12345", "黄")]
    [InlineData("蓝浙A12345", null, "浙A12345", "蓝")]
    [InlineData("浙A12345", "黄色", "浙A12345", "黄")]
    [InlineData("浙A12345", null, "浙A12345", null)]
    [InlineData("黄WJ京0001警", null, "WJ京0001警", "黄")]
    public void ParseLicense_ShouldPreferPrefixColorWithoutSeCharacter(
        string? raw,
        string? fallback,
        string expectedPlate,
        string? expectedColor)
    {
        var result = HikvisionPlateNumberHelper.ParseLicense(raw, fallback);

        Assert.Equal(expectedPlate, result.PlateNumber);
        Assert.Equal(expectedColor, result.PlateColor);
    }
}
