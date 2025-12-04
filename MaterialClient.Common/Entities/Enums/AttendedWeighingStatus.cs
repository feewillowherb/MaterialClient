namespace MaterialClient.Common.Entities.Enums;

/// <summary>
/// 有人值守称重状态枚举
/// </summary>
public enum AttendedWeighingStatus
{
    /// <summary>
    /// 下称
    /// </summary>
    下称 = 0,

    /// <summary>
    /// 上称等待重量稳定
    /// </summary>
    上称等待重量稳定 = 1,

    /// <summary>
    /// 上称重量已稳定
    /// </summary>
    上称重量已稳定 = 2
}
