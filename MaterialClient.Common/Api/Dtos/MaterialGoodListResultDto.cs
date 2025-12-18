using System;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 物料列表返回项
/// </summary>
public class MaterialGoodListResultDto
{
    /// <summary>
    /// 物料主键Id
    /// </summary>
    public int GoodsId { get; set; }

    /// <summary>
    /// 物料名称
    /// </summary>
    public string? GoodsName { get; set; }

    /// <summary>
    /// 品牌
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 大小 10*12米
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 最大阈值
    /// </summary>
    public decimal? UpperLimit { get; set; }

    /// <summary>
    /// 最小阈值
    /// </summary>
    public decimal? LowerLimit { get; set; }

    /// <summary>
    /// 单位(基础单位)
    /// </summary>
    public string? BasicUnit { get; set; }

    /// <summary>
    /// 物料类型Id
    /// </summary>
    public int? MaterialTypeId { get; set; }

    /// <summary>
    /// 物料状态(0：正常 1：删除)
    /// </summary>
    public int? DeleteStatus { get; set; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string? GoodsCode { get; set; }

    /// <summary>
    /// 公司Id
    /// </summary>
    public int? CoId { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specifications { get; set; }


    /// <summary>
    /// Desc:物料单位列表
    /// Default:DateTime.Now
    /// Nullable:True
    /// </summary>           
    public List<MaterialGoodUnitResultDto>? Units { get; set; }

    /// <summary>
    /// 项目Id
    /// </summary>
    public string? ProId { get; set; }

    /// <summary>
    /// 最后编辑人ID
    /// </summary>
    public int? LastEditUserId { get; set; }

    /// <summary>
    /// 最后编辑人名称
    /// </summary>
    public string? LastEditor { get; set; }

    /// <summary>
    /// 创建人ID
    /// </summary>
    public int? CreateUserId { get; set; }

    /// <summary>
    /// 创建人名称
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    /// 最后更新时间（时间戳）
    /// </summary>
    public int? UpdateTime { get; set; }

    /// <summary>
    /// 添加时间（时间戳）
    /// </summary>
    public int? AddTime { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    /// 添加时间
    /// </summary>
    public DateTime? AddDate { get; set; }


    /// <summary>
    /// 转为领域实体 Material
    /// </summary>
    public static Material ToEntity(MaterialGoodListResultDto dto)
    {
        var material = new Material(
            dto.GoodsId,
            dto.GoodsName ?? string.Empty,
            dto.CoId ?? 0)
        {
            Brand = dto.Brand,
            Size = dto.Size,
            UpperLimit = dto.UpperLimit,
            LowerLimit = dto.LowerLimit,
            BasicUnit = dto.BasicUnit,
            Code = dto.GoodsCode,
            Specifications = dto.Specifications,
            ProId = dto.ProId,
            IsDeleted = dto.DeleteStatus == 1,
            LastEditUserId = dto.LastEditUserId,
            LastEditor = dto.LastEditor,
            CreateUserId = dto.CreateUserId,
            Creator = dto.Creator,
            UpdateTime = dto.UpdateTime,
            AddTime = dto.AddTime,
            UpdateDate = dto.UpdateDate,
            AddDate = dto.AddDate
        };

        return material;
    }
}

public class MaterialGoodUnitResultDto
{
    /// <summary>
    /// Desc:单位Id
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int UnitId { get; set; }

    /// <summary>
    /// Desc:单位名称
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? UnitName { get; set; }

    /// <summary>
    /// Desc:单位转换率
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? Rate { get; set; }

    /// <summary>
    /// Desc:状态(0:正常  1:删除)
    /// Default:0
    /// Nullable:True
    /// </summary>           
    public int? DeleteStatus { get; set; }

    /// <summary>
    /// Desc:
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? LastEditUserId { get; set; }

    /// <summary>
    /// Desc:
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? LastEditor { get; set; }

    /// <summary>
    /// Desc:
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? CreateUserId { get; set; }

    /// <summary>
    /// Desc:
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? Creator { get; set; }

    /// <summary>
    /// Desc:最后更新时间
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? UpdateTime { get; set; }

    /// <summary>
    /// Desc:添加时间
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? AddTime { get; set; }

    /// <summary>
    /// Desc:最后更新时间
    /// Default:
    /// Nullable:True
    /// </summary>           
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    /// Desc:添加时间
    /// Default:DateTime.Now
    /// Nullable:True
    /// </summary>           
    public DateTime? AddDate { get; set; }

    /// <summary>
    /// Desc:单位计算类型(0:按重量  1:按数量)
    /// Default:0
    /// Nullable:True
    /// </summary>           
    public int? UnitCalculationType { get; set; }

    /// <summary>
    /// 供应商ID
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? RateName { get; set; }

    public static MaterialUnit ToEntity(MaterialGoodUnitResultDto dto, int materialId)
    {
        var materialUnit = new MaterialUnit(
            dto.UnitId,
            materialId,
            dto.UnitName ?? string.Empty,
            dto.Rate ?? 0)
        {
            UnitCalculationType = dto.UnitCalculationType,
            ProviderId = dto.ProviderId,
            RateName = dto.RateName,
            IsDeleted = dto.DeleteStatus == 1,
            LastEditUserId = dto.LastEditUserId,
            LastEditor = dto.LastEditor,
            CreateUserId = dto.CreateUserId,
            Creator = dto.Creator,
            UpdateTime = dto.UpdateTime,
            AddTime = dto.AddTime,
            UpdateDate = dto.UpdateDate,
            AddDate = dto.AddDate
        };

        return materialUnit;
    }
}