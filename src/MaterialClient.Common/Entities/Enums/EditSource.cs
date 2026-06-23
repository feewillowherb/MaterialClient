using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     称重记录修改来源
/// </summary>
public enum EditSource
{
    /// <summary>
    ///     客户端（MaterialClient）
    /// </summary>
    [Description("客户端")] Client = 0,

    /// <summary>
    ///     服务端（UrbanManagement Web）
    /// </summary>
    [Description("服务端")] Server = 1
}
