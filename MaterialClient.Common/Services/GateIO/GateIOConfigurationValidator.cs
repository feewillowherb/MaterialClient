using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.GateIO;

/// <summary>
///     道闸 IO 配置验证器
///     验证进出口道闸配置的完整性和正确性
/// </summary>
public interface IGateIOConfigurationValidator
{
    /// <summary>
    ///     验证配置列表
    /// </summary>
    Task<GateIOConfigurationValidationResult> ValidateAsync(IEnumerable<LicensePlateRecognitionConfig> configs);
}

/// <inheritdoc />
public sealed class GateIOConfigurationValidator : IGateIOConfigurationValidator, ITransientDependency
{
    private readonly ILogger<GateIOConfigurationValidator>? _logger;

    public GateIOConfigurationValidator(ILogger<GateIOConfigurationValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GateIOConfigurationValidationResult> ValidateAsync(IEnumerable<LicensePlateRecognitionConfig> configs)
    {
        await Task.CompletedTask;

        _logger?.LogInformation("开始验证道闸 IO 配置");

        var errors = new List<string>();
        var configList = configs.ToList();

        // 筛选出启用了道闸 IO 的配置
        var gateIoConfigs = configList
            .Where(c => c.EnableGateIo)
            .ToList();

        // 如果没有任何配置启用道闸 IO，验证通过
        if (gateIoConfigs.Count == 0)
        {
            _logger?.LogInformation("道闸 IO 功能未启用，跳过验证");
            return GateIOConfigurationValidationResult.Success();
        }

        // 将 LPR Direction 映射到 GateIO Direction
        var gateIODirections = gateIoConfigs
            .Select(c => MapLprDirectionToGateIODirection(c.Direction))
            .ToList();

        // 规则 1：进出口必须成对配置
        var hasEntry = gateIODirections.Any(d => d == GateIODirection.Entry);
        var hasExit = gateIODirections.Any(d => d == GateIODirection.Exit);
        if (!hasEntry || !hasExit)
        {
            errors.Add("进出口道闸必须成对配置");
        }

        // 规则 2：每个方向最多一个
        var entryCount = gateIODirections.Count(d => d == GateIODirection.Entry);
        var exitCount = gateIODirections.Count(d => d == GateIODirection.Exit);

        if (entryCount > 1)
        {
            errors.Add($"进口道闸只能配置一个（当前配置了 {entryCount} 个）");
        }

        if (exitCount > 1)
        {
            errors.Add($"出口道闸只能配置一个（当前配置了 {exitCount} 个）");
        }

        // 规则 3：IoChannel 必须有效
        foreach (var config in gateIoConfigs)
        {
            if (string.IsNullOrEmpty(config.IoChannel))
            {
                var directionName = config.Direction == LicensePlateDirection.In ? "进口" : "出口";
                errors.Add($"{directionName}道闸（{config.Name}）的 IoChannel 不能为空");
            }
            else if (!uint.TryParse(config.IoChannel, out _))
            {
                var directionName = config.Direction == LicensePlateDirection.In ? "进口" : "出口";
                errors.Add($"{directionName}道闸（{config.Name}）的 IoChannel 格式无效: {config.IoChannel}");
            }
        }

        // 注意：设备类型能力验证在更高层次处理（DeviceManagerService 中只对 Vzvision 设备启动 GateIO 服务）

        if (errors.Count == 0)
        {
            _logger?.LogInformation("道闸 IO 配置验证通过，共 {Count} 个设备", gateIoConfigs.Count);
            return GateIOConfigurationValidationResult.Success();
        }
        else
        {
            _logger?.LogError("道闸 IO 配置验证失败，共 {Count} 个错误", errors.Count);
            foreach (var error in errors)
            {
                _logger?.LogError("  - {Error}", error);
            }
            return GateIOConfigurationValidationResult.Failed(errors.ToArray());
        }
    }

    /// <summary>
    ///     映射 LPR Direction 到 GateIO Direction
    /// </summary>
    private static GateIODirection MapLprDirectionToGateIODirection(LicensePlateDirection lprDirection)
    {
        return lprDirection switch
        {
            LicensePlateDirection.In => GateIODirection.Entry,
            LicensePlateDirection.Out => GateIODirection.Exit,
            _ => throw new ArgumentException($"不支持的方向: {lprDirection}")
        };
    }
}
