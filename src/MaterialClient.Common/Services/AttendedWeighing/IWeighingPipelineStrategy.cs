using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     称重管线策略接口
///     定义称重流程中可扩展的扩展点，用于区分不同称重模式的行为差异
/// </summary>
public interface IWeighingPipelineStrategy
{
    /// <summary>
    ///     是否跳过运单匹对（TryMatchEvent 发布）
    /// </summary>
    bool ShouldSkipWaybillMatching();

    /// <summary>
    ///     状态转换时的扩展点
    ///     在 AttendedWeighingService.ProcessStatusTransition 中调用
    /// </summary>
    Task OnStatusTransitionAsync(AttendedWeighingStatus previousStatus, AttendedWeighingStatus newStatus,
        decimal weight);
}
