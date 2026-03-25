using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     道闸 IO 状态枚举
/// </summary>
public enum GateIOState
{
    /// <summary>
    ///     空闲状态，无车辆在磅，道闸可响应识别
    /// </summary>
    [Description("空闲")]
    Idle = 0,

    /// <summary>
    ///     锁定状态，车辆上磅未稳定，所有道闸被锁定并持续写入 0
    /// </summary>
    [Description("锁定中")]
    Locked = 1,

    /// <summary>
    ///     开闸状态，地磅稳定后正在开闸
    /// </summary>
    [Description("开闸中")]
    Opening = 2,

    /// <summary>
    ///     异常状态，需要人工干预
    /// </summary>
    [Description("异常")]
    Error = 3
}
