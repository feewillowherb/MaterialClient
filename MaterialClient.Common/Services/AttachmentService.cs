using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.ChangeTracking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
/// 附件服务接口
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// 批量查询称重记录的附件
    /// </summary>
    /// <param name="weighingRecordIds">称重记录ID列表</param>
    /// <returns>字典，key为称重记录ID，value为附件文件列表</returns>
    Task<Dictionary<long, List<AttachmentFile>>> GetAttachmentsByWeighingRecordIdsAsync(
        IEnumerable<long> weighingRecordIds);

    /// <summary>
    /// 批量查询运单的附件
    /// </summary>
    /// <param name="waybillIds">运单ID列表</param>
    /// <returns>字典，key为运单ID，value为附件文件列表</returns>
    Task<Dictionary<long, List<AttachmentFile>>> GetAttachmentsByWaybillIdsAsync(IEnumerable<long> waybillIds);

    /// <summary>
    /// 根据列表项获取附件（统一接口，自动根据 ItemType 路由）
    /// </summary>
    /// <param name="item">列表项</param>
    /// <returns>附件文件列表</returns>
    Task<List<AttachmentFile>> GetAttachmentsByListItemAsync(WeighingListItemDto item);

    /// <summary>
    /// 创建或替换指定列表项的 BillPhoto 附件
    /// </summary>
    /// <param name="listItem">列表项</param>
    /// <param name="photoPath">照片文件路径</param>
    Task CreateOrReplaceBillPhotoAsync(WeighingListItemDto listItem, string photoPath);

    /// <summary>
    /// 同步指定运单的附件到OSS
    /// </summary>
    /// <param name="waybillId">运单ID</param>
    Task SyncWaybillAttachmentsToOssAsync(long waybillId);

    /// <summary>
    /// 批量同步已推送运单的附件到OSS（使用关联查询）
    /// </summary>
    Task SyncPendingAttachmentsToOssAsync();
}

/// <summary>
/// 附件服务实现
/// </summary>
[AutoConstructor]
public partial class AttachmentService : IAttachmentService, ITransientDependency
{
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<WaybillAttachment, int> _waybillAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ILogger<AttachmentService>? _logger;
    private readonly IOssUploadService _ossUploadService;


    /// <summary>
    /// 批量查询称重记录的附件
    /// </summary>
    [DisableEntityChangeTracking]
    public async Task<Dictionary<long, List<AttachmentFile>>> GetAttachmentsByWeighingRecordIdsAsync(
        IEnumerable<long> weighingRecordIds)
    {
        var result = new Dictionary<long, List<AttachmentFile>>();
        var idList = weighingRecordIds.ToList();

        if (idList.Count == 0)
            return result;

        // 初始化所有ID的空列表
        foreach (var id in idList)
        {
            result[id] = new List<AttachmentFile>();
        }

        try
        {
            // 批量查询关联记录
            var attachments = await _weighingRecordAttachmentRepository.GetListAsync(
                predicate: x => idList.Contains(x.WeighingRecordId)
            );

            if (attachments.Count == 0)
                return result;

            // 获取所有附件文件ID
            var attachmentFileIds = attachments.Select(x => x.AttachmentFileId).Distinct().ToList();

            // 批量查询附件文件
            var attachmentFiles = await _attachmentFileRepository.GetListAsync(
                predicate: x => attachmentFileIds.Contains(x.Id)
            );

            // 构建附件文件ID到实体的映射
            var fileDict = attachmentFiles.ToDictionary(x => x.Id);

            // 填充结果
            foreach (var attachment in attachments)
            {
                if (fileDict.TryGetValue(attachment.AttachmentFileId, out var file))
                {
                    result[attachment.WeighingRecordId].Add(file);
                }
            }
        }
        catch
        {
            // 如果查询失败，返回空字典
        }

        return result;
    }

    /// <summary>
    /// 批量查询运单的附件
    /// </summary>
    [DisableEntityChangeTracking]
    public async Task<Dictionary<long, List<AttachmentFile>>> GetAttachmentsByWaybillIdsAsync(
        IEnumerable<long> waybillIds)
    {
        var result = new Dictionary<long, List<AttachmentFile>>();
        var idList = waybillIds.ToList();

        if (idList.Count == 0)
            return result;

        // 初始化所有ID的空列表
        foreach (var id in idList)
        {
            result[id] = new List<AttachmentFile>();
        }

        try
        {
            // 批量查询关联记录
            var attachments = await _waybillAttachmentRepository.GetListAsync(
                predicate: x => idList.Contains(x.WaybillId)
            );

            if (attachments.Count == 0)
                return result;

            // 获取所有附件文件ID
            var attachmentFileIds = attachments.Select(x => x.AttachmentFileId).Distinct().ToList();

            // 批量查询附件文件
            var attachmentFiles = await _attachmentFileRepository.GetListAsync(
                predicate: x => attachmentFileIds.Contains(x.Id)
            );

            // 构建附件文件ID到实体的映射
            var fileDict = attachmentFiles.ToDictionary(x => x.Id);

            // 填充结果
            foreach (var attachment in attachments)
            {
                if (fileDict.TryGetValue(attachment.AttachmentFileId, out var file))
                {
                    result[attachment.WaybillId].Add(file);
                }
            }
        }
        catch
        {
            // 如果查询失败，返回空字典
        }

        return result;
    }

    /// <summary>
    /// 根据列表项获取附件（统一接口，自动根据 ItemType 路由）
    /// </summary>
    [DisableEntityChangeTracking]
    public async Task<List<AttachmentFile>> GetAttachmentsByListItemAsync(WeighingListItemDto item)
    {
        if (item.ItemType == WeighingListItemType.WeighingRecord)
        {
            var result = await GetAttachmentsByWeighingRecordIdsAsync(new[] { item.Id });
            return result.TryGetValue(item.Id, out var files) ? files : new List<AttachmentFile>();
        }
        else if (item.ItemType == WeighingListItemType.Waybill)
        {
            var result = await GetAttachmentsByWaybillIdsAsync(new[] { item.Id });
            return result.TryGetValue(item.Id, out var files) ? files : new List<AttachmentFile>();
        }

        return new List<AttachmentFile>();
    }

    /// <summary>
    /// 创建或替换指定列表项的 BillPhoto 附件
    /// </summary>
    [UnitOfWork]
    public async Task CreateOrReplaceBillPhotoAsync(WeighingListItemDto listItem, string photoPath)
    {
        try
        {
            // 检查文件是否存在
            if (!File.Exists(photoPath))
            {
                _logger?.LogWarning("BillPhoto file does not exist: {PhotoPath}", photoPath);
                return;
            }

            // 先删除旧的TicketPhoto附件（如果存在）
            var existingAttachments = await GetAttachmentsByListItemAsync(listItem);
            var existingTicketPhoto = existingAttachments.FirstOrDefault(a => a.AttachType == AttachType.TicketPhoto);

            if (existingTicketPhoto != null)
            {
                // 删除关联记录
                if (listItem.ItemType == WeighingListItemType.WeighingRecord)
                {
                    var existingRecordAttachments = await _weighingRecordAttachmentRepository.GetListAsync(
                        predicate: ra => ra.WeighingRecordId == listItem.Id && ra.AttachmentFileId == existingTicketPhoto.Id);
                    foreach (var recordAttachment in existingRecordAttachments)
                    {
                        await _weighingRecordAttachmentRepository.DeleteAsync(recordAttachment);
                    }
                }
                else if (listItem.ItemType == WeighingListItemType.Waybill)
                {
                    var existingWaybillAttachments = await _waybillAttachmentRepository.GetListAsync(
                        predicate: wa => wa.WaybillId == listItem.Id && wa.AttachmentFileId == existingTicketPhoto.Id);
                    foreach (var waybillAttachment in existingWaybillAttachments)
                    {
                        await _waybillAttachmentRepository.DeleteAsync(waybillAttachment);
                    }
                }

                // 删除AttachmentFile
                await _attachmentFileRepository.DeleteAsync(existingTicketPhoto);
                _logger?.LogInformation("Deleted old BillPhoto attachment: FileId={FileId}", existingTicketPhoto.Id);
            }

            // 创建新的AttachmentFile
            var fileName = Path.GetFileName(photoPath);
            var attachmentFile = new AttachmentFile(fileName, photoPath, AttachType.TicketPhoto);
            await _attachmentFileRepository.InsertAsync(attachmentFile, true);

            // 根据ItemType创建关联记录
            if (listItem.ItemType == WeighingListItemType.WeighingRecord)
            {
                var weighingRecordAttachment = new WeighingRecordAttachment(listItem.Id, attachmentFile.Id);
                await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment, true);
            }
            else if (listItem.ItemType == WeighingListItemType.Waybill)
            {
                var waybillAttachment = new WaybillAttachment(listItem.Id, attachmentFile.Id);
                await _waybillAttachmentRepository.InsertAsync(waybillAttachment, true);
            }

            _logger?.LogInformation("Successfully created BillPhoto attachment: FilePath={FilePath}, FileId={FileId}", photoPath, attachmentFile.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create BillPhoto attachment: FilePath={FilePath}", photoPath);
            throw;
        }
    }

    /// <summary>
    /// 同步指定运单的附件到OSS
    /// </summary>
    [UnitOfWork]
    public async Task SyncWaybillAttachmentsToOssAsync(long waybillId)
    {
        try
        {
            // 查询运单的所有附件
            var attachments = await _waybillAttachmentRepository.GetListAsync(
                predicate: x => x.WaybillId == waybillId
            );

            if (attachments.Count == 0)
            {
                _logger?.LogInformation("SyncWaybillAttachmentsToOssAsync: 运单 {WaybillId} 没有附件", waybillId);
                return;
            }

            // 获取所有附件文件ID
            var attachmentFileIds = attachments.Select(x => x.AttachmentFileId).Distinct().ToList();

            // 查询附件文件，只选择未上传的（OssFullPath == null）且本地文件存在的
            var attachmentFiles = await _attachmentFileRepository.GetListAsync(
                predicate: x => attachmentFileIds.Contains(x.Id) 
                    && x.OssFullPath == null 
                    && !string.IsNullOrWhiteSpace(x.LocalPath)
            );

            // 过滤掉本地文件不存在的
            var validAttachments = attachmentFiles
                .Where(af => File.Exists(af.LocalPath))
                .Select(af => (attachment: af, waybillId: waybillId))
                .ToList();

            if (validAttachments.Count == 0)
            {
                _logger?.LogInformation("SyncWaybillAttachmentsToOssAsync: 运单 {WaybillId} 没有需要上传的附件", waybillId);
                return;
            }

            // 批量上传到OSS
            var uploadResults = await _ossUploadService.UploadFilesAsync(validAttachments);

            // 更新上传成功的附件
            var now = DateTime.Now;
            foreach (var (attachmentId, ossFullPath) in uploadResults)
            {
                var attachment = validAttachments.FirstOrDefault(x => x.attachment.Id == attachmentId).attachment;
                if (attachment != null)
                {
                    attachment.OssFullPath = ossFullPath;
                    attachment.LastSyncTime = now;
                    await _attachmentFileRepository.UpdateAsync(attachment);
                }
            }

            _logger?.LogInformation(
                "SyncWaybillAttachmentsToOssAsync: 运单 {WaybillId} 附件同步完成，成功: {SuccessCount}/{TotalCount}",
                waybillId, uploadResults.Count, validAttachments.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SyncWaybillAttachmentsToOssAsync: 运单 {WaybillId} 附件同步失败", waybillId);
            // 不抛出异常，避免影响主流程
        }
    }

    /// <summary>
    /// 批量同步已推送运单的附件到OSS（使用关联查询）
    /// </summary>
    [UnitOfWork]
    public async Task SyncPendingAttachmentsToOssAsync()
    {
        try
        {
            // 获取查询对象
            var waybillQuery = await _waybillRepository.GetQueryableAsync();
            var waybillAttachmentQuery = await _waybillAttachmentRepository.GetQueryableAsync();
            var attachmentQuery = await _attachmentFileRepository.GetQueryableAsync();

            // 使用关联查询获取需要上传的附件
            // 条件：1. 运单已推送（LastSyncTime != null）
            //       2. 附件未上传（OssFullPath == null）
            //       3. 本地文件存在
            var pendingAttachments = await waybillQuery
                .Where(w => w.LastSyncTime != null)
                .Join(waybillAttachmentQuery,
                    w => w.Id,
                    wa => wa.WaybillId,
                    (w, wa) => new { Waybill = w, WaybillAttachment = wa })
                .Join(attachmentQuery,
                    wa => wa.WaybillAttachment.AttachmentFileId,
                    af => af.Id,
                    (wa, af) => new { wa.Waybill, wa.WaybillAttachment, AttachmentFile = af })
                .Where(x => x.AttachmentFile.OssFullPath == null
                    && !string.IsNullOrWhiteSpace(x.AttachmentFile.LocalPath))
                .Select(x => new
                {
                    x.AttachmentFile,
                    x.Waybill.Id
                })
                .ToListAsync();

            // 过滤掉本地文件不存在的
            var validAttachments = pendingAttachments
                .Where(x => File.Exists(x.AttachmentFile.LocalPath))
                .Select(x => (attachment: x.AttachmentFile, waybillId: x.Id))
                .ToList();

            if (validAttachments.Count == 0)
            {
                _logger?.LogInformation("SyncPendingAttachmentsToOssAsync: 没有需要上传的附件");
                return;
            }

            _logger?.LogInformation("SyncPendingAttachmentsToOssAsync: 找到 {Count} 个需要上传的附件", validAttachments.Count);

            // 按运单ID分组，便于统计
            var groupedByWaybill = validAttachments.GroupBy(x => x.waybillId).ToList();

            // 批量上传到OSS
            var uploadResults = await _ossUploadService.UploadFilesAsync(validAttachments);

            // 更新上传成功的附件
            var now = DateTime.Now;
            var successCount = 0;
            foreach (var (attachmentId, ossFullPath) in uploadResults)
            {
                var attachment = validAttachments.FirstOrDefault(x => x.attachment.Id == attachmentId).attachment;
                if (attachment != null)
                {
                    attachment.OssFullPath = ossFullPath;
                    attachment.LastSyncTime = now;
                    await _attachmentFileRepository.UpdateAsync(attachment);
                    successCount++;
                }
            }

            _logger?.LogInformation(
                "SyncPendingAttachmentsToOssAsync: 批量同步完成，成功: {SuccessCount}/{TotalCount}, 涉及运单数: {WaybillCount}",
                successCount, validAttachments.Count, groupedByWaybill.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SyncPendingAttachmentsToOssAsync: 批量同步失败");
            // 不抛出异常，避免影响主流程
        }
    }
}