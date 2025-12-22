using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Models;

namespace MaterialClient.Converters;

/// <summary>
/// 将 WeighingListItemMaterialDto 转换为显示文本的 Converter
/// </summary>
public class WeighingListItemMaterialConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WeighingListItemMaterialDto materialDto || parameter is not string propertyName)
            return string.Empty;

        return propertyName switch
        {
            "MaterialName" => materialDto.MaterialName ?? string.Empty,
            "MaterialUnitDisplayName" => materialDto.MaterialUnitDisplayName,
            "MaterialUnitName" => materialDto.MaterialUnitName ?? string.Empty,
            "MaterialUnitRate" => materialDto.MaterialUnitRate?.ToString("F2") ?? string.Empty,
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

