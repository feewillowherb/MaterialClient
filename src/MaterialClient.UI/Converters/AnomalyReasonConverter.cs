using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.UI.Converters;

/// <summary>
///     AnomalyReason enum to Description text converter for list display.
/// </summary>
public class AnomalyReasonConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is AnomalyReason reason
            ? GetEnumDescription(reason)
            : value?.ToString() ?? "--";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string GetEnumDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();

        var attribute = field.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}
