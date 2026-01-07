using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Converters;

/// <summary>
///     Snapshot camera type converter for displaying enum values as text
/// </summary>
public class SnapshotCameraTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SnapshotCameraType cameraType)
        {
            return cameraType switch
            {
                SnapshotCameraType.Hikvision => "海康威视",
                SnapshotCameraType.LPRAllInOne => "车牌识别一体机",
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

