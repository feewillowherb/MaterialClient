using System.Text.RegularExpressions;

namespace MaterialClient.Common.Providers;

/// <summary>
///     车牌号验证提供者
/// </summary>
public static class PlateNumberValidator
{
    /// <summary>
    ///     验证指定的车牌号是否为有效的中国车牌号
    /// </summary>
    /// <param name="plateNumber">要验证的车牌号</param>
    /// <returns>如果车牌号有效返回true，否则返回false</returns>
    public static bool IsValidChinesePlateNumber(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return false;

        // 移除空格
        var normalizedPlateNumber = plateNumber.Trim();

        // 验证是否匹配任一格式
        return IsNormalPlate(normalizedPlateNumber) ||
               IsNewEnergyPlate(normalizedPlateNumber) ||
               IsPolicePlate(normalizedPlateNumber);
    }

    /// <summary>
    ///     验证是否为普通车牌
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>如果是普通车牌返回true，否则返回false</returns>
    public static bool IsNormalPlate(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return false;

        // 普通车牌格式：省份简称 + 字母 + 5位数字/字母组合（7位）
        // 例如：京A12345、粤B88888
        // 排除字母I和O，避免与数字1和0混淆
        var pattern = @"^[京津冀晋蒙辽吉黑沪苏浙皖闽赣鲁豫鄂湘粤桂琼渝川贵云藏陕甘青宁新港澳台使领][A-Z][A-HJ-NP-Z0-9]{5}$";
        return Regex.IsMatch(plateNumber.Trim(), pattern);
    }

    /// <summary>
    ///     验证是否为新能源车牌
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>如果是新能源车牌返回true，否则返回false</returns>
    public static bool IsNewEnergyPlate(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return false;

        // 新能源车牌格式：省份简称 + 字母 + 6位数字/字母组合（8位）
        // 小型新能源车：第6位为D或F，例如：京AD12345
        // 大型新能源车：第1位为D或F，例如：京AF12345
        var pattern = @"^[京津冀晋蒙辽吉黑沪苏浙皖闽赣鲁豫鄂湘粤桂琼渝川贵云藏陕甘青宁新港澳台][A-Z]([0-9DF][A-HJ-NP-Z0-9]{5}|[A-HJ-NP-Z0-9]{5}[DF])$";
        return Regex.IsMatch(plateNumber.Trim(), pattern);
    }

    /// <summary>
    ///     验证是否为武警车牌
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>如果是武警车牌返回true，否则返回false</returns>
    public static bool IsPolicePlate(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return false;

        // 武警车牌：WJ + 省份简称 + 4位数字 + 1位字母/数字（8位）
        // 例如：WJ京0001警
        var pattern = @"^WJ[京津冀晋蒙辽吉黑沪苏浙皖闽赣鲁豫鄂湘粤桂琼渝川贵云藏陕甘青宁新][0-9]{4}[A-HJ-NP-Z0-9]$";
        return Regex.IsMatch(plateNumber.Trim(), pattern);
    }

    /// <summary>
    ///     获取车牌号类型描述
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>车牌类型描述</returns>
    public static string GetPlateType(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return "无效车牌";

        var normalized = plateNumber.Trim();

        if (IsNewEnergyPlate(normalized)) return "新能源车牌";

        if (IsNormalPlate(normalized)) return "普通车牌";

        if (IsPolicePlate(normalized)) return "武警车牌";

        return "无效车牌";
    }

    /// <summary>
    ///     获取支持的所有省份简称
    /// </summary>
    /// <returns>省份简称数组</returns>
    public static string[] GetSupportedProvinces()
    {
        return new[]
        {
            "京", "津", "冀", "晋", "蒙", "辽", "吉", "黑",
            "沪", "苏", "浙", "皖", "闽", "赣", "鲁", "豫",
            "鄂", "湘", "粤", "桂", "琼", "渝", "川", "贵",
            "云", "藏", "陕", "甘", "青", "宁", "新",
            "港", "澳", "台", "使", "领"
        };
    }

    /// <summary>
    ///     格式化车牌号（移除空格，转换为大写）
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>格式化后的车牌号</returns>
    public static string? NormalizePlateNumber(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;

        return plateNumber.Trim().ToUpper();
    }
}