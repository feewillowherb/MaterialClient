using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;

namespace MaterialClient.UI.Converters;

/// <summary>
///     Scale type converter for displaying enum values as text
/// </summary>
public class ScaleTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScaleType scaleType)
        {
            return scaleType.GetDescription();
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

