using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Configuration;
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
}

/// <summary>
/// Service for matching weighing records and creating waybills
/// </summary>
[AutoConstructor]
public partial class WeighingMatchingService : DomainService, IWeighingMatchingService
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<Material, int> _materialRepository;
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

    private async Task TryCalculateMaterialAsync(Waybill waybill, int? materialId, int? materialUnitId,
        decimal? waybillQuantity)
    {
        if (!materialUnitId.HasValue || !waybillQuantity.HasValue || !materialId.HasValue)
            return;

        var material = await _materialRepository.GetAsync(materialId.Value);

        // 更新 Waybill 的物料信息
        waybill.MaterialId = materialId;
        waybill.MaterialUnitId = materialUnitId;
        waybill.OrderPlanOnPcs = waybillQuantity;
        waybill.MaterialUnitRate = material.UnitRate;
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


    [UnitOfWork]
    private async Task CreateWaybillAsync(WeighingRecord joinRecord, WeighingRecord outRecord,
        DeliveryType deliveryType)
    {
        var todayCount = await _waybillRepository.CountAsync(w =>
            w.CreationTime.Date == DateTime.Today);

        var orderNo = Waybill.GenerateOrderNo(deliveryType, joinRecord.CreationTime, todayCount);
        var waybill = new Waybill(orderNo)
        {
            ProviderId = joinRecord.ProviderId ?? outRecord.ProviderId,
            PlateNumber = joinRecord.PlateNumber ?? outRecord.PlateNumber,
            JoinTime = joinRecord.CreationTime,
            OutTime = outRecord.CreationTime,
            DeliveryType = deliveryType,
            OrderSource = OrderSource.MannedStation,
            OrderType = OrderTypeEnum.Completed
        };
        waybill.SetWeight(joinRecord, outRecord, deliveryType);
        joinRecord.MatchAsJoin(outRecord.Id);
        outRecord.MatchAsOut(joinRecord.Id);

        // 先插入 Waybill 获取 Id
        await _waybillRepository.InsertAsync(waybill);
        await _weighingRecordRepository.UpdateAsync(joinRecord);
        await _weighingRecordRepository.UpdateAsync(outRecord);


        // 计算物料信息（从 Materials 集合中获取）
        var joinMaterial = joinRecord.Materials?.FirstOrDefault();
        var outMaterial = outRecord.Materials?.FirstOrDefault();
        var materialId = joinMaterial?.MaterialId ?? outMaterial?.MaterialId;
        var materialUnitId = joinMaterial?.MaterialUnitId ?? outMaterial?.MaterialUnitId;
        var waybillQuantity = joinMaterial?.WaybillQuantity ?? outMaterial?.WaybillQuantity;
        await TryCalculateMaterialAsync(waybill, materialId, materialUnitId, waybillQuantity);
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

        return new PagedResultDto<WeighingListItemDto>(totalCount, items);
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