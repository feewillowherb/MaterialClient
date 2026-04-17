using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     物料上行同步 DTO
/// </summary>
public record UpsertMaterialGoodDto
{
    /// <summary>
    ///     操作类型（Create/Update/Delete）
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = "Update";

    /// <summary>
    ///     物料主键ID
    /// </summary>
    [JsonPropertyName("goodsId")]
    public int? GoodsId { get; init; }

    /// <summary>
    ///     物料名称
    /// </summary>
    [JsonPropertyName("goodsName")]
    public string GoodsName { get; init; } = string.Empty;

    /// <summary>
    ///     物料编码
    /// </summary>
    [JsonPropertyName("goodsCode")]
    public string GoodsCode { get; init; } = string.Empty;

    /// <summary>
    ///     规格说明
    /// </summary>
    [JsonPropertyName("specifications")]
    public string Specifications { get; init; } = string.Empty;

    /// <summary>
    ///     基础单位
    /// </summary>
    [JsonPropertyName("basicUnit")]
    public string BasicUnit { get; init; } = string.Empty;

    /// <summary>
    ///     上限
    /// </summary>
    [JsonPropertyName("upperLimit")]
    public decimal UpperLimit { get; init; }

    /// <summary>
    ///     下限
    /// </summary>
    [JsonPropertyName("lowerLimit")]
    public decimal LowerLimit { get; init; }

    /// <summary>
    ///     物料类型ID
    /// </summary>
    [JsonPropertyName("materialTypeId")]
    public int? MaterialTypeId { get; init; }

    /// <summary>
    ///     供应商ID
    /// </summary>
    [JsonPropertyName("proId")]
    public string ProId { get; init; } = string.Empty;

    /// <summary>
    ///     公司ID
    /// </summary>
    [JsonPropertyName("coId")]
    public int CoId { get; init; }

    /// <summary>
    ///     单位列表
    /// </summary>
    [JsonPropertyName("units")]
    public List<UpsertMaterialUnitDto> Units { get; init; } = [];

    /// <summary>
    ///     基础版本（用于乐观并发控制）
    /// </summary>
    [JsonPropertyName("baseVersion")]
    public long? BaseVersion { get; init; }

    /// <summary>
    ///     客户端请求ID（幂等键）
    /// </summary>
    [JsonPropertyName("clientRequestId")]
    public string ClientRequestId { get; init; } = string.Empty;

    /// <summary>
    ///     从 Material 实体创建 DTO
    /// </summary>
    public static UpsertMaterialGoodDto FromEntity(Material material, long localVersion, Guid clientRequestId, string action = "Update")
    {
        return new UpsertMaterialGoodDto
        {
            Action = action,
            GoodsId = material.Id,
            GoodsName = material.Name,
            GoodsCode = material.Code ?? string.Empty,
            Specifications = material.Specifications ?? string.Empty,
            BasicUnit = material.BasicUnit ?? string.Empty,
            UpperLimit = material.UpperLimit ?? 0,
            LowerLimit = material.LowerLimit ?? 0,
            MaterialTypeId = null, // TODO: 从 MaterialType 获取
            ProId = material.ProId ?? string.Empty,
            CoId = material.CoId,
            Units = [], // TODO: 从 MaterialUnit 获取
            BaseVersion = localVersion,
            ClientRequestId = clientRequestId.ToString("D")
        };
    }
}

/// <summary>
///     物料单位上行同步 DTO
/// </summary>
public record UpsertMaterialUnitDto
{
    [JsonPropertyName("unitId")]
    public int UnitId { get; init; }

    [JsonPropertyName("unitName")]
    public string UnitName { get; init; } = string.Empty;

    [JsonPropertyName("rate")]
    public decimal? Rate { get; init; }

    [JsonPropertyName("unitCalculationType")]
    public int? UnitCalculationType { get; init; }

    [JsonPropertyName("providerId")]
    public int? ProviderId { get; init; }
}

/// <summary>
///     供应商上行同步 DTO
/// </summary>
public record UpsertMaterialProviderDto
{
    /// <summary>
    ///     操作类型（Create/Update/Delete）
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = "Update";

    /// <summary>
    ///     供应商ID
    /// </summary>
    [JsonPropertyName("providerId")]
    public int? ProviderId { get; init; }

    /// <summary>
    ///     供应商名称
    /// </summary>
    [JsonPropertyName("providerName")]
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    ///     联系人姓名
    /// </summary>
    [JsonPropertyName("contectName")]
    public string ContectName { get; init; } = string.Empty;

    /// <summary>
    ///     联系人电话
    /// </summary>
    [JsonPropertyName("contectPhone")]
    public string ContectPhone { get; init; } = string.Empty;

    /// <summary>
    ///     统一社会信用代码
    /// </summary>
    [JsonPropertyName("usciCode")]
    public string UsciCode { get; init; } = string.Empty;

    /// <summary>
    ///     公司ID
    /// </summary>
    [JsonPropertyName("coId")]
    public int CoId { get; init; }

    /// <summary>
    ///     物料类型ID
    /// </summary>
    [JsonPropertyName("materialTypeId")]
    public int? MaterialTypeId { get; init; }

    /// <summary>
    ///     基础版本（用于乐观并发控制）
    /// </summary>
    [JsonPropertyName("baseVersion")]
    public long? BaseVersion { get; init; }

    /// <summary>
    ///     客户端请求ID（幂等键）
    /// </summary>
    [JsonPropertyName("clientRequestId")]
    public string ClientRequestId { get; init; } = string.Empty;

    /// <summary>
    ///     从 Provider 实体创建 DTO
    /// </summary>
    public static UpsertMaterialProviderDto FromEntity(Provider provider, long localVersion, Guid clientRequestId, string action = "Update")
    {
        return new UpsertMaterialProviderDto
        {
            Action = action,
            ProviderId = provider.Id,
            ProviderName = provider.ProviderName,
            ContectName = provider.ContectName ?? string.Empty,
            ContectPhone = provider.ContectPhone ?? string.Empty,
            UsciCode = string.Empty, // Provider 实体中没有此字段
            CoId = provider.CoId ?? 0,
            MaterialTypeId = provider.MaterialTypeId,
            BaseVersion = localVersion,
            ClientRequestId = clientRequestId.ToString("D")
        };
    }
}

/// <summary>
///     批量上行同步请求 DTO
/// </summary>
public record UpsertBatchRequestDto<T>
{
    /// <summary>
    ///     批量项列表（最多100条）
    /// </summary>
    [JsonPropertyName("items")]
    public List<T> Items { get; init; } = new();
}

/// <summary>
///     上行同步结果 DTO
/// </summary>
public record UpsertResultDto
{
    /// <summary>
    ///     状态（applied/conflict/invalid/deleted）
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    ///     实体ID
    /// </summary>
    [JsonPropertyName("entityId")]
    public int? EntityId { get; init; }

    /// <summary>
    ///     当前版本
    /// </summary>
    [JsonPropertyName("version")]
    public long? Version { get; init; }

    /// <summary>
    ///     服务端版本（冲突时）
    /// </summary>
    [JsonPropertyName("serverVersion")]
    public long? ServerVersion { get; init; }

    /// <summary>
    ///     服务端数据（冲突时）
    /// </summary>
    [JsonPropertyName("serverData")]
    public object? ServerData { get; init; }

    /// <summary>
    ///     冲突字段（冲突时）
    /// </summary>
    [JsonPropertyName("conflictFields")]
    public List<string>? ConflictFields { get; init; }

    /// <summary>
    ///     验证错误（无效时）
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public List<string>? ValidationErrors { get; init; }

    /// <summary>
    ///     消息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
///     同步变更日志项 DTO
/// </summary>
public record SyncChangeItemDto
{
    /// <summary>
    ///     变更ID
    /// </summary>
    [JsonPropertyName("changeId")]
    public long ChangeId { get; init; }

    /// <summary>
    ///     实体类型（Material/Provider）
    /// </summary>
    [JsonPropertyName("entityType")]
    public string EntityType { get; init; } = string.Empty;

    /// <summary>
    ///     实体ID
    /// </summary>
    [JsonPropertyName("entityId")]
    public int EntityId { get; init; }

    /// <summary>
    ///     操作类型（Create/Update/Delete）
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    /// <summary>
    ///     版本
    /// </summary>
    [JsonPropertyName("version")]
    public long Version { get; init; }

    /// <summary>
    ///     变更时间（UTC）
    /// </summary>
    [JsonPropertyName("changedAtUtc")]
    public DateTime ChangedAtUtc { get; init; }

    /// <summary>
    ///     变更载荷
    /// </summary>
    [JsonPropertyName("payload")]
    public object? Payload { get; init; }
}

/// <summary>
///     同步变更查询参数 DTO
/// </summary>
public record SyncChangesQueryDto
{
    /// <summary>
    ///     起始变更ID
    /// </summary>
    [JsonPropertyName("sinceChangeId")]
    public long? SinceChangeId { get; init; }

    /// <summary>
    ///     实体类型
    /// </summary>
    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    /// <summary>
    ///     限制数量
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }
}
