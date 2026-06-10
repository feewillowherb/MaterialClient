using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.AttendedWeighing;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

/// <summary>
///     城管称重管线策略
///     UrbanMode 下跳过运单匹对（TryMatchEvent）和 waybill 匹对逻辑
/// </summary>
[AutoConstructor]
public partial class UrbanWeighingPipelineStrategy : IWeighingPipelineStrategy, ISingletonDependency
{
    private readonly ILogger<UrbanWeighingPipelineStrategy> _logger;


    /// <inheritdoc />
    public bool ShouldSkipWaybillMatching()
    {
        _logger.LogDebug("UrbanMode: 跳过运单匹对");
        return true;
    }

    /// <inheritdoc />
    public Task OnStatusTransitionAsync(AttendedWeighingStatus previousStatus, AttendedWeighingStatus newStatus,
        decimal weight)
    {
        _logger.LogDebug("UrbanMode: 状态转换 {PreviousStatus} -> {NewStatus}, 跳过 waybill 匹对逻辑",
            previousStatus, newStatus);
        return Task.CompletedTask;
    }
}