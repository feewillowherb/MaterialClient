using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Utils;

namespace MaterialClient.Common.Models;

/// <summary>
///     物料计算 Record（自包含计算逻辑的不可变值对象）
///     统一 UI 和领域模型的计算方式
/// </summary>
public record MaterialCalculation
{
    #region 构造函数（执行计算）

    public MaterialCalculation(
        decimal? planQuantity,
        decimal? actualWeight,
        decimal? unitRate,
        decimal? lowerLimit = null,
        decimal? upperLimit = null)
    {
        // 保存输入参数
        PlanQuantity = planQuantity;
        ActualWeight = actualWeight;
        UnitRate = unitRate;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;

        // 验证必要参数
        IsValid = MaterialMath.IsValidCalculation(planQuantity, actualWeight, unitRate);
        if (!IsValid)
        {
            OffsetResult = OffsetResultType.Default;
            return;
        }

        // 计划重量 = 计划件数 × 换算率
        PlanWeight = MaterialMath.CalculatePlanWeight(planQuantity, unitRate);

        // 实际件数 = 实际重量 / 换算率
        ActualQuantity = MaterialMath.CalculateActualQuantity(actualWeight, unitRate);

        // 差值 = 实际重量 - 计划重量
        Difference = MaterialMath.CalculateDifference(actualWeight, PlanWeight);

        // 偏差数量 = 实际件数 - 计划件数
        DifferenceCount = ActualQuantity!.Value - PlanQuantity!.Value;

        // 偏差率计算
        DeviationRate = MaterialMath.CalculateDeviationRate(Difference, PlanWeight);
        OffsetResult = MaterialMath.DetermineOffsetResult(DeviationRate, lowerLimit, upperLimit);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    ///     获取偏差结果的显示文本
    /// </summary>
    public string OffsetResultDisplay => OffsetResult switch
    {
        OffsetResultType.OverNegativeDeviation => "超负差",
        OffsetResultType.OverPositiveDeviation => "超正差",
        OffsetResultType.Normal => "正常",
        _ => "-"
    };

    #endregion

    #region 输入参数

    /// <summary>
    ///     计划件数
    /// </summary>
    public decimal? PlanQuantity { get; }

    /// <summary>
    ///     实际重量（货物重量）
    /// </summary>
    public decimal? ActualWeight { get; }

    /// <summary>
    ///     单位换算率
    /// </summary>
    public decimal? UnitRate { get; }

    /// <summary>
    ///     下限阈值
    /// </summary>
    public decimal? LowerLimit { get; }

    /// <summary>
    ///     上限阈值
    /// </summary>
    public decimal? UpperLimit { get; }

    #endregion

    #region 计算结果

    /// <summary>
    ///     计划重量 = 计划件数 × 换算率
    /// </summary>
    public decimal? PlanWeight { get; }

    /// <summary>
    ///     实际件数 = 实际重量 / 换算率
    /// </summary>
    public decimal? ActualQuantity { get; }

    /// <summary>
    ///     差值 = 实际重量 - 计划重量
    /// </summary>
    public decimal? Difference { get; }

    /// <summary>
    ///     偏差率 = 差值 / 计划重量 × 100
    /// </summary>
    public decimal? DeviationRate { get; }

    /// <summary>
    ///     偏差结果
    /// </summary>
    public OffsetResultType OffsetResult { get; }


    /// <summary>
    ///     偏差数量
    /// </summary>
    public decimal DifferenceCount { get; set; }

    /// <summary>
    ///     计算是否有效
    /// </summary>
    public bool IsValid { get; }

    #endregion
}