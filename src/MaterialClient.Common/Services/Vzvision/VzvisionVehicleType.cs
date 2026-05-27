using System.ComponentModel;

namespace MaterialClient.Common.Services.Vzvision;

/// <summary>
///     车型分类（与 Vz SDK 车辆抓拍信息一致）
/// </summary>
public enum VzvisionVehicleType
{
    [Description("未知")]
    Unknown = 0,

    [Description("小型车")]
    Small = 1,

    [Description("中型车")]
    Medium = 2,

    [Description("大型车")]
    Large = 3,

    [Description("特大型车")]
    ExtraLarge = 4,

    [Description("货车")]
    Truck = 5,

    [Description("客车")]
    Bus = 6,

    [Description("轿车")]
    Sedan = 7,

    [Description("SUV")]
    SUV = 8,

    [Description("MPV")]
    MPV = 9,

    [Description("其他")]
    Other = 99
}