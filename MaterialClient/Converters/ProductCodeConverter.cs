using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;

namespace MaterialClient.Converters;

/// <summary>
///     Product code converter for displaying enum values as text
/// </summary>
public class ProductCodeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProductCode productCode)
        {
            return productCode.GetDescription();
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
