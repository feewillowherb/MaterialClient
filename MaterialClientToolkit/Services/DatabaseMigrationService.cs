using MaterialClient.Common.Entities;
using MaterialClient.EFCore;
using MaterialClientToolkit.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MaterialClientToolkit.Services;

/// <summary>
/// 数据库迁移服务
/// 从加密的源数据库读取数据并迁移到目标数据库
/// </summary>
public class DatabaseMigrationService
{
    private readonly SourceDatabaseReaderService _sourceReader;
    private readonly CsvMapperService _mapperService;
    private readonly MaterialClientDbContext _targetDbContext;
    private readonly ILogger<DatabaseMigrationService>? _logger;

    public DatabaseMigrationService(
        SourceDatabaseReaderService sourceReader,
        CsvMapperService mapperService,
        MaterialClientDbContext targetDbContext,
        ILogger<DatabaseMigrationService>? logger = null)
    {
        _sourceReader = sourceReader;
        _mapperService = mapperService;
        _targetDbContext = targetDbContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行完整的数据迁移
    /// </summary>
    public async Task<int> MigrateAsync()
    {
        _logger?.LogInformation("开始从源数据库读取数据...");

        // 从源数据库读取数据（返回CSV格式的数据）
        var orders = await _sourceReader.ReadMaterialOrdersAsync();
        var orderGoods = await _sourceReader.ReadMaterialOrderGoodsAsync();
        var attaches = await _sourceReader.ReadMaterialAttachesAsync();

        _logger?.LogInformation($"读取完成: Orders={orders.Count}, OrderGoods={orderGoods.Count}, Attaches={attaches.Count}");

        // 禁用审计概念，因为源数据库数据已经包含了审计信息
        _targetDbContext.DisableAuditConcepts = true;

        // 使用事务确保数据一致性
        using var transaction = await _targetDbContext.Database.BeginTransactionAsync();

        try
        {
            _logger?.LogInformation("开始数据迁移...");

            // 1. 迁移Material_Order到Waybill和WeighingRecord（复用CSV映射逻辑）
            var waybills = new List<Waybill>();
            var weighingRecords = new List<WeighingRecord>();
            var waybillIdMap = new Dictionary<long, long>(); // CSV OrderId -> 数据库WaybillId
            var weighingRecordIdMap = new Dictionary<long, long>(); // CSV OrderId -> 数据库WeighingRecordId

            foreach (var order in orders)
            {
                if (_mapperService.IsWaybill(order))
                {
                    var waybill = _mapperService.MapToWaybill(order);
                    waybills.Add(waybill);
                    waybillIdMap[order.OrderId] = waybill.Id;
                }
                else
                {
                    var weighingRecord = _mapperService.MapToWeighingRecord(order);
                    weighingRecords.Add(weighingRecord);
                    weighingRecordIdMap[order.OrderId] = weighingRecord.Id;
                }
            }

            _logger?.LogInformation($"准备插入: Waybills={waybills.Count}, WeighingRecords={weighingRecords.Count}");

            // 批量插入Waybill
            if (waybills.Any())
            {
                _targetDbContext.Waybills.AddRange(waybills);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybills.Count} 条Waybill记录");
            }

            // 批量插入WeighingRecord
            if (weighingRecords.Any())
            {
                _targetDbContext.WeighingRecords.AddRange(weighingRecords);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {weighingRecords.Count} 条WeighingRecord记录");
            }

            // 2. 迁移Material_OrderGoods到WaybillMaterial（复用CSV映射逻辑）
            var waybillMaterials = new List<WaybillMaterial>();
            
            // 2.1 查询所有需要的MaterialId，批量查询MaterialName
            var materialIds = orderGoods.Select(og => og.GoodsId).Distinct().ToList();
            var materialNameMap = new Dictionary<int, string?>();
            
            if (materialIds.Any())
            {
                materialNameMap = await _sourceReader.ReadMaterialNameMapAsync(materialIds);
            }
            
            // 2.2 批量查询Material_GoodsUnits的Rate和Material_Goods的Specifications
            var goodsUnitsMap = await _sourceReader.ReadMaterialGoodsUnitsAsync();
            var goodsSpecificationsMap = await _sourceReader.ReadMaterialGoodsSpecificationsAsync(materialIds);
            
            // 2.3 创建WaybillMaterial，并填充MaterialName、Specifications和Rate
            var waybillMaterialMap = new Dictionary<long, WaybillMaterial>(); // WaybillId -> WaybillMaterial（用于后续更新Waybill）
            
            foreach (var orderGood in orderGoods)
            {
                if (waybillIdMap.TryGetValue(orderGood.OrderId, out var waybillId))
                {
                    // 从Material表查询MaterialName，查询不到则为null
                    materialNameMap.TryGetValue(orderGood.GoodsId, out var materialName);
                    var waybillMaterial = _mapperService.MapToWaybillMaterial(orderGood, waybillId, materialName);
                    
                    // 从Material_Goods查询Specifications
                    if (goodsSpecificationsMap.TryGetValue(orderGood.GoodsId, out var specifications))
                    {
                        waybillMaterial.Specifications = specifications;
                    }
                    
                    waybillMaterials.Add(waybillMaterial);
                    // 保存第一个WaybillMaterial用于更新Waybill（如果有多个，使用第一个）
                    if (!waybillMaterialMap.ContainsKey(waybillId))
                    {
                        waybillMaterialMap[waybillId] = waybillMaterial;
                    }
                }
                else
                {
                    _logger?.LogWarning($"OrderId {orderGood.OrderId} 对应的Waybill不存在，跳过WaybillMaterial创建");
                }
            }

            if (waybillMaterials.Any())
            {
                _targetDbContext.WaybillMaterials.AddRange(waybillMaterials);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillMaterials.Count} 条WaybillMaterial记录");
            }

            // 2.4 更新Waybill：从WaybillMaterial聚合数据
            foreach (var waybill in waybills)
            {
                if (waybillMaterialMap.TryGetValue(waybill.Id, out var wm))
                {
                    // 更新Waybill的字段
                    waybill.MaterialId = wm.MaterialId;
                    waybill.MaterialUnitId = wm.MaterialUnitId;
                    waybill.OffsetRate = wm.OffsetRate;
                    waybill.OffsetCount = wm.OffsetCount;
                    
                    // 从Material_GoodsUnits查询Rate
                    if (wm.MaterialUnitId.HasValue && wm.MaterialId > 0)
                    {
                        var key = (wm.MaterialUnitId.Value, wm.MaterialId);
                        if (goodsUnitsMap.TryGetValue(key, out var rate))
                        {
                            waybill.MaterialUnitRate = rate;
                        }
                    }
                }
            }
            
            // 保存Waybill的更新
            if (waybills.Any())
            {
                _targetDbContext.Waybills.UpdateRange(waybills);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已更新 {waybills.Count} 条Waybill记录");
            }

            // 3. 迁移Material_Attaches到AttachmentFile及关联表（复用CSV映射逻辑）
            var attachmentFiles = new List<AttachmentFile>();
            var attachmentFileCsvIdMap = new Dictionary<int, AttachmentFile>(); // CSV FileId -> AttachmentFile对象

            // 3.1 先创建所有AttachmentFile对象
            foreach (var attach in attaches)
            {
                var attachmentFile = _mapperService.MapToAttachmentFile(attach);
                attachmentFiles.Add(attachmentFile);
                attachmentFileCsvIdMap[attach.FileId] = attachmentFile;
            }

            // 3.2 批量插入AttachmentFile（必须先保存以获取真实ID）
            if (attachmentFiles.Any())
            {
                _targetDbContext.AttachmentFiles.AddRange(attachmentFiles);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {attachmentFiles.Count} 条AttachmentFile记录");
            }

            // 3.3 创建关联关系（WaybillAttachment和WeighingRecordAttachment）
            var waybillAttachments = new List<WaybillAttachment>();
            var weighingRecordAttachments = new List<WeighingRecordAttachment>();
            var waybillAttachmentKeys = new HashSet<(long WaybillId, int AttachmentFileId)>(); // 用于去重
            var weighingRecordAttachmentKeys = new HashSet<(long WeighingRecordId, int AttachmentFileId)>(); // 用于去重

            foreach (var attach in attaches)
            {
                if (!attachmentFileCsvIdMap.TryGetValue(attach.FileId, out var attachmentFile))
                {
                    _logger?.LogWarning($"FileId {attach.FileId} 对应的AttachmentFile未找到，跳过关联关系创建");
                    continue;
                }

                // 判断是Waybill还是WeighingRecord的附件
                if (_mapperService.IsBizTypeForWaybill(attach.BizType))
                {
                    // 查找对应的Waybill
                    if (waybillIdMap.TryGetValue(attach.BizId, out var waybillId))
                    {
                        var key = (waybillId, attachmentFile.Id);
                        // 检查是否已存在，避免重复
                        if (!waybillAttachmentKeys.Contains(key))
                        {
                            var waybillAttachment = new WaybillAttachment(waybillId, attachmentFile.Id);
                            waybillAttachments.Add(waybillAttachment);
                            waybillAttachmentKeys.Add(key);
                        }
                        else
                        {
                            _logger?.LogWarning($"WaybillAttachment已存在: WaybillId={waybillId}, AttachmentFileId={attachmentFile.Id}，跳过重复创建");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning($"BizId {attach.BizId} 对应的Waybill不存在，跳过WaybillAttachment创建");
                    }
                }
                else
                {
                    // 查找对应的WeighingRecord（BizId应该对应OrderId）
                    if (weighingRecordIdMap.TryGetValue(attach.BizId, out var weighingRecordId))
                    {
                        var key = (weighingRecordId, attachmentFile.Id);
                        // 检查是否已存在，避免重复
                        if (!weighingRecordAttachmentKeys.Contains(key))
                        {
                            var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                            weighingRecordAttachments.Add(weighingRecordAttachment);
                            weighingRecordAttachmentKeys.Add(key);
                        }
                        else
                        {
                            _logger?.LogWarning($"WeighingRecordAttachment已存在: WeighingRecordId={weighingRecordId}, AttachmentFileId={attachmentFile.Id}，跳过重复创建");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning($"BizId {attach.BizId} 对应的WeighingRecord不存在，跳过WeighingRecordAttachment创建");
                    }
                }
            }

            // 3.4 批量插入WaybillAttachment关联关系
            if (waybillAttachments.Any())
            {
                _targetDbContext.WaybillAttachments.AddRange(waybillAttachments);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillAttachments.Count} 条WaybillAttachment关联记录");
            }

            // 3.5 批量插入WeighingRecordAttachment关联关系
            if (weighingRecordAttachments.Any())
            {
                _targetDbContext.WeighingRecordAttachments.AddRange(weighingRecordAttachments);
                await _targetDbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {weighingRecordAttachments.Count} 条WeighingRecordAttachment关联记录");
            }

            // 提交事务
            await transaction.CommitAsync();
            _logger?.LogInformation("数据迁移完成！");

            return 0; // 成功
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "数据迁移失败，正在回滚事务...");
            
            // 构建详细的错误信息
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine($"错误: {ex.Message}");
            
            // 递归显示所有内部异常
            var innerEx = ex.InnerException;
            int depth = 1;
            while (innerEx != null && depth <= 5)
            {
                errorBuilder.AppendLine($"内部异常 #{depth}: {innerEx.Message}");
                errorBuilder.AppendLine($"  类型: {innerEx.GetType().FullName}");
                if (!string.IsNullOrEmpty(innerEx.StackTrace))
                {
                    var stackLines = innerEx.StackTrace.Split('\n').Take(3);
                    errorBuilder.AppendLine($"  堆栈: {string.Join("", stackLines)}");
                }
                innerEx = innerEx.InnerException;
                depth++;
            }
            
            // 如果是DbUpdateException，显示更多详细信息
            if (ex is DbUpdateException dbEx)
            {
                errorBuilder.AppendLine("\n数据库更新异常详情:");
                foreach (var entry in dbEx.Entries)
                {
                    errorBuilder.AppendLine($"  实体类型: {entry.Entity.GetType().Name}, 状态: {entry.State}");
                    
                    // 显示实体的关键属性值
                    try
                    {
                        var entityType = entry.Entity.GetType();
                        var idProperty = entityType.GetProperty("Id");
                        if (idProperty != null)
                        {
                            var idValue = idProperty.GetValue(entry.Entity);
                            errorBuilder.AppendLine($"    Id: {idValue}");
                        }
                        
                        // 显示其他关键属性
                        var keyProperties = new[] { "WaybillId", "MaterialId", "OrderNo", "FileName" };
                        foreach (var propName in keyProperties)
                        {
                            var prop = entityType.GetProperty(propName);
                            if (prop != null)
                            {
                                var propValue = prop.GetValue(entry.Entity);
                                if (propValue != null)
                                {
                                    errorBuilder.AppendLine($"    {propName}: {propValue}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略属性访问错误
                    }
                }
            }
            
            var errorMessage = errorBuilder.ToString();
            _logger?.LogError(errorMessage);
            Console.WriteLine(errorMessage);
            
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            // 恢复审计概念设置
            _targetDbContext.DisableAuditConcepts = false;
        }
    }
}

