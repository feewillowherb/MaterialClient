using MaterialClient.Common.Entities;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace MaterialClientToolkit.Services;

/// <summary>
/// CSV数据迁移服务
/// </summary>
public class CsvMigrationService: ITransientDependency
{
    private readonly CsvReaderService _csvReaderService;
    private readonly CsvMapperService _csvMapperService;
    private readonly MaterialClientDbContext _dbContext;
    private readonly ILogger<CsvMigrationService>? _logger;

    public CsvMigrationService(
        CsvReaderService csvReaderService,
        CsvMapperService csvMapperService,
        MaterialClientDbContext dbContext,
        ILogger<CsvMigrationService>? logger = null)
    {
        _csvReaderService = csvReaderService;
        _csvMapperService = csvMapperService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行完整的数据迁移
    /// </summary>
    public async Task<int> MigrateAsync(string csvDirectory)
    {
        var csvPath = Path.Combine(csvDirectory, "Csv");
        var materialOrderPath = Path.Combine(csvPath, "Material_Order.csv");
        var materialOrderGoodsPath = Path.Combine(csvPath, "Material_OrderGoods.csv");
        var materialAttachesPath = Path.Combine(csvPath, "Material_Attaches.csv");

        _logger?.LogInformation("开始读取CSV文件...");

        // 读取CSV文件
        var orders = await _csvReaderService.ReadMaterialOrderAsync(materialOrderPath);
        var orderGoods = await _csvReaderService.ReadMaterialOrderGoodsAsync(materialOrderGoodsPath);
        var attaches = await _csvReaderService.ReadMaterialAttachesAsync(materialAttachesPath);

        _logger?.LogInformation($"读取完成: Orders={orders.Count}, OrderGoods={orderGoods.Count}, Attaches={attaches.Count}");

        // 禁用审计概念，因为CSV数据已经包含了审计信息
        _dbContext.DisableAuditConcepts = true;

        // 使用事务确保数据一致性
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            _logger?.LogInformation("开始数据迁移...");

            // 1. 迁移Material_Order到Waybill和WeighingRecord
            var waybills = new List<Waybill>();
            var weighingRecords = new List<(long OrderId, WeighingRecord Record)>();
            var waybillIdMap = new Dictionary<long, long>(); // CSV OrderId -> 数据库WaybillId
            var weighingRecordIdMap = new Dictionary<long, long>(); // CSV OrderId -> 数据库WeighingRecordId

            foreach (var order in orders)
            {
                if (_csvMapperService.IsWaybill(order))
                {
                    var waybill = _csvMapperService.MapToWaybill(order);
                    waybills.Add(waybill);
                    waybillIdMap[order.OrderId] = waybill.Id;
                }
                else
                {
                    var weighingRecord = _csvMapperService.MapToWeighingRecord(order);
                    weighingRecords.Add((order.OrderId, weighingRecord));
                }
            }

            _logger?.LogInformation($"准备插入: Waybills={waybills.Count}, WeighingRecords={weighingRecords.Count}");

            // 批量插入Waybill
            if (waybills.Any())
            {
                _dbContext.Waybills.AddRange(waybills);
                await _dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybills.Count} 条Waybill记录");
            }

            // 批量插入WeighingRecord
            if (weighingRecords.Any())
            {
                var recordsToInsert = weighingRecords.Select(x => x.Record).ToList();
                _dbContext.WeighingRecords.AddRange(recordsToInsert);
                await _dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {recordsToInsert.Count} 条WeighingRecord记录");
                
                // 更新weighingRecordIdMap，使用实际生成的ID
                foreach (var (orderId, record) in weighingRecords)
                {
                    weighingRecordIdMap[orderId] = record.Id;
                }
            }

            // 2. 迁移Material_OrderGoods到WaybillMaterial
            var waybillMaterials = new List<WaybillMaterial>();
            
            // 2.1 查询所有需要的MaterialId，批量查询MaterialName
            var materialIds = orderGoods.Select(og => og.GoodsId).Distinct().ToList();
            var materialNameMap = new Dictionary<int, string?>();
            
            if (materialIds.Any())
            {
                var materials = await _dbContext.Materials
                    .Where(m => materialIds.Contains(m.Id) && !m.IsDeleted)
                    .Select(m => new { m.Id, m.Name })
                    .ToListAsync();
                
                foreach (var material in materials)
                {
                    materialNameMap[material.Id] = material.Name;
                }
                
                _logger?.LogInformation($"查询到 {materials.Count} 个Material记录，共需要 {materialIds.Count} 个");
            }
            
            // 2.2 创建WaybillMaterial，并填充MaterialName
            foreach (var orderGood in orderGoods)
            {
                if (waybillIdMap.TryGetValue(orderGood.OrderId, out var waybillId))
                {
                    // 从Material表查询MaterialName，查询不到则为null
                    materialNameMap.TryGetValue(orderGood.GoodsId, out var materialName);
                    var waybillMaterial = _csvMapperService.MapToWaybillMaterial(orderGood, waybillId, materialName);
                    waybillMaterials.Add(waybillMaterial);
                }
                else
                {
                    _logger?.LogWarning($"OrderId {orderGood.OrderId} 对应的Waybill不存在，跳过WaybillMaterial创建");
                }
            }

            if (waybillMaterials.Any())
            {
                _dbContext.WaybillMaterials.AddRange(waybillMaterials);
                await _dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillMaterials.Count} 条WaybillMaterial记录");
            }

            // 3. 迁移Material_Attaches到AttachmentFile及关联表
            var attachmentFiles = new List<AttachmentFile>();
            var attachmentFileCsvIdMap = new Dictionary<int, AttachmentFile>(); // CSV FileId -> AttachmentFile对象

            // 3.1 先创建所有AttachmentFile对象
            foreach (var attach in attaches)
            {
                // 判断附件是属于Waybill还是WeighingRecord
                bool isForWaybill = waybillIdMap.ContainsKey(attach.BizId);
                var attachmentFile = _csvMapperService.MapToAttachmentFile(attach, isForWaybill);
                attachmentFiles.Add(attachmentFile);
                attachmentFileCsvIdMap[attach.FileId] = attachmentFile;
            }

            // 3.2 批量插入AttachmentFile（必须先保存以获取真实ID）
            if (attachmentFiles.Any())
            {
                _dbContext.AttachmentFiles.AddRange(attachmentFiles);
                await _dbContext.SaveChangesAsync();
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

                // 判断是Waybill还是WeighingRecord的附件（通过检查BizId在映射表中的存在）
                // 先检查是否为Waybill
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
                // 再检查是否为WeighingRecord
                else if (weighingRecordIdMap.TryGetValue(attach.BizId, out var weighingRecordId))
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
                    _logger?.LogWarning($"BizId {attach.BizId} 既不在Waybill映射表中，也不在WeighingRecord映射表中，跳过关联关系创建");
                }
            }

            // 3.4 批量插入WaybillAttachment关联关系
            if (waybillAttachments.Any())
            {
                _dbContext.WaybillAttachments.AddRange(waybillAttachments);
                await _dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillAttachments.Count} 条WaybillAttachment关联记录");
            }

            // 3.5 批量插入WeighingRecordAttachment关联关系
            if (weighingRecordAttachments.Any())
            {
                _dbContext.WeighingRecordAttachments.AddRange(weighingRecordAttachments);
                await _dbContext.SaveChangesAsync();
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
            _dbContext.DisableAuditConcepts = false;
        }
    }
}

