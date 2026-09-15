using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;

namespace MaterialClient.UI.Converters;

/// <summary>
///     Transmission format type converter for ComboBox display.
/// </summary>
public class TransmissionFormatTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TransmissionFormatType format)
            return format.GetDescription();

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
