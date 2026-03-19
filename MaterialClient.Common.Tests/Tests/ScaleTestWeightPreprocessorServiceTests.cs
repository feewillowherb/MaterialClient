using MaterialClient.Common.Services.Hardware;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

public class ScaleTestWeightPreprocessorServiceTests
{
    [Fact]
    public void Smoothstep_Should_ReturnExpectedValues()
    {
        // t = 0.2 => 0.2^2 * (3 - 2*0.2) = 0.04 * 2.6 = 0.104
        ScaleTestWeightPreprocessorService.Smoothstep(0.2m).ShouldBe(0.104m);
    }

    [Fact]
    public void Interpolate_Should_GenerateFiveSteps_AndReachB()
    {
        var a = 10m;
        var b = 20m;
        var steps = 5;

        var expected = new[]
        {
            11.04m, // step 1: t=0.2
            13.52m, // step 2: t=0.4
            16.48m, // step 3: t=0.6
            18.96m, // step 4: t=0.8
            20m // step 5: t=1.0
        };

        var actual = new decimal[steps];
        for (var i = 1; i <= steps; i++)
        {
            actual[i - 1] =
                ScaleTestWeightPreprocessorService.Interpolate(a, b, i, steps);
        }

        actual.ShouldBe(expected);
        actual[^1].ShouldBe(b);
    }
}

