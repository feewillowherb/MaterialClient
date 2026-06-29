using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     默认称重管线策略（有人值守模式）
///     保持现有有人值守称重的全部行为不变
/// </summary>
public class DefaultWeighingPipelineStrategy : IWeighingPipelineStrategy, ISingletonDependency
{
    private readonly ILogger<DefaultWeighingPipelineStrategy> _logger;

    public DefaultWeighingPipelineStrategy(ILogger<DefaultWeighingPipelineStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool ShouldSkipWaybillMatching() => false;

    /// <inheritdoc />
    public Task OnStatusTransitionAsync(AttendedWeighingStatus previousStatus, AttendedWeighingStatus newStatus,
        decimal weight)
    {
        // Default: no-op, existing attended weighing behavior is preserved
        return Task.CompletedTask;
    }
}
