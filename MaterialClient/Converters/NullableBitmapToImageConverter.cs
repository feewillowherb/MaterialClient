using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MaterialClient.Converters;

/// <summary>
///     Converts Bitmap? to IImage for Image.Source: returns the bitmap when non-null, otherwise the default car placeholder.
/// </summary>
public class NullableBitmapToImageConverter : IValueConverter
{
    private const string DefaultCarImage = "avares://MaterialClient/Assets/Car_Default.png";
    private static Bitmap? _defaultBitmap;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Bitmap bitmap)
            return bitmap;

        if (_defaultBitmap == null)
        {
            var assets = AssetLoader.Open(new Uri(DefaultCarImage));
            _defaultBitmap = new Bitmap(assets);
        }

        return _defaultBitmap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
