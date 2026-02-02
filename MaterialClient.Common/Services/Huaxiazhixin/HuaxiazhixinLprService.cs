using System;
using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Huaxiazhixin;

/// <summary>
///     华夏智信设备在线状态查询接口
///     用于根据心跳记录判断设备是否在线
/// </summary>
public interface IHuaxiazhixinLprOnlineState
{
    /// <summary>
    ///     记录设备最后心跳时间（用于在线状态判断）
    /// </summary>
    /// <param name="deviceIp">设备IP地址（cam_ip）</param>
    void RecordLastSeen(string deviceIp);

    /// <summary>
    ///     根据最后心跳时间判断设备是否在线
    /// </summary>
    /// <param name="deviceIp">设备IP地址</param>
    /// <param name="timeout">超过此时间未收到心跳视为离线；null 使用默认 30 秒</param>
    /// <returns>若在 timeout 内有过心跳则 true，否则 false</returns>
    bool IsOnline(string deviceIp, TimeSpan? timeout = null);
}

/// <summary>
///     华夏智信车牌识别服务占位实现
///     厂商不支持主动抓拍,此服务明确标记此限制
/// </summary>
public class HuaxiazhixinLprService : ILprDevice, IHuaxiazhixinLprOnlineState, ISingletonDependency
{
    private readonly ILogger<HuaxiazhixinLprService>? _logger;

    /// <summary>
    ///     默认在线超时：超过此时间未收到心跳视为离线（设备约 10 秒一次心跳）
    /// </summary>
    private static readonly TimeSpan DefaultOnlineTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, DateTime> _lastSeenUtcByIp = new();

    public HuaxiazhixinLprService(ILogger<HuaxiazhixinLprService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     华夏智信设备不支持主动抓拍
    /// </summary>
    public bool SupportsActiveCapture => false;

    /// <summary>
    ///     华夏智信设备不支持主动抓拍,此方法抛出 NotSupportedException
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <exception cref="NotSupportedException">
    ///     总是抛出此异常,因为华夏智信厂商不支持主动抓拍功能
    /// </exception>
    public Task TriggerCaptureAsync(LicensePlateRecognitionConfig config)
    {
        _logger?.LogWarning(
            "华夏智信设备不支持主动抓拍: {Device} (IP: {Ip})",
            config.Name, config.Ip);

        throw new NotSupportedException(
            "华夏智信厂商不支持主动抓拍功能。设备仅支持被动捕获模式。");
    }

    /// <inheritdoc />
    public void RecordLastSeen(string deviceIp)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
            return;
        _lastSeenUtcByIp.AddOrUpdate(deviceIp, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    /// <inheritdoc />
    public bool IsOnline(string deviceIp, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
            return false;
        var effectiveTimeout = timeout ?? DefaultOnlineTimeout;
        if (_lastSeenUtcByIp.TryGetValue(deviceIp, out var lastSeen))
            return DateTime.UtcNow - lastSeen <= effectiveTimeout;
        return false;
    }
}
