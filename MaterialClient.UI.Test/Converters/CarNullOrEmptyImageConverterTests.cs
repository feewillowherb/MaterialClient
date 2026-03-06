using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for CarNullOrEmptyImageConverter.
/// Note: Tests that require Avalonia runtime are marked as skipped or require proper setup.
/// </summary>
public class CarNullOrEmptyImageConverterTests
{
    private readonly CarNullOrEmptyImageConverter _converter = new();

    [Fact]
    public void ConvertBack_NotImplemented_ThrowsNotImplementedException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(string), null, CultureInfo.CurrentCulture));
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_NullValue_ReturnsDefaultBitmap()
    {
        // Arrange
        object? value = null;

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_EmptyString_ReturnsDefaultBitmap()
    {
        // Arrange
        var value = string.Empty;

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_WhitespaceOnly_ReturnsDefaultBitmap()
    {
        // Arrange
        var value = "   ";

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_InvalidPath_ReturnsDefaultBitmap()
    {
        // Arrange
        var value = "/invalid/path/to/image.png";

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_ValidAvaresPath_ReturnsBitmap()
    {
        // Arrange
        var value = "avares://MaterialClient/Assets/Car_Default.png";

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_ValidAssetPath_ReturnsBitmap()
    {
        // Arrange
        var value = "/Assets/Car_Default.png";

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_NonStringValue_ReturnsDefaultBitmap()
    {
        // Arrange
        var value = 12345;

        // Act
        var result = _converter.Convert(value, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }

    [Fact(Skip = "Requires Avalonia runtime initialization")]
    public void Convert_ReturnsSameDefaultBitmapInstance()
    {
        // Arrange
        object? value1 = null;
        var value2 = string.Empty;

        // Act
        var result1 = _converter.Convert(value1, typeof(Bitmap), null, CultureInfo.CurrentCulture);
        var result2 = _converter.Convert(value2, typeof(Bitmap), null, CultureInfo.CurrentCulture);

        // Assert - Should return the same cached instance for default image
        result1.ShouldBe(result2);
    }
}
