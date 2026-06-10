using MaterialClient.Common.Utils;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Utils;

public class MaterialMathWeightConversionTests
{
    [Theory]
    [InlineData(8.5, 8500)]
    [InlineData(30, 30000)]
    [InlineData(2, 2000)]
    public void ConvertTonToKg_maps_ton_to_integer_kg(decimal tons, decimal expectedKg)
    {
        MaterialMath.ConvertTonToKg(tons).ShouldBe(expectedKg);
    }

    [Fact]
    public void ConvertTonToKg_zero_ton_returns_zero_kg()
    {
        MaterialMath.ConvertTonToKg(0m).ShouldBe(0m);
    }
}
