using MaterialClientToolkit.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MaterialClientToolkit.Services;

/// <summary>
/// 源数据库读取服务
/// 从加密的源数据库读取数据（表结构与CSV一致）
/// </summary>
public class SourceDatabaseReaderService
{
    private readonly string _sourceConnectionString;
    private readonly ILogger<SourceDatabaseReaderService>? _logger;

    public SourceDatabaseReaderService(
        string sourceConnectionString,
        ILogger<SourceDatabaseReaderService>? logger = null)
    {
        _sourceConnectionString = sourceConnectionString;
        _logger = logger;
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_sourceConnectionString);
    }

    /// <summary>
    /// 读取所有Material_Order记录（对应CSV结构）
    /// </summary>
    public async Task<List<MaterialOrderCsv>> ReadMaterialOrdersAsync()
    {
        var orders = new List<MaterialOrderCsv>();

        using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT OrderId, ProviderId, OrderNo, OrderType, DeliveryType, TruckNo, DispatchNo,
                   OrderPlanOnWeight, OrderPlanOnPcs, OrderPcs, JoinTime, OutTime, Remark,
                   OrderTotalWeight, OrderTruckWeight, OrderGoodsWeight, DeleteStatus,
                   LastEditUserId, LastEditor, CreateUserId, Creator, UpdateTime, AddDate,
                   UpdateDate, AddTime, LastSyncTime, EarlyWarnStatus, PrintCount, AbortReason,
                   ReceivederId, OffsetResult, EarlyWarnType, OrderSource, TruckNum
            FROM Material_Order
            WHERE DeleteStatus = 0";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var order = new MaterialOrderCsv
            {
                OrderId = reader.GetInt64(0),
                ProviderId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                OrderNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                OrderType = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DeliveryType = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                TruckNo = reader.IsDBNull(5) ? null : reader.GetString(5),
                DispatchNo = reader.IsDBNull(6) ? null : reader.GetString(6),
                OrderPlanOnWeight = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                OrderPlanOnPcs = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                OrderPcs = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                JoinTime = reader.IsDBNull(10) ? null : reader.GetString(10),
                OutTime = reader.IsDBNull(11) ? null : reader.GetString(11),
                Remark = reader.IsDBNull(12) ? null : reader.GetString(12),
                OrderTotalWeight = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                OrderTruckWeight = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                OrderGoodsWeight = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                DeleteStatus = reader.GetInt32(16),
                LastEditUserId = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                LastEditor = reader.IsDBNull(18) ? null : reader.GetString(18),
                CreateUserId = reader.IsDBNull(19) ? null : reader.GetInt32(19),
                Creator = reader.IsDBNull(20) ? null : reader.GetString(20),
                UpdateTime = reader.IsDBNull(21) ? null : reader.GetInt32(21),
                AddDate = reader.IsDBNull(22) ? null : reader.GetString(22),
                UpdateDate = reader.IsDBNull(23) ? null : reader.GetString(23),
                AddTime = reader.IsDBNull(24) ? null : reader.GetInt32(24),
                LastSyncTime = reader.IsDBNull(25) ? null : reader.GetString(25),
                EarlyWarnStatus = reader.IsDBNull(26) ? null : reader.GetInt32(26),
                PrintCount = reader.IsDBNull(27) ? 0 : reader.GetInt32(27),
                AbortReason = reader.IsDBNull(28) ? null : reader.GetString(28),
                ReceivederId = reader.IsDBNull(29) ? null : reader.GetInt32(29),
                OffsetResult = reader.IsDBNull(30) ? null : reader.GetInt32(30),
                EarlyWarnType = reader.IsDBNull(31) ? null : reader.GetString(31),
                OrderSource = reader.IsDBNull(32) ? null : reader.GetInt32(32),
                TruckNum = reader.IsDBNull(33) ? null : reader.GetString(33)
            };
            orders.Add(order);
        }

        _logger?.LogInformation($"从源数据库读取到 {orders.Count} 条Material_Order记录");
        return orders;
    }

    /// <summary>
    /// 读取所有Material_OrderGoods记录（对应CSV结构）
    /// </summary>
    public async Task<List<MaterialOrderGoodsCsv>> ReadMaterialOrderGoodsAsync()
    {
        var orderGoods = new List<MaterialOrderGoodsCsv>();

        using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT OGId, OrderId, GoodsId, UnitId, GoodsPlanOnWeight, GoodsPlanOnPcs,
                   GoodsPcs, GoodsWeight, GoodsTakeWeight, DeleteStatus,
                   LastEditUserId, LastEditor, CreateUserId, Creator, UpdateTime, AddTime,
                   UpdateDate, AddDate, OffsetResult, OffsetWeight, OffsetCount, OffsetRate
            FROM Material_OrderGoods
            WHERE DeleteStatus = 0";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var orderGood = new MaterialOrderGoodsCsv
            {
                OGId = reader.GetInt32(0),
                OrderId = reader.GetInt64(1),
                GoodsId = reader.GetInt32(2),
                UnitId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                GoodsPlanOnWeight = reader.GetDecimal(4),
                GoodsPlanOnPcs = reader.GetDecimal(5),
                GoodsPcs = reader.GetDecimal(6),
                GoodsWeight = reader.GetDecimal(7),
                GoodsTakeWeight = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                DeleteStatus = reader.GetInt32(9),
                LastEditUserId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                LastEditor = reader.IsDBNull(11) ? null : reader.GetString(11),
                CreateUserId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                Creator = reader.IsDBNull(13) ? null : reader.GetString(13),
                UpdateTime = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                AddTime = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                UpdateDate = reader.IsDBNull(16) ? null : reader.GetString(16),
                AddDate = reader.IsDBNull(17) ? null : reader.GetString(17),
                OffsetResult = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),
                OffsetWeight = reader.GetDecimal(19),
                OffsetCount = reader.GetDecimal(20),
                OffsetRate = reader.GetDecimal(21)
            };
            orderGoods.Add(orderGood);
        }

        _logger?.LogInformation($"从源数据库读取到 {orderGoods.Count} 条Material_OrderGoods记录");
        return orderGoods;
    }

    /// <summary>
    /// 读取所有Material_Attaches记录（对应CSV结构）
    /// </summary>
    public async Task<List<MaterialAttachesCsv>> ReadMaterialAttachesAsync()
    {
        var attaches = new List<MaterialAttachesCsv>();

        using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT FileId, BizId, BizType, FileName, Bucket, BucketKey, FileSize,
                   UploadStatus, UploadTime, DeleteStatus,
                   LastEditUserId, LastEditor, CreateUserId, Creator, UpdateTime, AddTime,
                   UpdateDate, AddDate, LastSyncTime
            FROM Material_Attaches
            WHERE DeleteStatus = 0";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var attach = new MaterialAttachesCsv
            {
                FileId = reader.GetInt32(0),
                BizId = reader.GetInt64(1),
                BizType = reader.GetInt32(2),
                FileName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Bucket = reader.IsDBNull(4) ? null : reader.GetString(4),
                BucketKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                FileSize = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                UploadStatus = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                UploadTime = reader.IsDBNull(8) ? null : reader.GetString(8),
                DeleteStatus = reader.GetInt32(9),
                LastEditUserId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                LastEditor = reader.IsDBNull(11) ? null : reader.GetString(11),
                CreateUserId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                Creator = reader.IsDBNull(13) ? null : reader.GetString(13),
                UpdateTime = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                AddTime = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                UpdateDate = reader.IsDBNull(16) ? null : reader.GetString(16),
                AddDate = reader.IsDBNull(17) ? null : reader.GetString(17),
                LastSyncTime = reader.IsDBNull(18) ? null : reader.GetString(18)
            };
            attaches.Add(attach);
        }

        _logger?.LogInformation($"从源数据库读取到 {attaches.Count} 条Material_Attaches记录");
        return attaches;
    }

    /// <summary>
    /// 读取Material记录（用于查询MaterialName）
    /// </summary>
    public async Task<Dictionary<int, string?>> ReadMaterialNameMapAsync(List<int> materialIds)
    {
        if (!materialIds.Any())
            return new Dictionary<int, string?>();

        var materialNameMap = new Dictionary<int, string?>();

        using var connection = CreateConnection();
        await connection.OpenAsync();

        // 构建IN子句
        var placeholders = string.Join(",", materialIds.Select((_, i) => $"@p{i}"));
        var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT Id, Name
            FROM Material
            WHERE Id IN ({placeholders}) AND (DeleteStatus = 0 OR DeleteStatus IS NULL)";

        // 添加参数
        for (int i = 0; i < materialIds.Count; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", materialIds[i]);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            materialNameMap[id] = name;
        }

        _logger?.LogInformation($"从源数据库查询到 {materialNameMap.Count} 个Material记录，共需要 {materialIds.Count} 个");
        return materialNameMap;
    }

    /// <summary>
    /// 验证数据库连接
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            
            // 尝试查询一个简单的表是否存在
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Material_Order'";
            var result = await command.ExecuteScalarAsync();
            
            return result != null && Convert.ToInt64(result) > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "源数据库连接失败");
            return false;
        }
    }
}

