using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using MaterialClient.Common.Services;

namespace MaterialClient.Common.Events;

/// <summary>
/// 尝试匹配称重记录的事件处理器
/// </summary>
public class TryMatchEventHandler : ILocalEventHandler<TryMatchEvent>, ITransientDependency
{
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly ILogger<TryMatchEventHandler>? _logger;

    public TryMatchEventHandler(
        IWeighingMatchingService weighingMatchingService,
        ILogger<TryMatchEventHandler>? logger = null)
    {
        _weighingMatchingService = weighingMatchingService;
        _logger = logger;
    }

    public async Task HandleEventAsync(TryMatchEvent eventData)
    {
        _logger?.LogInformation(
            "TryMatchEventHandler: Received TryMatchEvent for WeighingRecordId {RecordId}",
            eventData.WeighingRecordId);

        try
        {
            var matched = await _weighingMatchingService.AutoMatchAsync(eventData.WeighingRecordId);

            if (matched)
            {
                _logger?.LogInformation(
                    "TryMatchEventHandler: Successfully matched WeighingRecordId {RecordId}",
                    eventData.WeighingRecordId);
            }
            else
            {
                _logger?.LogInformation(
                    "TryMatchEventHandler: No match found for WeighingRecordId {RecordId}",
                    eventData.WeighingRecordId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "TryMatchEventHandler: Error while processing TryMatchEvent for WeighingRecordId {RecordId}",
                eventData.WeighingRecordId);
        }
    }
}

