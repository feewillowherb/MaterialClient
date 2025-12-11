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
    public WeighingRecord(decimal weight)
    {
        Weight = weight;
    }

    public WeighingRecord(decimal weight, string? plateNumber)
    {
        Weight = weight;
        PlateNumber = plateNumber;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public WeighingRecord(long id, decimal weight)
        : base(id)
    {
        Weight = weight;
    }

    public void Update(string? plateNumber, int? providerId, int? materialId, decimal? waybillQuantity)
    {
        PlateNumber = plateNumber;
        ProviderId = providerId;
        MaterialId = materialId;
        WaybillQuantity = waybillQuantity;
    }

    /// <summary>
    /// 重量
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }


    /// <summary>
    /// 供应商Id
    /// </summary>
    public int? ProviderId { get; set; }


    /// <summary>
    /// 材料Id
    /// </summary>
    public int? MaterialId { get; set; }


    /// <summary>
    /// 单位Id
    /// </summary>
    public int? MaterialUnitId { get; set; }


    /// <summary>
    /// 运单数量
    /// </summary>
    public decimal? WaybillQuantity { get; set; }


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
}