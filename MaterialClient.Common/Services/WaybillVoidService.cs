using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     Waybill void service interface - handles selective voiding of waybills and their weighing records
/// </summary>
public interface IWaybillVoidService
{
    /// <summary>
    ///     Void a waybill with the specified scope
    /// </summary>
    /// <param name="waybillId">The waybill ID to void</param>
    /// <param name="scope">The scope of voiding (JoinOnly, OutOnly, or Both)</param>
    /// <param name="reason">The reason for voiding</param>
    Task VoidWaybillAsync(long waybillId, WaybillVoidScope scope, string reason);
}

/// <summary>
///     Waybill void service implementation - orchestrates selective voiding of waybills
/// </summary>
[AutoConstructor]
public partial class WaybillVoidService : IWaybillVoidService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly ILogger<WaybillVoidService> _logger;

    [UnitOfWork]
    public async Task VoidWaybillAsync(long waybillId, WaybillVoidScope scope, string reason)
    {
        var waybill = await _waybillRepository.GetAsync(waybillId);

        var relatedRecords = await _weighingRecordRepository
            .GetQueryableAsync();
        var records = await relatedRecords
            .Where(r => r.WaybillId == waybillId && !r.IsDeleted)
            .ToListAsync();

        var joinRecord = records.FirstOrDefault(r => r.MatchedType == WeighingRecordMatchType.Join);
        var outRecord = records.FirstOrDefault(r => r.MatchedType == WeighingRecordMatchType.Out);

        switch (scope)
        {
            case WaybillVoidScope.JoinOnly:
                if (joinRecord is not null)
                {
                    joinRecord.IsDeleted = true;
                    await _weighingRecordRepository.UpdateAsync(joinRecord);
                }

                if (outRecord is not null)
                {
                    outRecord.Unmatch();
                    await _weighingRecordRepository.UpdateAsync(outRecord);
                }

                break;

            case WaybillVoidScope.OutOnly:
                if (outRecord is not null)
                {
                    outRecord.IsDeleted = true;
                    await _weighingRecordRepository.UpdateAsync(outRecord);
                }

                if (joinRecord is not null)
                {
                    joinRecord.Unmatch();
                    await _weighingRecordRepository.UpdateAsync(joinRecord);
                }

                break;

            case WaybillVoidScope.Both:
                if (joinRecord is not null)
                {
                    joinRecord.IsDeleted = true;
                    await _weighingRecordRepository.UpdateAsync(joinRecord);
                }

                if (outRecord is not null)
                {
                    outRecord.IsDeleted = true;
                    await _weighingRecordRepository.UpdateAsync(outRecord);
                }

                break;
        }

        waybill.AbortWaybill(reason);
        waybill.SetPendingSync();
        await _waybillRepository.UpdateAsync(waybill);

        _logger.LogInformation(
            "Waybill {WaybillId} voided with scope {Scope}, reason: {Reason}",
            waybillId, scope, reason);
    }
}
