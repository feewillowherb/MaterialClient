using MaterialClient.Common.Providers;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌号文本处理辅助类
/// </summary>
public static class HikvisionPlateNumberHelper
{
    private static readonly string[] KnownColorPrefixes =
    [
        "民航黑色",
        "民航绿色",
        "黄绿色",
        "渐变绿色",
        "蓝色",
        "黄色",
        "黑色",
        "白色",
        "绿色",
        "其他",
        "蓝",
        "黄",
        "黑",
        "白",
        "绿"
    ];

    /// <summary>
    ///     解析海康 <c>sLicense</c>：提取纯车牌号与车牌颜色（颜色优先取自文本前缀，否则使用 fallback）。
    /// </summary>
    public static HikvisionPlateLicense ParseLicense(string? plateRaw, string? fallbackPlateColor = null)
    {
        var plateNumber = StripColorPrefix(plateRaw);
        var plateColor = TryExtractColorFromPrefix(plateRaw, plateNumber)
                         ?? NormalizePlateColor(fallbackPlateColor);
        return new HikvisionPlateLicense(plateNumber, plateColor);
    }

    /// <summary>
    ///     去除海康 <c>sLicense</c> 中嵌在车牌号前的颜色前缀（如「蓝」「黄色」）。
    /// </summary>
    public static string StripColorPrefix(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return plateNumber ?? string.Empty;
        }

        var trimmed = plateNumber.Trim();

        var wjIndex = trimmed.IndexOf("WJ", StringComparison.Ordinal);
        if (wjIndex >= 0)
        {
            return trimmed[wjIndex..];
        }

        var provinceIndex = FindFirstProvinceIndex(trimmed);
        if (provinceIndex > 0)
        {
            return trimmed[provinceIndex..].Trim();
        }

        return StripKnownColorPrefix(trimmed);
    }

    /// <summary>
    ///     将车牌颜色规范化为不含「色」字的短形式（如「蓝色」→「蓝」）。
    /// </summary>
    public static string? NormalizePlateColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var normalized = color.Replace("色", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string? TryExtractColorFromPrefix(string? plateRaw, string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateRaw))
        {
            return null;
        }

        var trimmed = plateRaw.Trim();
        if (string.Equals(trimmed, plateNumber, StringComparison.Ordinal))
        {
            return null;
        }

        var prefix = trimmed[..(trimmed.Length - plateNumber.Length)].Trim();
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        return NormalizePlateColor(prefix);
    }

    private static int FindFirstProvinceIndex(string text)
    {
        var earliest = int.MaxValue;
        foreach (var province in PlateNumberValidator.GetSupportedProvinces())
        {
            var index = text.IndexOf(province, StringComparison.Ordinal);
            if (index >= 0 && index < earliest)
            {
                earliest = index;
            }
        }

        return earliest == int.MaxValue ? -1 : earliest;
    }

    private static string StripKnownColorPrefix(string text)
    {
        foreach (var prefix in KnownColorPrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..].Trim();
            }
        }

        return text;
    }
}
