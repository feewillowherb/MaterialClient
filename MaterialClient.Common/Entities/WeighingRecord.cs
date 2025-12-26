using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Providers;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities;

/// <summary>
///     称重记录实体
/// </summary>
public class WeighingRecord : Entity<long>, IMaterialClientAuditedObject, IDeletionAuditedObject
{
    /// <summary>
    ///     构造函数（用于EF Core）
    /// </summary>
    protected WeighingRecord()
    {
    }

    /// <summary>
    ///     构造函数（用于自增主键）
    /// </summary>
    public WeighingRecord(decimal totalWeight)
    {
        TotalWeight = totalWeight;
    }

    public WeighingRecord(decimal totalWeight, string? plateNumber)
    {
        TotalWeight = totalWeight;
        PlateNumber = plateNumber;
    }

    /// <summary>
    ///     构造函数（用于指定Id）
    /// </summary>
    public WeighingRecord(long id, decimal totalWeight)
        : base(id)
    {
        TotalWeight = totalWeight;
    }

    /// <summary>
    ///     总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    ///     车牌号
    /// </summary>
    public string? PlateNumber { get; set; }


    /// <summary>
    ///     供应商Id
    /// </summary>
    public int? ProviderId { get; set; }


    /// <summary>
    ///     显示收发料模式
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }


    /// <summary>
    ///     匹配WeighingRecord的Id
    /// </summary>
    public long? MatchedId { get; set; }

    /// <summary>
    ///     关联的运单Id
    /// </summary>
    public long? WaybillId { get; set; }


    /// <summary>
    ///     匹配类型
    /// </summary>
    public WeighingRecordMatchType? MatchedType { get; set; }

    /// <summary>
    ///     物料列表的 JSON 存储字段
    /// </summary>
    public string? MaterialsJson { get; set; }

    /// <summary>
    ///     物料集合（从 JSON 反序列化）
    /// </summary>
    [NotMapped]
    public List<WeighingRecordMaterial> Materials
    {
        get
        {
            if (string.IsNullOrEmpty(MaterialsJson))
                return new List<WeighingRecordMaterial>();

            try
            {
                return JsonSerializer.Deserialize<List<WeighingRecordMaterial>>(MaterialsJson)
                       ?? new List<WeighingRecordMaterial>();
            }
            catch
            {
                return new List<WeighingRecordMaterial>();
            }
        }
        set =>
            MaterialsJson = value == null || value.Count == 0
                ? null
                : JsonSerializer.Serialize(value);
    }

    public void Update(string? plateNumber, int? providerId)
    {
        PlateNumber = plateNumber;
        ProviderId = providerId;
    }

    /// <summary>
    ///     添加物料
    /// </summary>
    public void AddMaterial(WeighingRecordMaterial material)
    {
        var materials = Materials;
        materials.Add(material);
        Materials = materials;
    }

    /// <summary>
    ///     清空并设置物料
    /// </summary>
    public void SetMaterials(IEnumerable<WeighingRecordMaterial> materials)
    {
        Materials = materials.ToList();
    }

    /// <summary>
    ///     验证车牌号是否为有效的中国车牌号
    /// </summary>
    /// <returns>如果车牌号有效返回true，否则返回false</returns>
    public bool IsValidChinesePlateNumber()
    {
        return PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber);
    }

    /// <summary>
    ///     验证指定的车牌号是否为有效的中国车牌号（静态方法）
    /// </summary>
    /// <param name="plateNumber">要验证的车牌号</param>
    /// <returns>如果车牌号有效返回true，否则返回false</returns>
    public static bool IsValidChinesePlateNumber(string? plateNumber)
    {
        return PlateNumberValidator.IsValidChinesePlateNumber(plateNumber);
    }


    public void MatchAsJoin(long matchedId, long waybillId)
    {
        WaybillId = waybillId;
        MatchedId = matchedId;
        MatchedType = WeighingRecordMatchType.Join;
    }

    public void MatchAsOut(long matchedId, long waybillId)
    {
        WaybillId = waybillId;
        MatchedId = matchedId;
        MatchedType = WeighingRecordMatchType.Out;
    }

    /// <summary>
    ///     判断两个记录是否可以配对，并返回 join/out 分配结果
    /// </summary>
    /// <param name="record1">记录1</param>
    /// <param name="record2">记录2</param>
    /// <param name="deliveryType">收发料类型</param>
    /// <param name="maxIntervalMinutes">最大时间间隔（分钟），默认300</param>
    /// <param name="minWeightDiff">最小重量差（吨），默认1</param>
    /// <returns>匹配结果，包含是否匹配成功以及 join/out 记录</returns>
    public static WeighingMatchResult TryMatch(
        WeighingRecord record1,
        WeighingRecord record2,
        DeliveryType deliveryType,
        int maxIntervalMinutes = 300,
        decimal minWeightDiff = 1m)
    {
        // 验证时间差
        var timeDiff = Math.Abs((record1.AddDate!.Value - record2.AddDate!.Value).TotalMinutes);
        if (timeDiff > maxIntervalMinutes)
            return new WeighingMatchResult(false, null, null);

        // 验证重量差
        var weightDiff = Math.Abs(record1.TotalWeight - record2.TotalWeight);
        if (weightDiff <= minWeightDiff)
            return new WeighingMatchResult(false, null, null);

        // 验证 DeliveryType（双方都需要匹配或为 null）
        if (record1.DeliveryType != null && record1.DeliveryType != deliveryType)
            return new WeighingMatchResult(false, null, null);
        if (record2.DeliveryType != null && record2.DeliveryType != deliveryType)
            return new WeighingMatchResult(false, null, null);

        // 收料：毛重记录是 Join（先进场），皮重记录是 Out（后出场）
        // 发料：皮重记录是 Join（先进场），毛重记录是 Out（后出场）
        var grossRecord = record1.TotalWeight > record2.TotalWeight ? record1 : record2;
        var tareRecord = record1.TotalWeight > record2.TotalWeight ? record2 : record1;

        if (deliveryType == Enums.DeliveryType.Receiving)
            return new WeighingMatchResult(true, grossRecord, tareRecord);
        // Sending
        return new WeighingMatchResult(true, tareRecord, grossRecord);
    }

    #region Audited Properties

    public int? LastEditUserId { get; set; }
    public string? LastEditor { get; set; }
    public int? CreateUserId { get; set; }
    public string? Creator { get; set; }
    public int? UpdateTime { get; set; }
    public int? AddTime { get; set; }
    public DateTime? UpdateDate { get; set; }
    public DateTime? AddDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }

    #endregion
}

/// <summary>
///     称重记录匹配结果
/// </summary>
/// <param name="IsMatch">是否匹配成功</param>
/// <param name="JoinRecord">进场记录</param>
/// <param name="OutRecord">出场记录</param>
public record WeighingMatchResult(
    bool IsMatch,
    WeighingRecord? JoinRecord,
    WeighingRecord? OutRecord
);