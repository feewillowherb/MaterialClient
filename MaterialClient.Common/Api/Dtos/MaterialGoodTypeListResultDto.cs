namespace MaterialClient.Common.Api.Dtos;

public class MaterialGoodTypeListResultDto
{
    public int MaterialTypeId { get; set; }

    /// <summary>
    /// 描述 :物料类型名称 
    /// 空值 : true  
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// 描述 :备注 
    /// 空值 : true  
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 描述 :物料状态(0：正常 1：删除) 
    /// 空值 : true  
    /// </summary>
    public int? DeleteStatus { get; set; }

    /// <summary>
    /// 描述 :最后编辑人ID 
    /// 空值 : true  
    /// </summary>
    public int? LastEditUserId { get; set; }

    /// <summary>
    /// 描述 :最后编辑人名称 
    /// 空值 : true  
    /// </summary>
    public string? LastEditor { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : true  
    /// </summary>
    public int? CreateUserId { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : true  
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    /// 描述 :最后更新时间 
    /// 空值 : true  
    /// </summary>
    public int? UpdateTime { get; set; }

    /// <summary>
    /// 描述 :添加时间 
    /// 空值 : true  
    /// </summary>
    public int? AddTime { get; set; }

    /// <summary>
    /// 描述 :最后更新时间 
    /// 空值 : true  
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    /// 描述 :添加时间 
    /// 空值 : true  
    /// </summary>
    public DateTime? AddDate { get; set; }

    /// <summary>
    /// 描述 :项目Id 
    /// 空值 : true  
    /// </summary>
    public Guid? ProId { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : false  
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : true  
    /// </summary>
    public string? TypeCode { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : false  
    /// </summary>
    public int CoId { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : false  
    /// </summary>
    public decimal UpperLimit { get; set; }

    /// <summary>
    /// 描述 : 
    /// 空值 : false  
    /// </summary>
    public decimal LowerLimit { get; set; }
}