using System;
using System.Globalization;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for ScaleUnitConverter.
/// </summary>
public class ScaleUnitConverterTests
{
    private readonly ScaleUnitConverter _converter = new();

    [Fact]
    public void Convert_Kg_ReturnsKg()
    {
        // Arrange
        var value = ScaleUnit.Kg;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("kg");
    }

    [Fact]
    public void Convert_Ton_ReturnsT()
    {
        // Arrange
        var value = ScaleUnit.Ton;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("t");
    }

    [Fact]
    public void Convert_TenGram_Returns10g()
    {
        // Arrange
        var value = ScaleUnit.TenGram;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("10g");
    }

    [Fact]
    public void Convert_HundredGram_Returns100g()
    {
        // Arrange
        var value = ScaleUnit.HundredGram;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("100g");
    }

    [Fact]
    public void Convert_Gram_ReturnsG()
    {
        // Arrange
        var value = ScaleUnit.Gram;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("g");
    }

    [Fact]
    public void Convert_NonScaleUnitValue_ReturnsToString()
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
        object? value = "kg";

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(ScaleUnit), null, CultureInfo.CurrentCulture));
    }
}
