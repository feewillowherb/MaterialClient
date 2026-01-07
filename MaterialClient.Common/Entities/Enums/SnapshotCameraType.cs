using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

public enum SnapshotCameraType
{
    [Description("海康威视")]
    Hikvision = 0,
    
    [Description("车牌识别一体机")]
    LPRAllInOne = 1
}