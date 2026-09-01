using MaterialClient.Common.Events;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Events;

/// <summary>
///     Attempts immediate cloud upload when a passage row is created locally; polling retries on failure.
/// </summary>
[AutoConstructor]
public partial class UrbanPassageRecordCreatedEventHandler
    : ILocalEventHandler<UrbanPassageRecordCreatedEventData>, ITransientDependency
{
    private readonly IUrbanPassageUploadService _uploadService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<UrbanPassageRecordCreatedEventHandler> _logger;

    public async Task HandleEventAsync(UrbanPassageRecordCreatedEventData eventData)
    {
        _logger.LogInformation(
            "UrbanPassageRecordCreated: starting immediate upload for passage {PassageRecordId}",
            eventData.PassageRecordId);

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            var success = await _uploadService.SubmitPassageRecordAsync(eventData.PassageRecordId);
            await uow.CompleteAsync();

            if (success)
            {
                _logger.LogInformation(
                    "UrbanPassageRecordCreated: upload completed for passage {PassageRecordId}",
                    eventData.PassageRecordId);
            }
            else
            {
                _logger.LogWarning(
                    "UrbanPassageRecordCreated: immediate upload failed for passage {PassageRecordId} (polling will retry)",
                    eventData.PassageRecordId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "UrbanPassageRecordCreated: immediate upload failed for passage {PassageRecordId} (polling will retry)",
                eventData.PassageRecordId);
        }
    }
}
