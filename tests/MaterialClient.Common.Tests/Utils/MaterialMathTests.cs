using MaterialClient.Common.Utils;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Utils;

public class MaterialMathTests
{
    [Fact]
    public void DetermineOffsetResult_OverNegativeDeviation()
    {
        // Arrange
        decimal? deviationRate = -5m; // -5%
        decimal? lowerLimit = -3m;    // allowed lower limit -3%
        decimal? upperLimit = null;

        // Act
        var result = MaterialMath.DetermineOffsetResult(deviationRate, lowerLimit, upperLimit);

        // Assert
        
        result.ShouldBe(Entities.Enums.OffsetResultType.OverNegativeDeviation);
    }


    [Fact]
    public void DetermineOffsetResult_OverPositiveDeviation()
    {
        // Arrange
        decimal? deviationRate = 6m;  // 6%
        decimal? lowerLimit = null;
        decimal? upperLimit = 4m;     // allowed upper limit 4%

        // Act
        var result = MaterialMath.DetermineOffsetResult(deviationRate, lowerLimit, upperLimit);

        // Assert
        result.ShouldBe(Entities.Enums.OffsetResultType.OverPositiveDeviation);
    }

    
}