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
    [Description("凡东智能物料验收系统V2.0")] Standard = 0,

    /// <summary>
    ///     固废模式
    /// </summary>
    [Description("城管固废称重验收系统客户端软件")] SolidWaste = 1,

    /// <summary>
    ///     城管专用模式（Urban 桌面端）
    /// </summary>
    [Description("城管固废称重验收系统客户端软件")] UrbanMode = 201,

    /// <summary>
    ///     资源化利用厂模式（Recycle 桌面端，前端功能同 SolidWaste，上报直连外部 §2.2 接口）
    /// </summary>
    [Description("资源化利用厂称重系统客户端软件")] Recycle = 301
}