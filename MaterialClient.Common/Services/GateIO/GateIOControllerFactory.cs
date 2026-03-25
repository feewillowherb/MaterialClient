using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.GateIO;

/// <summary>
///     道闸 IO 控制器工厂类
///     根据设备类型创建对应的 IO 控制器实例
/// </summary>
public static class GateIOControllerFactory
{
    /// <summary>
    ///     根据 LPR 设备类型创建 IO 控制器
    /// </summary>
    public static IGateIOController Create(LprDeviceType deviceType, IServiceProvider serviceProvider)
    {
        return deviceType switch
        {
            LprDeviceType.Vzvision => ActivatorUtilities.CreateInstance<VzLPRGateIOController>(serviceProvider),
            _ => throw new NotSupportedException($"设备类型 {deviceType} 暂不支持道闸 IO 功能")
        };
    }

    /// <summary>
    ///     创建默认的 Vzvision IO 控制器
    /// </summary>
    public static IGateIOController CreateDefault(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<VzLPRGateIOController>(serviceProvider);
    }
}
