using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Utils;

/// <summary>
///     JPEG post-capture compression utility.
///     All compression is performed in memory using MemoryStream to avoid GDI+ file save issues.
///     Fail-safe: never throws — returns false on any error, preserving the original data.
/// </summary>
public static class JpegCompressionUtil
{
    /// <summary>
    ///     Lrp 图片压缩质量（90），保持车牌识别清晰度同时减少存储空间。
    ///     适用于 UrbanMode = 201 的车牌识别图片压缩。
    /// </summary>
    public const int LrpCompressionQuality = 90;

    /// <summary>
    ///     Try to compress JPEG bytes in memory by re-encoding at the specified quality.
    ///     Quality >= 100 returns the original bytes immediately (skip, zero overhead).
    ///     On any exception, logs a warning and returns null (caller should use original bytes).
    /// </summary>
    /// <param name="jpegBytes">Original JPEG bytes.</param>
    /// <param name="quality">Target JPEG quality (1-100). Values >= 100 skip compression.</param>
    /// <param name="logger">Optional logger for warnings on failure.</param>
    /// <returns>Compressed JPEG bytes, or null if compression failed (use original bytes).</returns>
    public static byte[]? TryCompressJpegBytes(byte[] jpegBytes, int quality, ILogger? logger)
    {
        if (quality >= 100)
            return jpegBytes;

        try
        {
            using var ms = new MemoryStream(jpegBytes);
            using var originalBitmap = new Bitmap(ms);

            var jpegCodec = GetJpegCodecInfo();
            if (jpegCodec == null)
            {
                logger?.LogWarning("JPEG codec not found, skipping compression");
                return null;
            }

            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

            using var outputMs = new MemoryStream();
            originalBitmap.Save(outputMs, jpegCodec, encoderParams);
            return outputMs.ToArray();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "JPEG compression failed, using original bytes");
            return null;
        }
    }

    /// <summary>
    ///     Try to compress a JPEG file by reading it into memory, re-encoding, and overwriting.
    ///     Quality >= 100 returns true immediately (skip, zero overhead).
    ///     On any exception, logs a warning and returns false; the original file remains intact.
    /// </summary>
    /// <param name="filePath">Full path to the JPEG file.</param>
    /// <param name="quality">Target JPEG quality (1-100). Values >= 100 skip compression.</param>
    /// <param name="logger">Optional logger for warnings on failure.</param>
    /// <returns>True if compression succeeded or was skipped; false on error.</returns>
    public static bool TryCompressJpeg(string filePath, int quality, ILogger? logger)
    {
        if (quality >= 100)
            return true;

        try
        {
            var originalBytes = File.ReadAllBytes(filePath);
            var compressedBytes = TryCompressJpegBytes(originalBytes, quality, logger);
            if (compressedBytes == null)
                return false;

            File.WriteAllBytes(filePath, compressedBytes);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "JPEG compression failed, original file preserved: {FilePath}", filePath);
            return false;
        }
    }

    private static ImageCodecInfo? GetJpegCodecInfo()
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
    }
}
