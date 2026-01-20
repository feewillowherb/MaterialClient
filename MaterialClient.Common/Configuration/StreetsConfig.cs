using System;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     街道配置
/// </summary>
public class StreetsConfig
{
    /// <summary>
    ///     街道列表
    /// </summary>
    public string[] Streets { get; set; } = Array.Empty<string>();
}