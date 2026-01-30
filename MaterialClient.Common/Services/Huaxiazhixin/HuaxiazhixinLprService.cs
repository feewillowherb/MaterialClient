using System;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using System.Reactive;
using System.Reactive.Linq;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Huaxiazhixin;

/// <summary>
///     华夏智信车牌识别服务占位实现
///     厂商不支持主动抓拍,此服务明确标记此限制
/// </summary>
public class HuaxiazhixinLprService : ILprDevice, ISingletonDependency
{
    private readonly ILogger<HuaxiazhixinLprService>? _logger;

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
    /// <returns>永远不会返回,总是抛出 NotSupportedException</returns>
    /// <exception cref="NotSupportedException">
    ///     总是抛出此异常,因为华夏智信厂商不支持主动抓拍功能
    /// </exception>
    public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config)
    {
        _logger?.LogWarning(
            "华夏智信设备不支持主动抓拍: {Device} (IP: {Ip})",
            config.Name, config.Ip);

        return Observable.Throw<LicensePlateRecognizedEvent>(
            new NotSupportedException(
                "华夏智信厂商不支持主动抓拍功能。设备仅支持被动捕获模式。"));
    }
}
