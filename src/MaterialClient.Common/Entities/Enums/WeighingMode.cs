using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     称重模式
/// </summary>
public enum WeighingMode
{
    /// <summary>
    ///     标准模式
    /// </summary>
    [Description("物料验收系统客户端软件")] Standard = 0,

    /// <summary>
    ///     固废模式
    /// </summary>
    [Description("城管固废称重验收系统客户端软件")] SolidWaste = 1,

    /// <summary>
    ///     城管专用模式（Urban 桌面端）
    /// </summary>
    [Description("城管固废称重验收系统客户端软件")] UrbanMode = 201
}