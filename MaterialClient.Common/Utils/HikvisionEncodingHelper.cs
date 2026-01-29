using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Utils;

/// <summary>
///     海康威视编码辅助工具类
///     用于处理车牌识别结果中的中文字符（GBK 编码）
/// </summary>
public static class HikvisionEncodingHelper
{
    private static Encoding? _gbkEncoding;
    private static readonly object _lock = new();

    /// <summary>
    ///     获取 GBK 编码
    ///     如果系统不支持 GBK，则回退到 UTF-8 并记录警告
    /// </summary>
    private static Encoding GetGbkEncoding(ILogger? logger = null)
    {
        if (_gbkEncoding != null)
        {
            return _gbkEncoding;
        }

        lock (_lock)
        {
            if (_gbkEncoding != null)
            {
                return _gbkEncoding;
            }

            try
            {
                // 尝试获取 GBK 编码
                _gbkEncoding = Encoding.GetEncoding("GBK");
                logger?.LogDebug("使用 GBK 编码处理海康威视车牌识别结果");
            }
            catch (NotSupportedException)
            {
                // 系统不支持 GBK 编码，回退到 UTF-8
                _gbkEncoding = Encoding.UTF8;
                logger?.LogWarning(
                    "系统不支持 GBK 编码，将使用 UTF-8 编码处理车牌识别结果。这可能导致中文字符显示不正确。");
            }

            return _gbkEncoding;
        }
    }

    /// <summary>
    ///     从非托管指针读取 GBK 编码的字符串
    /// </summary>
    /// <param name="ptr">非托管指针</param>
    /// <param name="maxLength">最大长度（字节）</param>
    /// <param name="logger">可选的日志记录器</param>
    /// <returns>解码后的字符串</returns>
    public static string GetStringFromPtr(IntPtr ptr, int maxLength, ILogger? logger = null)
    {
        if (ptr == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            // 分配托管字节数组
            var buffer = new byte[maxLength];

            // 从非托管内存复制到托管数组
            Marshal.Copy(ptr, buffer, 0, maxLength);

            // 找到字符串结尾（null 终止符）
            var length = 0;
            for (var i = 0; i < maxLength; i++)
            {
                if (buffer[i] == 0)
                {
                    length = i;
                    break;
                }
            }

            // 如果没有找到 null 终止符，使用整个缓冲区
            if (length == 0)
            {
                length = maxLength;
            }

            // 使用 GBK 编码转换为字符串
            var encoding = GetGbkEncoding(logger);
            return encoding.GetString(buffer, 0, length).TrimEnd('\0');
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "从指针读取 GBK 字符串失败");
            return string.Empty;
        }
    }

    /// <summary>
    ///     将字符串转换为 GBK 编码的字节数组
    /// </summary>
    /// <param name="text">要转换的字符串</param>
    /// <param name="logger">可选的日志记录器</param>
    /// <returns>GBK 编码的字节数组</returns>
    public static byte[] GetBytes(string text, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<byte>();
        }

        try
        {
            var encoding = GetGbkEncoding(logger);
            return encoding.GetBytes(text);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "将字符串转换为 GBK 字节数组失败");
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    ///     从字节数组读取 GBK 编码的字符串
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <param name="logger">可选的日志记录器</param>
    /// <returns>解码后的字符串</returns>
    public static string GetString(byte[] bytes, ILogger? logger = null)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            // 找到字符串结尾（null 终止符）
            var length = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                {
                    length = i;
                    break;
                }
            }

            // 如果没有找到 null 终止符，使用整个数组
            if (length == 0)
            {
                length = bytes.Length;
            }

            var encoding = GetGbkEncoding(logger);
            return encoding.GetString(bytes, 0, length).TrimEnd('\0');
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "从字节数组读取 GBK 字符串失败");
            return string.Empty;
        }
    }
}
