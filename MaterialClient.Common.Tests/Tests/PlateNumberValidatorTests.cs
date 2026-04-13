using MaterialClient.Common.Providers;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public sealed class PlateNumberValidatorTests
{
    [Theory]
    [InlineData("京AD12345")]
    [InlineData("京AF12345")]
    [InlineData("粤BF12345")]
    [InlineData("浙AD12345")]
    [InlineData(" 沪AD12345 ")]
    public void IsNewEnergyPlate_Should_ReturnTrue_For_ValidPlates(string plate)
    {
        PlateNumberValidator.IsNewEnergyPlate(plate).ShouldBeTrue();
    }

    [Theory]
    [InlineData("京A12345")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("京AD1234")]
    [InlineData("京AD123456")]
    public void IsNewEnergyPlate_Should_ReturnFalse_For_InvalidPlates(string plate)
    {
        PlateNumberValidator.IsNewEnergyPlate(plate).ShouldBeFalse();
    }

    [Fact]
    public void IsNewEnergyPlate_Should_ReturnFalse_When_SequenceContainsLetterI()
    {
        PlateNumberValidator.IsNewEnergyPlate("京AI12345").ShouldBeFalse();
    }

    [Fact]
    public void IsNewEnergyPlate_Should_ReturnFalse_When_SequenceContainsLetterO()
    {
        PlateNumberValidator.IsNewEnergyPlate("京AO12345").ShouldBeFalse();
    }
}
