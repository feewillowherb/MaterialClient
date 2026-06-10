using System.ComponentModel;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     车型分类（与海康 SDK 车辆抓拍信息一致）
/// </summary>
public enum HikvisionVehicleType
{
    [Description("未知")]
    Unknown = 0,

    [Description("小轿车")]
    Sedan = 1,

    [Description("SUV")]
    SUV = 2,

    [Description("MPV")]
    MPV = 3,

    [Description("货车")]
    Truck = 4,

    [Description("客车")]
    Bus = 5,

    [Description("面包车")]
    Van = 6,

    [Description("皮卡")]
    Pickup = 7,

    [Description("其他")]
    Other = 99
}