using System;

namespace MaterialClient.Common.Models;

/// <summary>
/// 字符串扩展方法。
/// </summary>
public static class StringStableHashExtensions
{
    /// <summary>
    /// 确定性稳定哈希（基于 FNV-1a 32-bit），跨进程、跨运行时结果一致。
    /// 供 SelectionItem.FromStreet 使用。
    /// </summary>
    public static int GetStableHashCode(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        const uint fnvPrime = 16777619u;
        const uint fnvOffset = 2166136261u;

        uint hash = fnvOffset;
        foreach (char c in text)
        {
            hash ^= (uint)c;
            hash *= fnvPrime;
        }

        return (int)hash;
    }
}
