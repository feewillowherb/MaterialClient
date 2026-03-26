namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     道闸 I/O 控制方式枚举
/// </summary>
public enum GateIoControlMode
{
    /// <summary>
    ///     通过 LRP SDK 控制道闸 I/O（当前实现，默认）
    /// </summary>
    LrpSdk = 1,

    /// <summary>
    ///     直接通过 COM 控制道闸 I/O（预留，暂不支持）
    /// </summary>
    DirectCom = 2
}
