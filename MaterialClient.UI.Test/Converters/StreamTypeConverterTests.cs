using System;
using System.Globalization;
using MaterialClient.Common.Configuration;
using MaterialClient.UI.Converters;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Converters;

/// <summary>
/// Tests for StreamTypeConverter.
/// </summary>
public class StreamTypeConverterTests
{
    private readonly StreamTypeConverter _converter = new();

    [Fact]
    public void Convert_Substream_ReturnsChineseText()
    {
        // Arrange
        var value = StreamType.Substream;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("子码流");
    }

    [Fact]
    public void Convert_Mainstream_ReturnsChineseText()
    {
        // Arrange
        var value = StreamType.Mainstream;

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        result.ShouldBe("主码流");
    }

    [Fact]
    public void Convert_NonStreamTypeValue_ReturnsToString()
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
        object? value = "子码流";

        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(StreamType), null, CultureInfo.CurrentCulture));
    }
}
