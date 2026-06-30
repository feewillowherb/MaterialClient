using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     产品代码（与 WeighingMode 对应）
/// </summary>
public enum ProductCode
{
    /// <summary>
    ///     标准模式
    /// </summary>
    [Description("物料验收系统客户端软件")]
    Standard = 5000,

    /// <summary>
    ///     固废模式
    /// </summary>
    [Description("城管固废称重验收系统客户端软件")]
    SolidWaste = 5010,

    /// <summary>
    ///     城管专用产品代码（Urban 桌面端）
    /// </summary>
    [Description("城管地磅称重系统客户端软件")]
    Urban = 5001
}
