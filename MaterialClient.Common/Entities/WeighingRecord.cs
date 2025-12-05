using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;

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

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public WeighingRecord(long id, decimal weight)
        : base(id)
    {
        Weight = weight;
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
    /// 和匹配的Id
    /// </summary>
    public long? MatchedId { get; set; }


    /// <summary>
    /// 匹配类型
    /// </summary>
    public WeighingRecordMatchType? MatchedType { get; set; }
}