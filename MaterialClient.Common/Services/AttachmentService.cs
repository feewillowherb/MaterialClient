using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.ChangeTracking;
using Volo.Abp.Domain.Repositories;

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
}