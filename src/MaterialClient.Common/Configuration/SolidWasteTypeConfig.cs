using System;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     固废类型配置
/// </summary>
public class SolidWasteTypeConfig
{
    /// <summary>
    ///     固废类型列表
    /// </summary>
    public string[] SolidWasteTypes { get; set; } = Array.Empty<string>();
}