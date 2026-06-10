using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities;

namespace MaterialClient.UI.Converters;

/// <summary>
/// Build conversion unit display text like: 1个=1000吨.
/// Falls back to "1个" when UnitName/BasicUnit are missing.
/// </summary>
public class MaterialUnitDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Material material)
        {
            return "1个";
        }

        if (string.IsNullOrWhiteSpace(material.UnitName) || string.IsNullOrWhiteSpace(material.BasicUnit))
        {
            return "1个";
        }

        return $"1{material.UnitName}={FormatRate(material.UnitRate)}{material.BasicUnit}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string FormatRate(decimal rate)
    {
        return rate.ToString("0.################", CultureInfo.InvariantCulture);
    }
}
