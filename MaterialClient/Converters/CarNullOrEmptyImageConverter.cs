using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MaterialClient.Converters;

/// <summary>
///     将 null 或空字符串的图片路径转换为默认图片（用于车辆照片）
/// </summary>
public class CarNullOrEmptyImageConverter : IValueConverter
{
    private const string DefaultCarImage = "avares://MaterialClient/Assets/Car_Default.png";
    private static Bitmap? _defaultBitmap;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;

        // 如果值为 null 或空字符串，返回默认图片
        if (string.IsNullOrWhiteSpace(path))
        {
            // 懒加载默认图片
            if (_defaultBitmap == null)
            {
                var assets = AssetLoader.Open(new Uri(DefaultCarImage));
                _defaultBitmap = new Bitmap(assets);
            }

            return _defaultBitmap;
        }

        // 否则尝试加载指定路径的图片
        try
        {
            // 如果路径是本地文件路径
            if (File.Exists(path)) return new Bitmap(path);

            // 如果是资源路径
            if (path.StartsWith("avares://") || path.StartsWith("/Assets/"))
            {
                var uri = path.StartsWith("/") ? new Uri($"avares://MaterialClient{path}") : new Uri(path);
                var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }

            return _defaultBitmap;
        }
        catch
        {
            return _defaultBitmap;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}