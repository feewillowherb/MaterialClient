using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.GateIO;

/// <summary>
///     Vzvision LPR 道闸 IO 控制器实现
///     包含 LPR Direction 到 GateIO Direction 的映射逻辑
/// </summary>
public sealed class VzLPRGateIOController : IGateIOController, ITransientDependency
{
    private readonly IVzvisionLprService _vzvisionLprService;
    private readonly ILogger<VzLPRGateIOController>? _logger;
    private readonly ConcurrentDictionary<string, int> _ipToHandle = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public VzLPRGateIOController(
        IVzvisionLprService vzvisionLprService,
        ILogger<VzLPRGateIOController>? logger = null)
    {
        _vzvisionLprService = vzvisionLprService;
        _logger = logger;
    }

    /// <summary>
    ///     设置设备句柄（用于 IO 控制操作）
    /// </summary>
    public void SetDeviceHandle(string deviceIp, int handle)
    {
        _ipToHandle[deviceIp] = handle;
    }

    /// <inheritdoc />
    public Task<GateIOConfigurationValidationResult> ValidateConfigurationAsync(LicensePlateRecognitionConfig config)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("配置不能为空");
            return Task.FromResult(GateIOConfigurationValidationResult.Failed(errors.ToArray()));
        }

        if (!config.EnableGateIo)
        {
            return Task.FromResult(GateIOConfigurationValidationResult.Success());
        }

        // 注意：设备类型验证在更高层次处理（GateIOConfigurationValidator 或 DeviceManagerService）
        // 这里只验证配置的完整性

        // 验证 IoChannel
        if (string.IsNullOrEmpty(config.IoChannel))
        {
            errors.Add("IoChannel 不能为空");
        }
        else if (!uint.TryParse(config.IoChannel, out _))
        {
            errors.Add($"IoChannel 格式无效: {config.IoChannel}");
        }

        return Task.FromResult(errors.Count == 0
            ? GateIOConfigurationValidationResult.Success()
            : GateIOConfigurationValidationResult.Failed(errors.ToArray()));
    }

    /// <inheritdoc />
    public async Task OpenGateAsync(GateIODirection direction, int durationMs = 500)
    {
        await Task.CompletedTask;
        _logger?.LogWarning("OpenGateAsync(GateIODirection) 需要配置参数，请使用重载方法 OpenGateAsync(LicensePlateRecognitionConfig, int)");
    }

    /// <inheritdoc />
    public async Task CloseGateAsync(GateIODirection direction)
    {
        await Task.CompletedTask;
        _logger?.LogWarning("CloseGateAsync(GateIODirection) 需要配置参数，请使用重载方法 CloseGateAsync(LicensePlateRecognitionConfig)");
    }

    /// <inheritdoc />
    public async Task WriteOutputAsync(GateIODirection direction, bool value)
    {
        await Task.CompletedTask;
        _logger?.LogWarning("WriteOutputAsync(GateIODirection, bool) 需要配置参数，请使用重载方法 WriteOutputAsync(LicensePlateRecognitionConfig, bool)");
    }

    /// <summary>
    ///     打开指定配置的道闸
    /// </summary>
    public async Task OpenGateAsync(LicensePlateRecognitionConfig config, int durationMs = 500)
    {
        if (config == null || !config.EnableGateIo)
            return;

        if (!uint.TryParse(config.IoChannel, out var ioChannel))
        {
            _logger?.LogWarning("I/O 通道配置无效，跳过开闸: IoChannel={IoChannel}", config.IoChannel);
            return;
        }

        await _vzvisionLprService.SetIoOutputAutoRespAsync(config, ioChannel, durationMs);
        _logger?.LogInformation("已开闸: Device={Name}, IoChannel={IoChannel}, DurationMs={DurationMs}",
            config.Name, ioChannel, durationMs);
    }

    /// <summary>
    ///     关闭指定配置的道闸（写入 0）
    /// </summary>
    public async Task CloseGateAsync(LicensePlateRecognitionConfig config)
    {
        await WriteOutputAsync(config, false);
    }

    /// <summary>
    ///     向指定配置的道闸写入输出值（直接调用 SDK）
    /// </summary>
    public async Task WriteOutputAsync(LicensePlateRecognitionConfig config, bool value)
    {
        if (config == null || !config.EnableGateIo)
            return;

        if (!uint.TryParse(config.IoChannel, out var ioChannel))
        {
            _logger?.LogWarning("I/O 通道配置无效，跳过写入: IoChannel={IoChannel}", config.IoChannel);
            return;
        }

        if (!_ipToHandle.TryGetValue(config.Ip, out var handle) || handle == 0)
        {
            _logger?.LogWarning("设备句柄未找到，跳过写入: Device={Name}", config.Name);
            return;
        }

        // 调用 Vzvision SDK 设置 IO 输出
        // nOutput=0 表示开路（关闭），nOutput=1 表示闭路（打开）
        var nOutput = value ? 1 : 0;
        var ret = VzvisionSdk.VzLPRClient_SetIOOutput(handle, ioChannel, nOutput);

        if (ret != 0)
        {
            _logger?.LogWarning("VzLPRClient_SetIOOutput 返回非零: {Ret}, Device={Name}, IoChannel={IoChannel}, Value={Value}",
                ret, config.Name, ioChannel, value);
        }
        else
        {
            _logger?.LogDebug("写入 IO 输出: Device={Name}, IoChannel={IoChannel}, Value={Value}",
                config.Name, ioChannel, value);
        }
    }

    /// <summary>
    ///     映射 LPR Direction 到 GateIO Direction
    /// </summary>
    public static GateIODirection MapLprDirectionToGateIODirection(LicensePlateDirection lprDirection)
    {
        return lprDirection switch
        {
            LicensePlateDirection.In => GateIODirection.Entry,
            LicensePlateDirection.Out => GateIODirection.Exit,
            _ => throw new ArgumentException($"不支持的方向: {lprDirection}")
        };
    }

    /// <summary>
    ///     映射 GateIO Direction 到 LPR Direction
    /// </summary>
    public static LicensePlateDirection MapGateIODirectionToLprDirection(GateIODirection gateIoDirection)
    {
        return gateIoDirection switch
        {
            GateIODirection.Entry => LicensePlateDirection.In,
            GateIODirection.Exit => LicensePlateDirection.Out,
            _ => throw new ArgumentException($"不支持的方向: {gateIoDirection}")
        };
    }
}
