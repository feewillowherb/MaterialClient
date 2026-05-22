using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     车牌识别设备类型
/// </summary>
public enum LprDeviceType
{
    [Description("海康威视")]
    Hikvision = 0,
    
    /// <summary>臻识一体机（Vz SDK，原 LprAllInOne 枚举名；持久化 JSON 中数值仍为 1）</summary>
    [Description("臻识车牌识别")]
    Vzvision = 1,
    
    [Description("华夏智信")]
    Huaxiazhixin = 2
}
