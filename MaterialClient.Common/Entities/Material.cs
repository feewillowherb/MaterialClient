using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 物料定义实体
/// </summary>
public class Material : Entity<int>
{
    /// <summary>
    /// 物料名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 品牌
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 规格尺寸
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 上限
    /// </summary>
    public decimal? UpperLimit { get; set; }

    /// <summary>
    /// 下限
    /// </summary>
    public decimal? LowerLimit { get; set; }

    /// <summary>
    /// 基本单位
    /// </summary>
    public string? BasicUnit { get; set; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 公司ID
    /// </summary>
    public int CoId { get; set; }

    /// <summary>
    /// 规格说明
    /// </summary>
    public string? Specifications { get; set; }

    /// <summary>
    /// 产品ID
    /// </summary>
    public string? ProId { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string? UnitName { get; set; }

    /// <summary>
    /// 单位换算率
    /// </summary>
    public decimal UnitRate { get; set; } = 1;
}

