using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Configuration;

namespace MaterialClient.Converters;

/// <summary>
///     Stream type converter for displaying enum values as text
/// </summary>
public class StreamTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StreamType streamType)
        {
            return streamType switch
            {
                StreamType.Substream => "子码流",
                StreamType.Mainstream => "主码流",
                _ => value.ToString()
            };
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

