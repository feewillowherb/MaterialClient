using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MaterialClient.UI.Converters;

/// <summary>
///     Maps an enum whose underlying values are 0..n to ComboBox <c>SelectedIndex</c>.
/// </summary>
public sealed class EnumToSelectedIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum e ? System.Convert.ToInt32(e) : 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && targetType.IsEnum)
        {
            return Enum.ToObject(targetType, index);
        }

        return value;
    }
}
