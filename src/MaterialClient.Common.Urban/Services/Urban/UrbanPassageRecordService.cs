using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services.Urban;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Urban.Services.Urban;

public class UrbanPassageRecordService : DomainService, IUrbanPassageRecordService, ITransientDependency
{
    private readonly IRepository<UrbanPassageRecord, Guid> _passageRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUrbanWeighingExtensionService _weighingExtensionService;

    public UrbanPassageRecordService(
        IRepository<UrbanPassageRecord, Guid> passageRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IUrbanWeighingExtensionService weighingExtensionService)
    {
        _passageRepository = passageRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _weighingExtensionService = weighingExtensionService;
    }

    [UnitOfWork]
    public virtual async Task<UrbanPassageRecord> CreateFromLprAsync(UrbanLprCaptureContext context)
    {
        var entity = UrbanPassageRecord.FromLprCapture(context);
        await _passageRepository.InsertAsync(entity, autoSave: true);
        return entity;
    }

    public virtual async Task<PagedResultDto<UrbanAttendedListRow>> GetPagedListAsync(GetUrbanWeighingListInput input)
    {
        var pageIndex = input.PageIndex < 1 ? 1 : input.PageIndex;
        var pageSize = input.PageSize < 1 ? 20 : input.PageSize;
        var tab = input.TabFilter;

        if (tab is "正常" or "异常")
        {
            var weighingOnly = await _weighingExtensionService.GetPagedListItemsAsync(input);
            var weighingRows = weighingOnly.Items.Select(UrbanAttendedListRow.FromWeighing).ToList();
            return new PagedResultDto<UrbanAttendedListRow>(weighingOnly.TotalCount, weighingRows);
        }

        PassageSource? sourceFilter = tab switch
        {
            "卡口" => PassageSource.Checkpoint,
            "成品" => PassageSource.FinishedProduct,
            _ => null
        };

        var passageQuery = await _passageRepository.GetQueryableAsync();
        if (sourceFilter.HasValue)
            passageQuery = passageQuery.Where(x => x.PassageSource == sourceFilter.Value);

        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            passageQuery = passageQuery.Where(x => x.PlateNumber.Contains(input.SearchText));
        }

        if (input.StartTime.HasValue)
            passageQuery = passageQuery.Where(x => x.CapturedAt >= input.StartTime.Value);

        if (input.EndTime.HasValue)
            passageQuery = passageQuery.Where(x => x.CapturedAt <= input.EndTime.Value);

        if (sourceFilter.HasValue)
        {
            var total = await passageQuery.CountAsync();
            var page = await passageQuery
                .OrderByDescending(x => x.CapturedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var rows = await ProjectPassageRowsAsync(page);
            return new PagedResultDto<UrbanAttendedListRow>(total, rows);
        }

        var weighingAll = await _weighingExtensionService.GetPagedListItemsAsync(new GetUrbanWeighingListInput
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            TabFilter = null,
            SearchText = input.SearchText,
            StartTime = input.StartTime,
            EndTime = input.EndTime
        });

        var passages = await passageQuery.ToListAsync();
        var mixed = weighingAll.Items
            .Select(UrbanAttendedListRow.FromWeighing)
            .Concat(await ProjectPassageRowsAsync(passages))
            .OrderByDescending(x => x.SortTime)
            .ToList();

        var mixedTotal = mixed.Count;
        var mixedPage = mixed.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResultDto<UrbanAttendedListRow>(mixedTotal, mixedPage);
    }

    private async Task<List<UrbanAttendedListRow>> ProjectPassageRowsAsync(List<UrbanPassageRecord> records)
    {
        var ids = records
            .Select(r => r.LargeImageAttachmentId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var pathById = new Dictionary<int, string>();
        if (ids.Count > 0)
        {
            var files = await _attachmentFileRepository.GetListAsync(f => ids.Contains(f.Id));
            foreach (var file in files)
            {
                if (!string.IsNullOrWhiteSpace(file.LocalPath))
                    pathById[file.Id] = file.LocalPath;
            }
        }

        return records.Select(r =>
        {
            string? path = null;
            if (r.LargeImageAttachmentId is int aid)
                pathById.TryGetValue(aid, out path);

            return UrbanAttendedListRow.FromPassage(
                r.Id,
                r.PassageSource,
                r.PlateNumber,
                r.PlateColor,
                r.VehicleType,
                r.UrbanInOutType,
                r.UrbanSiteType,
                r.CapturedAt,
                r.LargeImageAttachmentId,
                path);
        }).ToList();
    }
}
