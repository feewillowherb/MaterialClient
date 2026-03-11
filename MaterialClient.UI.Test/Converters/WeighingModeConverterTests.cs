using System;
using System.Globalization;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for WeighingModeConverter.
/// </summary>
public class WeighingModeConverterTests
{
    private readonly WeighingModeConverter _converter = new();

    [Fact]
    public void Convert_Standard_ReturnsDescription()
    {
        // Arrange
        var value = WeighingMode.Standard;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("物料验收系统客户端软件");
    }

    [Fact]
    public void Convert_SolidWaste_ReturnsDescription()
    {
        // Arrange
        var value = WeighingMode.SolidWaste;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("城管固废称重验收系统客户端软件");
    }

    [Fact]
    public void Convert_NonWeighingModeValue_ReturnsToString()
    {
        // Arrange
        var value = "SomeString";

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("SomeString");
    }

    [Fact]
    public void Convert_NullValue_ReturnsNull()
    {
        // Arrange
        object? value = null;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertBack_NotImplemented_ThrowsNotImplementedException()
    {
        // Arrange
        object? value = "物料验收系统客户端软件";

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(WeighingMode), null, CultureInfo.CurrentCulture));
    }
}
