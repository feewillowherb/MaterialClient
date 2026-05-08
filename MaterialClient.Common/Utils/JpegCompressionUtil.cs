using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Utils;

/// <summary>
///     JPEG post-capture compression utility.
///     Loads a JPEG file, re-encodes it at the specified quality, and overwrites the original.
///     Fail-safe: never throws — returns false on any error, preserving the original file.
/// </summary>
public static class JpegCompressionUtil
{
    /// <summary>
    ///     Try to compress a JPEG file by re-encoding at the specified quality.
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
            using var originalBitmap = new Bitmap(filePath);

            // Get JPEG codec and encoder parameters
            var jpegCodec = GetJpegCodecInfo();
            if (jpegCodec == null)
            {
                logger?.LogWarning("JPEG codec not found, skipping compression: {FilePath}", filePath);
                return false;
            }

            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

            // GDI+ cannot save to the file that the Bitmap was loaded from;
            // write to a temp file first, then replace the original.
            var tempPath = filePath + ".tmp";
            originalBitmap.Save(tempPath, jpegCodec, encoderParams);
            originalBitmap.Dispose();
            File.Delete(filePath);
            File.Move(tempPath, filePath);
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
