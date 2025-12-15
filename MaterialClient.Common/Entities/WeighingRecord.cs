using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Providers;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 称重记录实体
/// </summary>
public class WeighingRecord : FullAuditedEntity<long>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected WeighingRecord()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
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
    /// 构造函数（用于指定Id）
    /// </summary>
    public WeighingRecord(long id, decimal totalWeight)
        : base(id)
    {
        TotalWeight = totalWeight;
    }

    public void Update(string? plateNumber, int? providerId)
    {
        PlateNumber = plateNumber;
        ProviderId = providerId;
    }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }


    /// <summary>
    /// 供应商Id
    /// </summary>
    public int? ProviderId { get; set; }


    /// <summary>
    /// 显示收发料模式
    /// </summary>
    public DeliveryType? DeliveryType { get; set; }


    /// <summary>
    /// 和匹配的Id
    /// </summary>
    public long? MatchedId { get; set; }


    /// <summary>
    /// 匹配类型
    /// </summary>
    public WeighingRecordMatchType? MatchedType { get; set; }

    /// <summary>
    /// 物料列表的 JSON 存储字段
    /// </summary>
    public string? MaterialsJson { get; set; }

    /// <summary>
    /// 物料集合（从 JSON 反序列化）
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
        set
        {
            MaterialsJson = value == null || value.Count == 0 
                ? null 
                : JsonSerializer.Serialize(value);
        }
    }

    /// <summary>
    /// 添加物料
    /// </summary>
    public void AddMaterial(WeighingRecordMaterial material)
    {
        var materials = Materials;
        materials.Add(material);
        Materials = materials;
    }

    /// <summary>
    /// 清空并设置物料
    /// </summary>
    public void SetMaterials(IEnumerable<WeighingRecordMaterial> materials)
    {
        Materials = materials.ToList();
    }

    /// <summary>
    /// 验证车牌号是否为有效的中国车牌号
    /// </summary>
    /// <returns>如果车牌号有效返回true，否则返回false</returns>
    public bool IsValidChinesePlateNumber()
    {
        return PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber);
    }

    /// <summary>
    /// 验证指定的车牌号是否为有效的中国车牌号（静态方法）
    /// </summary>
    /// <param name="plateNumber">要验证的车牌号</param>
    /// <returns>如果车牌号有效返回true，否则返回false</returns>
    public static bool IsValidChinesePlateNumber(string? plateNumber)
    {
        return PlateNumberValidator.IsValidChinesePlateNumber(plateNumber);
    }


    public void MatchAsJoin(long matchedId)
    {
        MatchedId = matchedId;
        MatchedType = WeighingRecordMatchType.Join;
    }

    public void MatchAsOut(long matchedId)
    {
        MatchedId = matchedId;
        MatchedType = WeighingRecordMatchType.Out;
    }
}
