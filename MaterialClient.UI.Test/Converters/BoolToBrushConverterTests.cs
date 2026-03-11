using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for BoolToBrushConverter.
/// </summary>
public class BoolToBrushConverterTests
{
    private readonly BoolToBrushConverter _converter = new();

    [Fact]
    public void Convert_TrueValue_ReturnsFirstColor()
    {
        // Arrange
        var value = true;
        var parameter = "#3B82F6|White";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.R.ShouldBe((byte)0x3B);
        brush.Color.G.ShouldBe((byte)0x82);
        brush.Color.B.ShouldBe((byte)0xF6);
    }

    [Fact]
    public void Convert_FalseValue_ReturnsSecondColor()
    {
        // Arrange
        var value = false;
        var parameter = "#3B82F6|White";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.White);
    }

    [Fact]
    public void Convert_WithNamedColors_ReturnsCorrectBrush()
    {
        // Arrange
        var value = true;
        var parameter = "White|Transparent";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.White);
    }

    [Fact]
    public void Convert_NonBoolValue_ReturnsTransparent()
    {
        // Arrange
        var value = "not a bool";
        var parameter = "#3B82F6|White";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void Convert_InvalidParameter_ReturnsTransparent()
    {
        // Arrange
        var value = true;
        var parameter = 123;

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void Convert_MalformedParameter_ReturnsTransparent()
    {
        // Arrange
        var value = true;
        var parameter = "#3B82F6"; // Missing second color

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void Convert_InvalidColorString_ReturnsTransparent()
    {
        // Arrange
        var value = true;
        var parameter = "NotAValidColor|White";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void ConvertBack_NotImplemented_ThrowsNotImplementedException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(bool), null, CultureInfo.CurrentCulture));
    }

    [Fact]
    public void Convert_CaseInsensitiveNamedColors()
    {
        // Arrange
        var value = true;
        var parameter = "white|TRANSPARENT";

        // Act
        var result = _converter.Convert(value, typeof(IBrush), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldBeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result;
        brush.Color.ShouldBe(Colors.White);
    }
}
