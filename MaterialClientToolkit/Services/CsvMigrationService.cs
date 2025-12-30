using MaterialClient.Common.Entities;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MaterialClientToolkit.Services;

/// <summary>
/// CSV数据迁移服务
/// </summary>
public class CsvMigrationService
{
    private readonly CsvReaderService _csvReaderService;
    private readonly CsvMapperService _csvMapperService;
    private readonly ILogger<CsvMigrationService>? _logger;
    private readonly string _connectionString;

    public CsvMigrationService(
        CsvReaderService csvReaderService,
        CsvMapperService csvMapperService,
        string connectionString,
        ILogger<CsvMigrationService>? logger = null)
    {
        _csvReaderService = csvReaderService;
        _csvMapperService = csvMapperService;
        _connectionString = connectionString;
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

        // 创建DbContext
        var optionsBuilder = new DbContextOptionsBuilder<MaterialClientDbContext>();
        optionsBuilder.UseSqlite(_connectionString)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();

        using var dbContext = new MaterialClientDbContext(optionsBuilder.Options);

        // 使用事务确保数据一致性
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            _logger?.LogInformation("开始数据迁移...");

            // 1. 迁移Material_Order到Waybill和WeighingRecord
            var waybills = new List<Waybill>();
            var weighingRecords = new List<WeighingRecord>();
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
                    weighingRecords.Add(weighingRecord);
                    weighingRecordIdMap[order.OrderId] = weighingRecord.Id;
                }
            }

            _logger?.LogInformation($"准备插入: Waybills={waybills.Count}, WeighingRecords={weighingRecords.Count}");

            // 批量插入Waybill
            if (waybills.Any())
            {
                dbContext.Waybills.AddRange(waybills);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybills.Count} 条Waybill记录");
            }

            // 批量插入WeighingRecord
            if (weighingRecords.Any())
            {
                dbContext.WeighingRecords.AddRange(weighingRecords);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {weighingRecords.Count} 条WeighingRecord记录");
            }

            // 2. 迁移Material_OrderGoods到WaybillMaterial
            var waybillMaterials = new List<WaybillMaterial>();
            foreach (var orderGood in orderGoods)
            {
                if (waybillIdMap.TryGetValue(orderGood.OrderId, out var waybillId))
                {
                    var waybillMaterial = _csvMapperService.MapToWaybillMaterial(orderGood, waybillId);
                    waybillMaterials.Add(waybillMaterial);
                }
                else
                {
                    _logger?.LogWarning($"OrderId {orderGood.OrderId} 对应的Waybill不存在，跳过WaybillMaterial创建");
                }
            }

            if (waybillMaterials.Any())
            {
                dbContext.WaybillMaterials.AddRange(waybillMaterials);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillMaterials.Count} 条WaybillMaterial记录");
            }

            // 3. 迁移Material_Attaches到AttachmentFile及关联表
            var attachmentFiles = new List<AttachmentFile>();
            var waybillAttachments = new List<WaybillAttachment>();
            var weighingRecordAttachments = new List<WeighingRecordAttachment>();
            var attachmentFileIdMap = new Dictionary<int, int>(); // CSV FileId -> 数据库AttachmentFileId

            foreach (var attach in attaches)
            {
                var attachmentFile = _csvMapperService.MapToAttachmentFile(attach);
                attachmentFiles.Add(attachmentFile);
                attachmentFileIdMap[attach.FileId] = attachmentFile.Id;

                // 判断是Waybill还是WeighingRecord的附件
                if (_csvMapperService.IsBizTypeForWaybill(attach.BizType))
                {
                    // 查找对应的Waybill
                    if (waybillIdMap.TryGetValue(attach.BizId, out var waybillId))
                    {
                        var waybillAttachment = new WaybillAttachment(waybillId, attachmentFile.Id);
                        waybillAttachments.Add(waybillAttachment);
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
                        var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                        weighingRecordAttachments.Add(weighingRecordAttachment);
                    }
                    else
                    {
                        _logger?.LogWarning($"BizId {attach.BizId} 对应的WeighingRecord不存在，跳过WeighingRecordAttachment创建");
                    }
                }
            }

            if (attachmentFiles.Any())
            {
                dbContext.AttachmentFiles.AddRange(attachmentFiles);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {attachmentFiles.Count} 条AttachmentFile记录");
            }

            if (waybillAttachments.Any())
            {
                dbContext.WaybillAttachments.AddRange(waybillAttachments);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {waybillAttachments.Count} 条WaybillAttachment记录");
            }

            if (weighingRecordAttachments.Any())
            {
                dbContext.WeighingRecordAttachments.AddRange(weighingRecordAttachments);
                await dbContext.SaveChangesAsync();
                _logger?.LogInformation($"已插入 {weighingRecordAttachments.Count} 条WeighingRecordAttachment记录");
            }

            // 提交事务
            await transaction.CommitAsync();
            _logger?.LogInformation("数据迁移完成！");

            return 0; // 成功
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "数据迁移失败，正在回滚事务...");
            await transaction.RollbackAsync();
            throw;
        }
    }
}

