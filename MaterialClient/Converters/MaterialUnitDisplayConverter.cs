using System;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace MaterialClient.Converters;

/// <summary>
/// Build conversion unit display text like: 1个=1000吨.
/// Falls back to "1个" when UnitName/BasicUnit are missing.
/// </summary>
public class MaterialUnitDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return "1个";
        }

        var unitName = AsString(GetPropertyValue(value, "UnitName"));
        var basicUnit = AsString(GetPropertyValue(value, "BasicUnit"));

        if (string.IsNullOrWhiteSpace(unitName) || string.IsNullOrWhiteSpace(basicUnit))
        {
            return "1个";
        }

        var unitRate = GetUnitRateValue(value) ?? 1m;
        return $"1{unitName}={FormatRate(unitRate)}{basicUnit}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static object? GetPropertyValue(object? obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }

        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return prop?.GetValue(obj);
    }

    private static string? AsString(object? value)
    {
        return value?.ToString();
    }

    private static decimal? GetUnitRateValue(object obj)
    {
        var rateObj = GetPropertyValue(obj, "UnitRate");
        if (rateObj == null)
        {
            return null;
        }

        return rateObj switch
        {
            decimal d => d,
            decimal? dn => dn,
            double db => (decimal)db,
            float f => (decimal)f,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static string FormatRate(decimal rate)
    {
        return rate.ToString("0.################", CultureInfo.InvariantCulture);
    }
}
