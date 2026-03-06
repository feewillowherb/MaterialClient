using System;
using System.Globalization;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for ScaleTypeConverter.
/// </summary>
public class ScaleTypeConverterTests
{
    private readonly ScaleTypeConverter _converter = new();

    [Fact]
    public void Convert_Yaohua_ReturnsDescription()
    {
        // Arrange
        var value = ScaleType.Yaohua;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("耀华");
    }

    [Fact]
    public void Convert_DingSong_ReturnsDescription()
    {
        // Arrange
        var value = ScaleType.DingSong;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("顶松");
    }

    [Fact]
    public void Convert_NonScaleTypeValue_ReturnsToString()
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
        object? value = "耀华";

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(ScaleType), null, CultureInfo.CurrentCulture));
    }
}
