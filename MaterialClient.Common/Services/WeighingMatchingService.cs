using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IWeighingMatchingService
{
    Task<bool> TryMatchWeighingRecordAsync(WeighingRecord record);
    Task UpdateWaybillAsync(UpdateWaybillInput input);
    Task UpdateWeighingRecordAsync(UpdateWeighingRecordInput input);

    /// <summary>
    /// 更新列表项（根据类型自动判断更新 WeighingRecord 或 Waybill）
    /// </summary>
    /// <param name="input">更新参数</param>
    Task UpdateListItemAsync(UpdateListItemInput input);

    /// <summary>
    /// 获取可匹配的候选记录列表
    /// </summary>
    /// <param name="record">当前称重记录</param>
    /// <param name="deliveryType">收发料类型</param>
    /// <returns>可匹配的候选记录列表</returns>
    Task<List<WeighingRecord>> GetCandidateRecordsAsync(WeighingRecord record, DeliveryType deliveryType);

    /// <summary>
    /// 手动匹配两条称重记录并创建运单
    /// </summary>
    /// <param name="currentRecord">当前称重记录</param>
    /// <param name="matchedRecord">匹配的称重记录</param>
    /// <param name="deliveryType">收发料类型</param>
    Task ManualMatchAsync(WeighingRecord currentRecord, WeighingRecord matchedRecord, DeliveryType deliveryType);

    /// <summary>
    /// 获取称重列表项（分页）
    /// </summary>
    /// <param name="input">分页和过滤参数</param>
    /// <returns>分页结果</returns>
    Task<PagedResultDto<WeighingListItemDto>> GetListItemsAsync(GetWeighingListItemsInput input);

    /// <summary>
    /// 自动匹配称重记录（同时尝试收料和发料两种类型）
    /// </summary>
    /// <param name="weighingRecordId">称重记录ID</param>
    /// <returns>是否匹配成功</returns>
    Task<bool> AutoMatchAsync(long weighingRecordId);

    /// <summary>
    /// 完成运单（将 OrderType 设置为 Completed）
    /// </summary>
    /// <param name="waybillId">运单ID</param>
    Task CompleteOrderAsync(long waybillId);

    /// <summary>
    /// 尝试计算物料重量（如果有必要的参数）
    /// </summary>
    /// <param name="waybill">运单实体</param>
    /// <param name="materialId">物料ID（可选，为null时使用waybill中的值）</param>
    /// <param name="materialUnitId">物料单位ID（可选，为null时使用waybill中的值）</param>
    /// <param name="waybillQuantity">运单数量（可选，为null时使用waybill中的值）</param>
    Task TryCalculateMaterialAsync(Waybill waybill, int? materialId = null, int? materialUnitId = null,
        decimal? waybillQuantity = null);

    /// <summary>
    /// 推送未同步的运单到平台
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task PushWaybillAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for matching weighing records and creating waybills
/// </summary>
[AutoConstructor]
public partial class WeighingMatchingService : DomainService, IWeighingMatchingService
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<WaybillAttachment, int> _waybillAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<LicenseInfo, Guid> _licenseInfoRepository;
    private readonly IMaterialPlatformApi _materialPlatformApi;
    private readonly ILogger<WeighingMatchingService>? _logger;


    private const int MaxIntervalMinutes = 300;
    private const decimal MinTon = 1m;


    [UnitOfWork]
    public async Task<bool> TryMatchWeighingRecordAsync(WeighingRecord record)
    {
        if (!record.IsValidChinesePlateNumber())
        {
            _logger?.LogInformation(
                "WeighingMatchingService: Record {RecordId} has invalid plate number '{PlateNumber}', skipping matching",
                record.Id, record.PlateNumber);
            return false;
        }

        var query = await _weighingRecordRepository.GetQueryableAsync();

        var unmatchedRecords = await query
            .Where(r => r.PlateNumber == record.PlateNumber && r.Id != record.Id)
            .Where(r => r.MatchedId == null)
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();
        if (unmatchedRecords.Count == 0)
        {
            _logger?.LogInformation(
                "WeighingMatchingService: No unmatched records found for plate '{PlateNumber}' to match with record {RecordId}",
                record.PlateNumber, record.Id);
            return false;
        }

        var validTime = record.CreationTime.AddMinutes(-MaxIntervalMinutes);
        var timeoutOrders = unmatchedRecords
            .Where(r => r.CreationTime < validTime)
            .ToList();
        if (timeoutOrders.Any())
        {
            _logger?.LogInformation(
                "WeighingMatchingService: Found {Count} timeout unmatched records for plate '{PlateNumber}' to match with record {RecordId}",
                timeoutOrders.Count, record.PlateNumber, record.Id);
        }

        if (record.DeliveryType == null || record.DeliveryType == DeliveryType.Receiving)
        {
            var candidateRecords = unmatchedRecords
                .Where(r => r.CreationTime >= validTime)
                .Where(r => r.TotalWeight > record.TotalWeight)
                .Where(r => (r.TotalWeight - record.TotalWeight) > MinTon)
                .OrderByDescending(r => r.CreationTime)
                .ToList();
            if (candidateRecords.Count == 0)
            {
                _logger?.LogInformation(
                    "WeighingMatchingService: No candidate records found for matching with record {RecordId}",
                    record.Id);
                return false;
            }

            var matchedRecord = candidateRecords
                .OrderBy(r => r.CreationTime - record.CreationTime)
                .First();

            await CreateWaybillAsync(record, matchedRecord, DeliveryType.Receiving);
            return true;
        }

        if (record.DeliveryType == DeliveryType.Sending)
        {
            var candidateRecords = unmatchedRecords
                .Where(r => r.CreationTime >= validTime)
                .Where(r => r.TotalWeight < record.TotalWeight)
                .Where(r => (record.TotalWeight - r.TotalWeight) > MinTon)
                .OrderByDescending(r => r.CreationTime)
                .ToList();
            if (candidateRecords.Count == 0)
            {
                _logger?.LogInformation(
                    "WeighingMatchingService: No candidate records found for matching with record {RecordId}",
                    record.Id);
                return false;
            }

            var matchedRecord = candidateRecords
                .OrderBy(r => r.CreationTime - record.CreationTime)
                .First();

            await CreateWaybillAsync(matchedRecord, record, DeliveryType.Sending);
            return true;
        }

        _logger?.LogWarning(
            "WeighingMatchingService: Record {RecordId} has unknown DeliveryType '{DeliveryType}', skipping matching",
            record.Id, record.DeliveryType);
        return false;
    }

    [UnitOfWork]
    public async Task UpdateWaybillAsync(UpdateWaybillInput input)
    {
        var waybill = await _waybillRepository.FindAsync(input.WaybillId);

        if (waybill == null)
        {
            _logger?.LogError("Waybill with ID {WaybillId} not found.", input.WaybillId);
            return;
        }

        // 更新基本字段
        if (input.PlateNumber != null) waybill.PlateNumber = input.PlateNumber;
        if (input.ProviderId.HasValue) waybill.ProviderId = input.ProviderId.Value;

        // 计算物料信息
        await TryCalculateMaterialAsync(waybill,
            input.MaterialId ?? waybill.MaterialId,
            input.MaterialUnitId ?? waybill.MaterialUnitId,
            input.WaybillQuantity ?? waybill.OrderPlanOnPcs);

        waybill.SetPendingSync();

        await _waybillRepository.UpdateAsync(waybill);
    }

    [UnitOfWork]
    public async Task UpdateWeighingRecordAsync(UpdateWeighingRecordInput input)
    {
        var record = await _weighingRecordRepository.FindAsync(input.WeighingRecordId);

        if (record == null)
        {
            _logger?.LogError("WeighingRecord with ID {RecordId} not found.", input.WeighingRecordId);
            return;
        }

        // 更新基本字段
        if (input.PlateNumber != null) record.PlateNumber = input.PlateNumber;
        if (input.ProviderId.HasValue) record.ProviderId = input.ProviderId;
        if (input.DeliveryType.HasValue) record.DeliveryType = input.DeliveryType;

        // 更新物料信息（在第一个 Material 中）
        if (input.MaterialId.HasValue || input.MaterialUnitId.HasValue || input.WaybillQuantity.HasValue)
        {
            var materials = record.Materials;
            var firstMaterial = materials.FirstOrDefault();
            if (firstMaterial != null)
            {
                if (input.MaterialId.HasValue) firstMaterial.MaterialId = input.MaterialId;
                if (input.MaterialUnitId.HasValue) firstMaterial.MaterialUnitId = input.MaterialUnitId;
                if (input.WaybillQuantity.HasValue) firstMaterial.WaybillQuantity = input.WaybillQuantity;
                // 重新设置以触发 JSON 序列化
                record.Materials = materials;
            }
            else
            {
                // 创建新的物料
                record.AddMaterial(new WeighingRecordMaterial(
                    0,
                    input.MaterialId,
                    input.MaterialUnitId,
                    input.WaybillQuantity));
            }
        }

        await _weighingRecordRepository.UpdateAsync(record);
    }

    [UnitOfWork]
    public async Task UpdateListItemAsync(UpdateListItemInput input)
    {
        if (input.ItemType == WeighingListItemType.WeighingRecord)
        {
            await UpdateWeighingRecordAsync(new UpdateWeighingRecordInput(
                input.Id,
                input.PlateNumber,
                input.ProviderId,
                input.MaterialId,
                input.MaterialUnitId,
                input.WaybillQuantity,
                input.DeliveryType
            ));
        }
        else if (input.ItemType == WeighingListItemType.Waybill)
        {
            await UpdateWaybillAsync(new UpdateWaybillInput(
                input.Id,
                input.PlateNumber,
                input.ProviderId,
                input.MaterialId,
                input.MaterialUnitId,
                input.WaybillQuantity
            ));
        }
    }

    [UnitOfWork]
    private async Task CreateWaybillAsync(WeighingRecord joinRecord, WeighingRecord outRecord,
        DeliveryType deliveryType)
    {
        var todayCount = await _waybillRepository.CountAsync(w =>
            w.AddDate!.Value.Date == DateTime.Today);

        var orderNo = Waybill.GenerateOrderNo(deliveryType, joinRecord.CreationTime, todayCount);
        var orderId = Waybill.GenerateOrderId();
        var waybill = new Waybill(orderId, orderNo)
        {
            ProviderId = joinRecord.ProviderId ?? outRecord.ProviderId,
            PlateNumber = joinRecord.PlateNumber ?? outRecord.PlateNumber,
            JoinTime = joinRecord.CreationTime,
            OutTime = outRecord.CreationTime,
            DeliveryType = deliveryType,
            OrderSource = OrderSource.MannedStation,
            OrderType = OrderTypeEnum.FirstWeight
        };
        waybill.SetWeight(joinRecord, outRecord, deliveryType);

        // 先插入 Waybill 获取 Id
        await _waybillRepository.InsertAsync(waybill, true);
        joinRecord.MatchAsJoin(outRecord.Id, waybill.Id);
        outRecord.MatchAsOut(joinRecord.Id, waybill.Id);
        await _weighingRecordRepository.UpdateAsync(joinRecord);
        await _weighingRecordRepository.UpdateAsync(outRecord);

        // 复制 WeighingRecord 的附件到 WaybillAttachment
        await CopyAttachmentsToWaybillAsync(waybill.Id, joinRecord.Id, outRecord.Id);

        // 计算物料信息（从 Materials 集合中获取）
        var joinMaterial = joinRecord.Materials?.FirstOrDefault();
        var outMaterial = outRecord.Materials?.FirstOrDefault();
        var materialId = joinMaterial?.MaterialId ?? outMaterial?.MaterialId;
        var materialUnitId = joinMaterial?.MaterialUnitId ?? outMaterial?.MaterialUnitId;
        var waybillQuantity = joinMaterial?.WaybillQuantity ?? outMaterial?.WaybillQuantity;
        await TryCalculateMaterialAsync(waybill, materialId, materialUnitId, waybillQuantity);
    }

    /// <summary>
    /// 复制 WeighingRecord 的附件到 WaybillAttachment，并设置 AttachType
    /// </summary>
    /// <param name="waybillId">运单ID</param>
    /// <param name="joinRecordId">进场称重记录ID</param>
    /// <param name="outRecordId">出场称重记录ID</param>
    private async Task CopyAttachmentsToWaybillAsync(long waybillId, long joinRecordId, long outRecordId)
    {
        var attachmentQuery = await _weighingRecordAttachmentRepository.GetQueryableAsync();

        // 处理 joinRecord 的附件（设为 EntryPhoto）
        var joinAttachments = await attachmentQuery
            .Where(ra => ra.WeighingRecordId == joinRecordId)
            .ToListAsync();

        foreach (var attachment in joinAttachments)
        {
            var attachmentFile = await _attachmentFileRepository.GetAsync(attachment.AttachmentFileId);
            attachmentFile.AttachType = AttachType.EntryPhoto;
            await _attachmentFileRepository.UpdateAsync(attachmentFile);

            await _waybillAttachmentRepository.InsertAsync(
                new WaybillAttachment(waybillId, attachment.AttachmentFileId));
        }

        // 处理 outRecord 的附件（设为 ExitPhoto）
        var outAttachments = await attachmentQuery
            .Where(ra => ra.WeighingRecordId == outRecordId)
            .ToListAsync();

        foreach (var attachment in outAttachments)
        {
            // 避免重复插入（如果同一个附件同时关联了 joinRecord 和 outRecord）
            var alreadyAdded = joinAttachments.Any(ja => ja.AttachmentFileId == attachment.AttachmentFileId);

            var attachmentFile = await _attachmentFileRepository.GetAsync(attachment.AttachmentFileId);
            attachmentFile.AttachType = AttachType.ExitPhoto;
            await _attachmentFileRepository.UpdateAsync(attachmentFile);

            if (!alreadyAdded)
            {
                await _waybillAttachmentRepository.InsertAsync(
                    new WaybillAttachment(waybillId, attachment.AttachmentFileId));
            }
        }
    }

    /// <summary>
    /// 获取可匹配的候选记录列表（双向匹配，record 可作为 join 或 out）
    /// </summary>
    [UnitOfWork]
    public async Task<List<WeighingRecord>> GetCandidateRecordsAsync(WeighingRecord record, DeliveryType deliveryType)
    {
        if (string.IsNullOrWhiteSpace(record.PlateNumber))
        {
            _logger?.LogWarning("GetCandidateRecordsAsync: Record {RecordId} has no plate number", record.Id);
            return new List<WeighingRecord>();
        }

        var query = await _weighingRecordRepository.GetQueryableAsync();

        var unmatchedRecords = await query
            .Where(r => r.PlateNumber == record.PlateNumber && r.Id != record.Id)
            .Where(r => r.MatchedId == null)
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();

        if (unmatchedRecords.Count == 0)
        {
            _logger?.LogInformation(
                "GetCandidateRecordsAsync: No unmatched records found for plate '{PlateNumber}'",
                record.PlateNumber);
            return new List<WeighingRecord>();
        }

        // 使用 TryMatch 过滤可匹配的候选记录
        return unmatchedRecords
            .Where(r => WeighingRecord.TryMatch(record, r, deliveryType, MaxIntervalMinutes, MinTon).IsMatch)
            .OrderByDescending(r => r.CreationTime)
            .ToList();
    }

    /// <summary>
    /// 手动匹配两条称重记录并创建运单
    /// </summary>
    [UnitOfWork]
    public async Task ManualMatchAsync(WeighingRecord currentRecord, WeighingRecord matchedRecord,
        DeliveryType deliveryType)
    {
        // 使用领域方法自动判断 join/out
        var matchResult = WeighingRecord.TryMatch(currentRecord, matchedRecord, deliveryType);

        if (!matchResult.IsMatch || matchResult.JoinRecord == null || matchResult.OutRecord == null)
            throw new BusinessException("无法匹配这两条记录");

        await CreateWaybillAsync(matchResult.JoinRecord, matchResult.OutRecord, deliveryType);
    }

    /// <summary>
    /// 获取称重列表项（分页）
    /// </summary>
    [UnitOfWork]
    public async Task<PagedResultDto<WeighingListItemDto>> GetListItemsAsync(GetWeighingListItemsInput input)
    {
        var result = new List<WeighingListItemDto>();
        var isCompleted = input.IsCompleted;

        if (isCompleted == null || isCompleted == false)
        {
            // 获取未完成的 WeighingRecord（MatchedId == null）
            var weighingRecordQuery = await _weighingRecordRepository.GetQueryableAsync();
            var unmatchedRecords = await weighingRecordQuery
                .Where(r => r.MatchedId == null)
                .ToListAsync();

            foreach (var record in unmatchedRecords)
            {
                result.Add(WeighingListItemDto.FromWeighingRecord(record));
            }

            // 获取未完成的 Waybill（OrderType == FirstWeight）
            var waybillQuery = await _waybillRepository.GetQueryableAsync();
            var firstWeightWaybills = await waybillQuery
                .Where(w => w.OrderType == OrderTypeEnum.FirstWeight)
                .ToListAsync();

            foreach (var waybill in firstWeightWaybills)
            {
                result.Add(WeighingListItemDto.FromWaybill(waybill));
            }
        }

        if (isCompleted == null || isCompleted == true)
        {
            // 获取已完成的 Waybill（OrderType == Completed）
            var waybillQuery = await _waybillRepository.GetQueryableAsync();
            var completedWaybills = await waybillQuery
                .Where(w => w.OrderType == OrderTypeEnum.Completed)
                .ToListAsync();

            foreach (var waybill in completedWaybills)
            {
                result.Add(WeighingListItemDto.FromWaybill(waybill));
            }
        }

        // 获取总数
        var totalCount = result.Count;

        // 按 JoinTime 降序排列，然后分页
        var items = result
            .OrderByDescending(item => item.JoinTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        // 填充预计算字段
        await PopulateComputedFieldsAsync(items);

        return new PagedResultDto<WeighingListItemDto>(totalCount, items);
    }

    /// <summary>
    /// 填充列表项的预计算字段
    /// </summary>
    private async Task PopulateComputedFieldsAsync(List<WeighingListItemDto> items)
    {
        // 收集所有需要查询的 ID
        var providerIds = items.Where(i => i.ProviderId.HasValue).Select(i => i.ProviderId!.Value).Distinct().ToList();
        var materialIds = items.Where(i => i.MaterialId.HasValue).Select(i => i.MaterialId!.Value).Distinct().ToList();
        var materialUnitIds = items.Where(i => i.MaterialUnitId.HasValue).Select(i => i.MaterialUnitId!.Value)
            .Distinct().ToList();

        // 批量查询供应商
        var providers = providerIds.Count > 0
            ? (await _providerRepository.GetListAsync(p => providerIds.Contains(p.Id))).ToDictionary(p => p.Id)
            : new Dictionary<int, Provider>();

        // 批量查询物料
        var materials = materialIds.Count > 0
            ? (await _materialRepository.GetListAsync(m => materialIds.Contains(m.Id))).ToDictionary(m => m.Id)
            : new Dictionary<int, Material>();

        // 批量查询物料单位
        var materialUnits = materialUnitIds.Count > 0
            ? (await _materialUnitRepository.GetListAsync(u => materialUnitIds.Contains(u.Id))).ToDictionary(u => u.Id)
            : new Dictionary<int, MaterialUnit>();

        // 填充预计算字段
        foreach (var item in items)
        {
            // 供应商名称
            if (item.ProviderId.HasValue && providers.TryGetValue(item.ProviderId.Value, out var provider))
            {
                item.ProviderName = provider.ProviderName;
            }

            // 物料信息
            if (item.MaterialId.HasValue && materials.TryGetValue(item.MaterialId.Value, out var material))
            {
                string? unitInfo = null;
                if (item.MaterialUnitId.HasValue &&
                    materialUnits.TryGetValue(item.MaterialUnitId.Value, out var materialUnit))
                {
                    unitInfo = $"{materialUnit.Rate}/{materialUnit.UnitName}";
                }

                item.MaterialInfo = unitInfo != null ? $"{unitInfo} {material.Name}" : material.Name;
            }

            // 仅对 Waybill 类型填充进出场重量和偏差信息
            if (item.ItemType == WeighingListItemType.Waybill)
            {
                // 计算进出场重量
                if (item.DeliveryType == Entities.Enums.DeliveryType.Sending)
                {
                    item.JoinWeight = item.TruckWeight;
                    item.OutWeight = item.Weight;
                }
                else if (item.DeliveryType == Entities.Enums.DeliveryType.Receiving)
                {
                    item.JoinWeight = item.Weight;
                    item.OutWeight = item.TruckWeight;
                }
            }
        }
    }

    /// <summary>
    /// 自动匹配称重记录（同时尝试收料和发料两种类型）
    /// </summary>
    [UnitOfWork]
    public async Task<bool> AutoMatchAsync(long weighingRecordId)
    {
        var record = await _weighingRecordRepository.FindAsync(weighingRecordId);
        if (record == null)
        {
            _logger?.LogWarning("AutoMatchAsync: Record {RecordId} not found", weighingRecordId);
            return false;
        }

        // 已经匹配过的记录不再处理
        if (record.MatchedId != null)
        {
            _logger?.LogInformation("AutoMatchAsync: Record {RecordId} is already matched", weighingRecordId);
            return false;
        }

        // 验证车牌号
        if (!record.IsValidChinesePlateNumber())
        {
            _logger?.LogInformation(
                "AutoMatchAsync: Record {RecordId} has invalid plate number '{PlateNumber}', skipping matching",
                record.Id, record.PlateNumber);
            return false;
        }

        // 如果记录有明确的 DeliveryType，只尝试该类型
        if (record.DeliveryType != null)
        {
            return await TryMatchWithDeliveryTypeAsync(record, record.DeliveryType.Value);
        }

        // 没有明确 DeliveryType 时，同时尝试收料和发料
        // 优先尝试收料（Receiving）
        if (await TryMatchWithDeliveryTypeAsync(record, DeliveryType.Receiving))
        {
            return true;
        }

        // 再尝试发料（Sending）
        return await TryMatchWithDeliveryTypeAsync(record, DeliveryType.Sending);
    }

    /// <summary>
    /// 尝试使用指定的 DeliveryType 匹配记录
    /// </summary>
    private async Task<bool> TryMatchWithDeliveryTypeAsync(WeighingRecord record, DeliveryType deliveryType)
    {
        var candidates = await GetCandidateRecordsAsync(record, deliveryType);
        if (candidates.Count == 0)
        {
            _logger?.LogInformation(
                "AutoMatchAsync: No candidate records found for record {RecordId} with DeliveryType {DeliveryType}",
                record.Id, deliveryType);
            return false;
        }

        // 选择时间最接近的候选记录
        var matchedRecord = candidates
            .OrderBy(r => Math.Abs((r.CreationTime - record.CreationTime).TotalMinutes))
            .First();

        // 使用领域方法自动判断 join/out
        var matchResult = WeighingRecord.TryMatch(record, matchedRecord, deliveryType, MaxIntervalMinutes, MinTon);

        if (!matchResult.IsMatch || matchResult.JoinRecord == null || matchResult.OutRecord == null)
        {
            _logger?.LogWarning(
                "AutoMatchAsync: TryMatch failed for record {RecordId} with candidate {CandidateId}",
                record.Id, matchedRecord.Id);
            return false;
        }

        await CreateWaybillAsync(matchResult.JoinRecord, matchResult.OutRecord, deliveryType);

        _logger?.LogInformation(
            "AutoMatchAsync: Successfully matched record {RecordId} with {MatchedId}, DeliveryType: {DeliveryType}",
            record.Id, matchedRecord.Id, deliveryType);
        return true;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task CompleteOrderAsync(long waybillId)
    {
        var waybill = await _waybillRepository.GetAsync(waybillId);

        // 先执行物料计算
        await TryCalculateMaterialAsync(waybill);

        waybill.OrderTypeCompleted();
        await _waybillRepository.UpdateAsync(waybill);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task TryCalculateMaterialAsync(Waybill waybill, int? materialId = null, int? materialUnitId = null,
        decimal? waybillQuantity = null)
    {
        // 使用传入的值，或从 waybill 中获取
        materialId ??= waybill.MaterialId;
        materialUnitId ??= waybill.MaterialUnitId;
        waybillQuantity ??= waybill.OrderPlanOnPcs;

        if (!materialUnitId.HasValue || !waybillQuantity.HasValue || !materialId.HasValue)
            return;

        var material = await _materialRepository.GetAsync(materialId.Value);

        var materialUnit = await _materialUnitRepository.GetAsync(materialUnitId.Value);

        // 更新 Waybill 的物料信息
        waybill.MaterialId = materialId;
        waybill.MaterialUnitId = materialUnitId;
        waybill.OrderPlanOnPcs = waybillQuantity;
        waybill.MaterialUnitRate = materialUnit.Rate;
        waybill.CalculateMaterialWeight(material.LowerLimit, material.UpperLimit);

        // 查找或创建 WaybillMaterial
        var existingMaterial = await _waybillMaterialRepository.FirstOrDefaultAsync(wm => wm.WaybillId == waybill.Id);

        if (existingMaterial != null)
        {
            // 更新现有记录
            existingMaterial.MaterialId = material.Id;
            existingMaterial.MaterialName = material.Name;
            existingMaterial.Specifications = material.Specifications;
            existingMaterial.MaterialUnitId = materialUnitId;
            existingMaterial.GoodsPlanOnPcs = waybillQuantity.Value;
            existingMaterial.UpdateOffsetFromWaybill(waybill);
            await _waybillMaterialRepository.UpdateAsync(existingMaterial);
        }
        else
        {
            // 创建新记录
            var waybillMaterial = new WaybillMaterial(waybill.Id, material.Id, material.Name,
                material.Specifications, materialUnitId.Value, waybillQuantity.Value);
            waybillMaterial.UpdateOffsetFromWaybill(waybill);
            await _waybillMaterialRepository.InsertAsync(waybillMaterial);
        }
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task PushWaybillAsync(CancellationToken cancellationToken = default)
    {
        // 获取 LicenseInfo 以获取 proId
        var licenseInfo = await _licenseInfoRepository.FirstOrDefaultAsync(cancellationToken);
        if (licenseInfo == null)
        {
            _logger?.LogWarning("PushWaybillAsync: 未找到许可证信息，跳过运单推送");
            return;
        }

        var proId = licenseInfo.ProjectId.ToString();

        // 处理未同步的运单（新增）
        await PushNewWaybillsAsync(proId, cancellationToken);

        // 处理待更新的运单（修改）
        await PushUpdatedWaybillsAsync(proId, cancellationToken);
    }

    /// <summary>
    /// 批量查询并分组运单物料信息
    /// </summary>
    private async Task<Dictionary<long, List<WaybillMaterial>>> GetWaybillMaterialsDict(
        List<Waybill> waybills,
        CancellationToken cancellationToken)
    {
        if (waybills.Count == 0)
            return new Dictionary<long, List<WaybillMaterial>>();

        var waybillMaterialQuery = await _waybillMaterialRepository.GetQueryableAsync();
        var allWaybillMaterials = await waybillMaterialQuery
            .Where(wm => waybills.Select(w => w.Id).Contains(wm.WaybillId))
            .ToListAsync(cancellationToken);

        return allWaybillMaterials
            .GroupBy(wm => wm.WaybillId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 同步单个运单（新增）
    /// </summary>
    private async Task<bool> SyncNewWaybillAsync(
        Waybill waybill,
        List<WaybillMaterial>? waybillMaterials,
        string proId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 转换为 DTO
            var dto = SynchronizationOrderInputDto.FromWaybill(
                waybill,
                waybillMaterials,
                proId,
                receivederId: null);

            // 调用同步 API
            var result = await _materialPlatformApi.SynchronizationOrderAsync(dto, cancellationToken);

            if (result.Success)
            {
                // 更新同步时间
                waybill.PushCompleted(DateTime.Now);
                await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
                _logger?.LogInformation(
                    "SyncNewWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 同步成功",
                    waybill.Id, waybill.OrderNo);
                return true;
            }
            else
            {
                _logger?.LogWarning(
                    "SyncNewWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 同步失败，API 返回 false",
                    waybill.Id, waybill.OrderNo);

                waybill.PushCompleted(DateTime.Now);
                waybill.SetPendingSync();
                await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "SyncNewWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 同步时发生异常",
                waybill.Id, waybill.OrderNo);
            return false;
        }
    }

    /// <summary>
    /// 更新单个运单（修改）
    /// </summary>
    private async Task<bool> SyncUpdatedWaybillAsync(
        Waybill waybill,
        List<WaybillMaterial>? waybillMaterials,
        string proId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 转换为 DTO
            var dto = SynchronizationOrderInputDto.FromWaybill(
                waybill,
                waybillMaterials,
                proId,
                receivederId: null);

            // 调用修改订单 API
            var result = await _materialPlatformApi.SynchronizationModifyOrderAsync(dto, cancellationToken);

            if (result.Success)
            {
                // 重置待同步状态
                waybill.ResetPendingSync(DateTime.Now);
                await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
                _logger?.LogInformation(
                    "SyncUpdatedWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 更新成功",
                    waybill.Id, waybill.OrderNo);
                return true;
            }
            else
            {
                _logger?.LogWarning(
                    "SyncUpdatedWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 更新失败，API 返回 false",
                    waybill.Id, waybill.OrderNo);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "SyncUpdatedWaybillAsync: 运单 {WaybillId} (订单号: {OrderNo}) 更新时发生异常",
                waybill.Id, waybill.OrderNo);
            return false;
        }
    }

    /// <summary>
    /// 推送未同步的运单（新增）
    /// </summary>
    private async Task PushNewWaybillsAsync(string proId, CancellationToken cancellationToken)
    {
        // 查询所有 LastSyncTime 为 null 的运单
        var waybillQuery = await _waybillRepository.GetQueryableAsync();
        var unsyncedWaybills = await waybillQuery
            .Where(w => w.LastSyncTime == null)
            .ToListAsync(cancellationToken);

        if (unsyncedWaybills.Count == 0)
        {
            _logger?.LogInformation("PushNewWaybillsAsync: 没有需要同步的新运单");
            return;
        }

        _logger?.LogInformation("PushNewWaybillsAsync: 开始推送 {Count} 个未同步的运单", unsyncedWaybills.Count);

        var successCount = 0;
        var failCount = 0;

        // 批量查询运单物料信息
        var waybillMaterialsDict = await GetWaybillMaterialsDict(unsyncedWaybills, cancellationToken);

        // 对每个运单进行同步
        foreach (var waybill in unsyncedWaybills)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 获取该运单的物料集合
            var waybillMaterials = waybillMaterialsDict.TryGetValue(waybill.Id, out var materials)
                ? materials
                : null;

            var success = await SyncNewWaybillAsync(waybill, waybillMaterials, proId, cancellationToken);
            if (success)
                successCount++;
            else
                failCount++;
        }

        _logger?.LogInformation(
            "PushNewWaybillsAsync: 推送完成，成功: {SuccessCount}, 失败: {FailCount}",
            successCount, failCount);
    }

    /// <summary>
    /// 推送待更新的运单（修改）
    /// </summary>
    private async Task PushUpdatedWaybillsAsync(string proId, CancellationToken cancellationToken)
    {
        // 查询所有 LastSyncTime != null && IsPendingSync == true 的运单
        var waybillQuery = await _waybillRepository.GetQueryableAsync();
        var unUpdatedWaybills = await waybillQuery
            .Where(w => w.LastSyncTime != null && w.IsPendingSync == true)
            .ToListAsync(cancellationToken);

        if (unUpdatedWaybills.Count == 0)
        {
            _logger?.LogInformation("PushUpdatedWaybillsAsync: 没有需要更新的运单");
            return;
        }

        _logger?.LogInformation("PushUpdatedWaybillsAsync: 开始推送 {Count} 个待更新的运单", unUpdatedWaybills.Count);

        var successCount = 0;
        var failCount = 0;

        // 批量查询运单物料信息
        var waybillMaterialsDict = await GetWaybillMaterialsDict(unUpdatedWaybills, cancellationToken);

        // 对每个运单进行更新
        foreach (var waybill in unUpdatedWaybills)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 获取该运单的物料集合
            var waybillMaterials = waybillMaterialsDict.TryGetValue(waybill.Id, out var materials)
                ? materials
                : null;

            var success = await SyncUpdatedWaybillAsync(waybill, waybillMaterials, proId, cancellationToken);
            if (success)
                successCount++;
            else
                failCount++;
        }

        _logger?.LogInformation(
            "PushUpdatedWaybillsAsync: 更新完成，成功: {SuccessCount}, 失败: {FailCount}",
            successCount, failCount);
    }
}

public record UpdateWaybillInput(
    long WaybillId,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? WaybillQuantity
);

public record UpdateWeighingRecordInput(
    long WeighingRecordId,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? WaybillQuantity,
    DeliveryType? DeliveryType
);

public record UpdateListItemInput(
    long Id,
    WeighingListItemType ItemType,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? WaybillQuantity,
    DeliveryType? DeliveryType
);