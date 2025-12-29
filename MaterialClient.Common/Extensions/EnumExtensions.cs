using System.ComponentModel;
using System.Reflection;

namespace MaterialClient.Common.Extensions;

/// <summary>
///     枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    ///     获取枚举值的 Description 特性值
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>Description 特性值，如果没有则返回枚举名称</returns>
    public static string GetDescription(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        if (fieldInfo == null) return enumValue.ToString();

        var descriptionAttribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? enumValue.ToString();
    }
}

