using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MaterialClient.Converters;

/// <summary>
///     将布尔值转换为画刷，true返回第一个颜色，false返回第二个颜色
///     参数格式: "TrueColor|FalseColor"，例如 "#3B82F6|White"
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramStr)
            return new SolidColorBrush(Colors.Transparent);

        var colors = paramStr.Split('|');
        if (colors.Length != 2)
            return new SolidColorBrush(Colors.Transparent);

        var colorStr = boolValue ? colors[0] : colors[1];

        // 处理命名颜色
        if (colorStr.Equals("White", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Colors.White);
        if (colorStr.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Colors.Transparent);

        // 尝试解析为颜色代码
        try
        {
            return new SolidColorBrush(Color.Parse(colorStr));
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}