using System.Reactive.Subjects;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     称重状态管理器
///     封装称重状态机的状态转换逻辑，使用 BehaviorSubject 维护当前状态
/// </summary>
public class WeighingStateManager : ISingletonDependency, IDisposable
{
    private readonly BehaviorSubject<AttendedWeighingStatus> _statusSubject = new(AttendedWeighingStatus.OffScale);
    private readonly BehaviorSubject<DeliveryType> _deliveryTypeSubject = new(DeliveryType.Receiving);
    private readonly BehaviorSubject<long?> _lastCreatedWeighingRecordIdSubject = new(null);
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<WeighingStateManager> _logger;

    private AttendedWeighingStatus _previousStatus = AttendedWeighingStatus.OffScale;

    public WeighingStateManager(
        ILocalEventBus localEventBus,
        ILogger<WeighingStateManager> logger)
    {
        _localEventBus = localEventBus;
        _logger = logger;
    }

    /// <summary>
    ///     获取当前称重状态
    /// </summary>
    public AttendedWeighingStatus GetCurrentStatus() => _statusSubject.Value;

    /// <summary>
    ///     获取上一个称重状态（用于状态转换检测）
    /// </summary>
    public AttendedWeighingStatus GetPreviousStatus() => _previousStatus;

    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    public DeliveryType CurrentDeliveryType => _deliveryTypeSubject.Value;

    /// <summary>
    ///     获取状态 BehaviorSubject（供流管道使用）
    /// </summary>
    internal BehaviorSubject<AttendedWeighingStatus> StatusSubject => _statusSubject;

    /// <summary>
    ///     获取记录 ID BehaviorSubject（供流管道使用）
    /// </summary>
    internal BehaviorSubject<long?> RecordIdSubject => _lastCreatedWeighingRecordIdSubject;

    /// <summary>
    ///     更新状态（由 Orchestrator 在收到状态流通知时调用）
    /// </summary>
    public void UpdateStatus(AttendedWeighingStatus newStatus)
    {
        var currentStatus = _statusSubject.Value;
        if (newStatus != currentStatus)
        {
            _previousStatus = currentStatus;
            _statusSubject.OnNext(newStatus);
            _logger.LogDebug("Status updated: {PreviousStatus} -> {NewStatus}", _previousStatus, newStatus);
        }
    }

    /// <summary>
    ///     设置收发料类型（变更时通过 ILocalEventBus 发送通知）
    /// </summary>
    public void SetDeliveryType(DeliveryType deliveryType)
    {
        if (_deliveryTypeSubject.Value != deliveryType)
        {
            _deliveryTypeSubject.OnNext(deliveryType);
            _logger.LogInformation("DeliveryType changed to {DeliveryType}", deliveryType);

            // Send ILocalEventBus notification
            _ = _localEventBus.PublishAsync(new DeliveryTypeChangedEventData(deliveryType));
        }
    }

    /// <summary>
    ///     设置最近创建的称重记录 ID
    /// </summary>
    public void SetLastCreatedWeighingRecordId(long recordId)
    {
        _lastCreatedWeighingRecordIdSubject.OnNext(recordId);
        _logger.LogInformation("Last created weighing record ID set to {RecordId}", recordId);
    }

    /// <summary>
    ///     获取最近创建的称重记录 ID（null 表示当前周期内未创建记录）
    /// </summary>
    public long? GetLastCreatedWeighingRecordId() => _lastCreatedWeighingRecordIdSubject.Value;

    /// <summary>
    ///     重置称重周期（清除记录 ID 标记，为新的称重周期做准备）
    /// </summary>
    public void ResetCycle()
    {
        _lastCreatedWeighingRecordIdSubject.OnNext(null);
        _logger.LogDebug("Weighing cycle reset: record ID cleared");
    }

    /// <summary>
    ///     状态通知（更新状态并通过 ILocalEventBus 广播）
    /// </summary>
    public void UpdateStatusAndNotify(AttendedWeighingStatus newStatus)
    {
        UpdateStatus(newStatus);
        _ = _localEventBus.PublishAsync(new StatusChangedEventData(newStatus));
    }

    public void Dispose()
    {
        try { _statusSubject?.OnCompleted(); } catch (InvalidOperationException) { }
        _statusSubject?.Dispose();

        try { _deliveryTypeSubject?.OnCompleted(); } catch (InvalidOperationException) { }
        _deliveryTypeSubject?.Dispose();

        try { _lastCreatedWeighingRecordIdSubject?.OnCompleted(); } catch (InvalidOperationException) { }
        _lastCreatedWeighingRecordIdSubject?.Dispose();
    }
}
