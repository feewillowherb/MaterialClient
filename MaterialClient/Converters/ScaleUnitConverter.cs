using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Converters;

/// <summary>
///     Scale unit converter for displaying enum values as text
/// </summary>
public class ScaleUnitConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScaleUnit unit)
        {
            return unit switch
            {
                ScaleUnit.Kg => "kg",
                ScaleUnit.Ton => "t",
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

