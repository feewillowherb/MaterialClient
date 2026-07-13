using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.UI.Converters;

/// <summary>
///     Delivery type converter for displaying enum values as text
/// </summary>
public class DeliveryTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DeliveryType deliveryType)
        {
            return deliveryType switch
            {
                DeliveryType.Receiving => "收料",
                DeliveryType.Sending => "发料",
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
