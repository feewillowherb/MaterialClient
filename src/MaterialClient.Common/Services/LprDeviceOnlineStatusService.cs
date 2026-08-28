using System;
using System.Collections.Generic;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.Vzvision;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

public sealed record LprOnlineStatusItem(LicensePlateRecognitionConfig Config, bool IsOnline);

/// <summary>
///     统一 LPR 设备在线状态查询接口
///     对 Hikvision、Vzvision、华夏智信 三种设备类型提供单一 API
/// </summary>
public interface ILprDeviceOnlineStatusService
{
    /// <summary>
    ///     判断指定类型的 LPR 设备是否在线
    /// </summary>
    bool IsOnline(LprDeviceType deviceType, LicensePlateRecognitionConfig config);

    /// <summary>
    ///     按每行 <see cref="LicensePlateRecognitionConfig.ResolvedDeviceType"/> 批量查询在线状态
    /// </summary>
    IReadOnlyList<LprOnlineStatusItem> GetOnlineStatuses(
        IReadOnlyList<LicensePlateRecognitionConfig> configs);
}

/// <summary>
///     统一 LPR 设备在线状态服务实现
/// </summary>
public class LprDeviceOnlineStatusService : ILprDeviceOnlineStatusService, ISingletonDependency
{
    private static readonly TimeSpan DefaultTimeoutVzvision = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DefaultTimeoutHuaxiazhixin = TimeSpan.FromSeconds(30);

    private readonly IHikvisionLprService _hikvisionLprService;
    private readonly IVzvisionLprService _vzvisionLprService;
    private readonly IHuaxiazhixinLprService _huaxiazhixinLprService;

    public LprDeviceOnlineStatusService(
        IHikvisionLprService hikvisionLprService,
        IVzvisionLprService vzvisionLprService,
        IHuaxiazhixinLprService huaxiazhixinLprService)
    {
        _hikvisionLprService = hikvisionLprService ?? throw new ArgumentNullException(nameof(hikvisionLprService));
        _vzvisionLprService = vzvisionLprService ?? throw new ArgumentNullException(nameof(vzvisionLprService));
        _huaxiazhixinLprService = huaxiazhixinLprService ?? throw new ArgumentNullException(nameof(huaxiazhixinLprService));
    }

    /// <inheritdoc />
    public bool IsOnline(LprDeviceType deviceType, LicensePlateRecognitionConfig config)
    {
        if (config == null)
            return false;
        if (string.IsNullOrWhiteSpace(config.Ip))
            return false;

        return deviceType switch
        {
            LprDeviceType.Hikvision => _hikvisionLprService.IsOnline(config),
            LprDeviceType.Vzvision => _vzvisionLprService.IsOnline(config.Ip, DefaultTimeoutVzvision),
            LprDeviceType.Huaxiazhixin => _huaxiazhixinLprService.IsOnline(config.Ip, DefaultTimeoutHuaxiazhixin),
            _ => false
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<LprOnlineStatusItem> GetOnlineStatuses(
        IReadOnlyList<LicensePlateRecognitionConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return Array.Empty<LprOnlineStatusItem>();

        var list = new List<LprOnlineStatusItem>(configs.Count);
        foreach (var config in configs)
        {
            var isOnline = IsOnline(config.ResolvedDeviceType, config);
            list.Add(new LprOnlineStatusItem(config, isOnline));
        }

        return list;
    }
}
