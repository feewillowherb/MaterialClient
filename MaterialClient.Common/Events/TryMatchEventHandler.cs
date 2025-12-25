using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;

namespace MaterialClient.Common.Events;

/// <summary>
/// 尝试匹配称重记录的事件处理器
/// </summary>
[AutoConstructor]
public partial class TryMatchEventHandler : ILocalEventHandler<TryMatchEvent>, ITransientDependency
{
    private readonly IWeighingMatchingService _weighingMatchingService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly ILogger<TryMatchEventHandler>? _logger;


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

                // 查询匹配成功后的 WaybillId
                var weighingRecord = await _weighingRecordRepository.FindAsync(eventData.WeighingRecordId);
                if (weighingRecord != null && weighingRecord.WaybillId.HasValue)
                {
                    // 通过 ReactiveUI MessageBus 发送匹配成功消息
                    var message = new MatchSucceededMessage(weighingRecord.WaybillId.Value, eventData.WeighingRecordId);
                    MessageBus.Current.SendMessage(message);
                    
                    _logger?.LogInformation(
                        "TryMatchEventHandler: Sent MatchSucceededMessage via MessageBus for WaybillId {WaybillId}, WeighingRecordId {RecordId}",
                        weighingRecord.WaybillId.Value, eventData.WeighingRecordId);
                }
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