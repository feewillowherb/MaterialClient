using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MaterialClient.Common.Utils;

namespace MaterialClient.UI.Converters;

/// <summary>
///     将 null 或空字符串的图片路径转换为 null（用于 BillPhoto 等不需要默认图片的场景）
/// </summary>
public class NullOrEmptyImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;

        // 如果值为 null 或空字符串，返回 null
        if (string.IsNullOrWhiteSpace(path)) return null;

        // 否则尝试加载指定路径的图片
        try
        {
            // 如果是资源路径，直接处理
            if (path.StartsWith("avares://") || path.StartsWith("/Assets/"))
            {
                var uri = path.StartsWith("/") ? new Uri($"avares://MaterialClient.UI{path}") : new Uri(path);
                var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }

            // 如果路径是本地文件路径，转换为绝对路径后加载
            // This ensures images load correctly even when app starts from System32
            var absolutePath = PathManager.ToAbsolutePath(path);
            if (File.Exists(absolutePath)) return new Bitmap(absolutePath);

            return null;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}