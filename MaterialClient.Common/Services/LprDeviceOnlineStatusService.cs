using System;
using System.Collections.Generic;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.LprAllInOne;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     统一 LPR 设备在线状态查询接口
///     对 Hikvision、LprAllInOne、华夏智信 三种设备类型提供单一 API
/// </summary>
public interface ILprDeviceOnlineStatusService
{
    /// <summary>
    ///     判断指定类型的 LPR 设备是否在线
    /// </summary>
    /// <param name="deviceType">设备类型</param>
    /// <param name="config">设备配置（Hikvision 需完整配置，LprAllInOne/华夏智信 主要使用 Ip）</param>
    /// <returns>设备在线返回 true，否则 false；配置无效（如 Ip 为空）返回 false</returns>
    bool IsOnline(LprDeviceType deviceType, LicensePlateRecognitionConfig config);

    /// <summary>
    ///     批量查询指定类型的 LPR 设备在线状态（便于 UI 列表展示）
    /// </summary>
    /// <param name="deviceType">设备类型</param>
    /// <param name="configs">设备配置列表</param>
    /// <returns>每个配置及其在线状态的只读列表</returns>
    IReadOnlyList<(LicensePlateRecognitionConfig Config, bool IsOnline)> GetOnlineStatuses(
        LprDeviceType deviceType,
        IReadOnlyList<LicensePlateRecognitionConfig> configs);
}

/// <summary>
///     统一 LPR 设备在线状态服务实现
///     按设备类型委托给 IHikvisionLprService / ILprAllInOneService / IHuaxiazhixinLprOnlineState
/// </summary>
public class LprDeviceOnlineStatusService : ILprDeviceOnlineStatusService, ISingletonDependency
{
    private static readonly TimeSpan DefaultTimeoutLprAllInOne = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DefaultTimeoutHuaxiazhixin = TimeSpan.FromSeconds(30);

    private readonly IHikvisionLprService _hikvisionLprService;
    private readonly ILprAllInOneService _lprAllInOneService;
    private readonly IHuaxiazhixinLprOnlineState _huaxiazhixinLprOnlineState;

    public LprDeviceOnlineStatusService(
        IHikvisionLprService hikvisionLprService,
        ILprAllInOneService lprAllInOneService,
        IHuaxiazhixinLprOnlineState huaxiazhixinLprOnlineState)
    {
        _hikvisionLprService = hikvisionLprService ?? throw new ArgumentNullException(nameof(hikvisionLprService));
        _lprAllInOneService = lprAllInOneService ?? throw new ArgumentNullException(nameof(lprAllInOneService));
        _huaxiazhixinLprOnlineState = huaxiazhixinLprOnlineState ?? throw new ArgumentNullException(nameof(huaxiazhixinLprOnlineState));
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
            LprDeviceType.LprAllInOne => _lprAllInOneService.IsOnline(config.Ip, DefaultTimeoutLprAllInOne),
            LprDeviceType.Huaxiazhixin => _huaxiazhixinLprOnlineState.IsOnline(config.Ip, DefaultTimeoutHuaxiazhixin),
            _ => false
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<(LicensePlateRecognitionConfig Config, bool IsOnline)> GetOnlineStatuses(
        LprDeviceType deviceType,
        IReadOnlyList<LicensePlateRecognitionConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return Array.Empty<(LicensePlateRecognitionConfig Config, bool IsOnline)>();

        var list = new List<(LicensePlateRecognitionConfig Config, bool IsOnline)>(configs.Count);
        foreach (var config in configs)
        {
            var isOnline = IsOnline(deviceType, config);
            list.Add((config, isOnline));
        }
        return list;
    }
}
