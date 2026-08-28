using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

public enum LprSiteType
{
    [Description("地磅")]
    Scale = 0,

    [Description("卡口")]
    Checkpoint = 1,

    [Description("成品")]
    FinishedProduct = 2
}
