using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 订单同步请求参数模型
/// </summary>
public class SynchronizationOrderInputDto
{
    /// <summary>
    /// Desc:项目Id
    /// Default:
    /// Nullable:True
    /// </summary>   
    [Required(ErrorMessage = "项目ID必填")]
    public string ProId { get; set; } = null!;

    /// <summary>
    /// Desc:供应商ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? ProviderId { get; set; }

    /// <summary>
    /// Desc:台账ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public long? OrderId { get; set; }

    /// <summary>
    /// Desc:台账ID字符串
    /// Default:
    /// Nullable:True
    /// </summary> 
    [Required(ErrorMessage = "台账ID必填")]
    public string? StrOrderId { get; set; }

    /// <summary>
    /// Desc:收料单号
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? OrderNo { get; set; }

    /// <summary>
    /// Desc:类型(0:收货中  1:已收货 2:已取消)
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? OrderType { get; set; }

    /// <summary>
    /// Desc:交货类型[0.收料 1.发料]
    /// Default:0
    /// Nullable:True
    /// </summary>           
    public int? DeliveryType { get; set; }

    /// <summary>
    /// Desc:车牌号码
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? TruckNo { get; set; }

    /// <summary>
    /// Desc:发货单ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? DispatchNo { get; set; }

    /// <summary>
    /// Desc:计划重量，发货单上物料的总重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderPlanOnWeight { get; set; }

    /// <summary>
    /// Desc:计划数量，发货单上总数量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderPlanOnPcs { get; set; }

    /// <summary>
    /// Desc:进场时间
    /// Default:
    /// Nullable:True
    /// </summary>           
    public DateTime? JoinTime { get; set; }

    /// <summary>
    /// Desc:出场时间
    /// Default:
    /// Nullable:True
    /// </summary>           
    public DateTime? OutTime { get; set; }

    /// <summary>
    /// Desc:备注
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? Remark { get; set; }

    /// <summary>
    /// Desc:毛重，物料加卡车总实际称重重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderTotalWeight { get; set; }

    /// <summary>
    /// Desc:皮重，卡车卸货后实际称重重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderTruckWeight { get; set; }

    /// <summary>
    /// Desc:净重，物料的实际称重重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderGoodsWeight { get; set; }

    /// <summary>
    /// Desc:实际总数量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? OrderPcs { get; set; }

    /// <summary>
    /// Desc:状态(0:正常  1:删除)
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? DeleteStatus { get; set; }

    /// <summary>
    /// Desc:最后编辑人ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? LastEditUserId { get; set; }

    /// <summary>
    /// Desc:最后编辑人名称
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? LastEditor { get; set; }

    /// <summary>
    /// Desc:创建人编号
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? CreateUserId { get; set; }

    /// <summary>
    /// Desc:创建人
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
    /// Desc:收货方Id(发货时的收货方Id)
    /// Default:DateTime.Now
    /// Nullable:True
    /// </summary>           
    public int ReceivederId { get; set; }

    /// <summary>
    /// 是否预警 0否  1是
    /// </summary>
    public short? EarlyWarnStatus { get; set; } = 0;

    /// <summary>
    /// 打印次数
    /// </summary>
    public short? PrintCount { get; set; } = 0;

    /// <summary>
    /// 
    /// </summary>
    public string? AbortReason { get; set; }

    /// <summary>
    /// 重量偏差结果：  1超正差  2正常 3超负差
    /// </summary>
    public short? OffsetResult { get; set; } = 0;

    /// <summary>
    /// 超负差 =1,车辆进出异常 =2
    /// </summary>
    public string? EarlyWarnType { get; set; }

    /// <summary>
    /// 来源：1 称重 2补录 3移动验收 4 无人值守
    /// </summary>
    public short? OrderSource { get; set; }

    /// <summary>
    /// 订单和物料关系集合
    /// </summary>
    public List<OrderGoodsDto> OrderGoodList { get; set; } = new List<OrderGoodsDto>();

    public string? TruckNum { get; set; }

    public bool? IsConfirm { get; set; }
}

/// <summary>
/// 订单和物资关系数据
/// </summary>
public class OrderGoodsDto
{
    /// <summary>
    /// Desc:物料ID
    /// Default:
    /// Nullable:False
    /// </summary>           
    public int GoodsId { get; set; }

    /// <summary>
    /// Desc:物料单位ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? UnitId { get; set; }

    /// <summary>
    /// Desc:计划重量，运单上的重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? GoodsPlanOnWeight { get; set; }

    /// <summary>
    /// Desc:计划数量，运单上的数量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? GoodsPlanOnPcs { get; set; }

    /// <summary>
    /// Desc:进出场数量，实际数量(手动输入)
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? GoodsPcs { get; set; }

    /// <summary>
    /// Desc:进出场重量，扣量后的重量(单位的转换率乘以数量)
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? GoodsWeight { get; set; }

    /// <summary>
    /// Desc:扣量重量
    /// Default:
    /// Nullable:True
    /// </summary>           
    public decimal? GoodsTakeWeight { get; set; }

    /// <summary>
    /// Desc:状态(0:正常  1:删除)
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? DeleteStatus { get; set; }

    /// <summary>
    /// Desc:最后编辑人ID
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? LastEditUserId { get; set; }

    /// <summary>
    /// Desc:最后编辑人名称
    /// Default:
    /// Nullable:True
    /// </summary>           
    public string? LastEditor { get; set; }

    /// <summary>
    /// Desc:创建人编号
    /// Default:
    /// Nullable:True
    /// </summary>           
    public int? CreateUserId { get; set; }

    /// <summary>
    /// Desc:创建人
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
    /// 重量偏差结果：  1超正差  2正常 3超负差
    /// </summary>
    public short? OffsetResult { get; set; } = 0;

    /// <summary>
    /// 偏差重量
    /// </summary>
    public decimal? OffsetWeight { get; set; } = 0;

    /// <summary>
    /// 偏差数量
    /// </summary>
    public decimal? OffsetCount { get; set; } = 0;

    /// <summary>
    /// 偏差百分比，负数表示比预期的少
    /// </summary>
    public decimal? OffsetRate { get; set; } = 0;
}