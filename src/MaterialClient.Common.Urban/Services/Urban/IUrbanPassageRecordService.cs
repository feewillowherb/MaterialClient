using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Urban;

public interface IUrbanPassageRecordService : ITransientDependency
{
    Task<UrbanPassageRecord> CreateFromLprAsync(UrbanLprCaptureContext context);

    Task<PagedResultDto<UrbanAttendedListRow>> GetPagedListAsync(GetUrbanWeighingListInput input);

    Task<List<UrbanPassageRecord>> GetPendingForUploadAsync(int maxCount = 100);

    Task MarkSyncedAsync(Guid passageRecordId);

    Task MarkUploadFailedAsync(Guid passageRecordId);
}
