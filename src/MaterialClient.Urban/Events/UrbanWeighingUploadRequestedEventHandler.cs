using MaterialClient.Common.Events;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Events;

/// <summary>
///     审批后对单条称重记录立即上云；失败时保留 Pending 状态供轮询 Worker 兜底重试。
/// </summary>
[AutoConstructor]
public partial class UrbanWeighingUploadRequestedEventHandler
    : ILocalEventHandler<UrbanWeighingUploadRequestedEventData>, ITransientDependency
{
    private readonly IUrbanServerUploadService _uploadService;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<UrbanWeighingUploadRequestedEventHandler> _logger;

    public async Task HandleEventAsync(UrbanWeighingUploadRequestedEventData eventData)
    {
        _logger.LogInformation(
            "UrbanWeighingUploadRequested: starting immediate upload for record {RecordId}",
            eventData.WeighingRecordId);

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await _uploadService.SubmitRecordAsync(eventData.WeighingRecordId);
            await uow.CompleteAsync();

            await _localEventBus.PublishAsync(new UploadCompletedEventData(eventData.WeighingRecordId));

            _logger.LogInformation(
                "UrbanWeighingUploadRequested: upload completed for record {RecordId}",
                eventData.WeighingRecordId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "UrbanWeighingUploadRequested: immediate upload failed for record {RecordId} (polling will retry)",
                eventData.WeighingRecordId);
        }
    }
}
