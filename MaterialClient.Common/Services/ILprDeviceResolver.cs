using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.Vzvision;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Resolves the LPR device implementation for a given device type.
/// </summary>
public interface ILprDeviceResolver
{
    /// <summary>
    ///     Gets the <see cref="ILprDevice"/> implementation for the given <paramref name="type"/>.
    /// </summary>
    /// <param name="type">LPR device type (Hikvision, Vzvision, Huaxiazhixin).</param>
    /// <returns>The corresponding LPR device instance.</returns>
    ILprDevice GetDevice(LprDeviceType type);
}

/// <summary>
///     Resolves the LPR device implementation by <see cref="LprDeviceType"/>.
/// </summary>
public class LprDeviceResolver : ILprDeviceResolver, ISingletonDependency
{
    private readonly IHikvisionLprService _hikvisionLprService;
    private readonly IVzvisionLprService _vzvisionLprService;
    private readonly HuaxiazhixinLprService _huaxiazhixinLprService;

    public LprDeviceResolver(
        IHikvisionLprService hikvisionLprService,
        IVzvisionLprService vzvisionLprService,
        HuaxiazhixinLprService huaxiazhixinLprService)
    {
        _hikvisionLprService = hikvisionLprService;
        _vzvisionLprService = vzvisionLprService;
        _huaxiazhixinLprService = huaxiazhixinLprService;
    }

    /// <inheritdoc />
    public ILprDevice GetDevice(LprDeviceType type)
    {
        return type switch
        {
            LprDeviceType.Hikvision => (ILprDevice)_hikvisionLprService,
            LprDeviceType.Vzvision => (ILprDevice)_vzvisionLprService,
            LprDeviceType.Huaxiazhixin => _huaxiazhixinLprService,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown LPR device type.")
        };
    }
}
