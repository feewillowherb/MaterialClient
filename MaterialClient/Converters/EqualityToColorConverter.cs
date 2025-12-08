using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MaterialClient.Converters;

/// <summary>
/// 比较两个对象是否相等，相等返回选中颜色，不相等返回透明
/// </summary>
public class EqualityToColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 2)
            return new SolidColorBrush(Colors.Transparent);

        var value1 = values[0];
        var value2 = values[1];

        if (value1 != null && value1.Equals(value2))
        {
            // 选中时返回蓝色背景
            return new SolidColorBrush(Color.Parse("#E3F2FD"));
        }

        return new SolidColorBrush(Colors.Transparent);
    }
}
