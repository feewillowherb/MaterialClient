using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Models;

/// <summary>
/// 物料计算 Record（自包含计算逻辑的不可变值对象）
/// 统一 UI 和领域模型的计算方式
/// </summary>
public record MaterialCalculation
{
    #region 输入参数

    /// <summary>
    /// 计划件数
    /// </summary>
    public decimal? PlanQuantity { get; }

    /// <summary>
    /// 实际重量（货物重量）
    /// </summary>
    public decimal? ActualWeight { get; }

    /// <summary>
    /// 单位换算率
    /// </summary>
    public decimal? UnitRate { get; }

    /// <summary>
    /// 下限阈值
    /// </summary>
    public decimal? LowerLimit { get; }

    /// <summary>
    /// 上限阈值
    /// </summary>
    public decimal? UpperLimit { get; }

    #endregion

    #region 计算结果

    /// <summary>
    /// 计划重量 = 计划件数 × 换算率
    /// </summary>
    public decimal? PlanWeight { get; }

    /// <summary>
    /// 实际件数 = 实际重量 / 换算率
    /// </summary>
    public decimal? ActualQuantity { get; }

    /// <summary>
    /// 差值 = 实际重量 - 计划重量
    /// </summary>
    public decimal? Difference { get; }

    /// <summary>
    /// 偏差率 = 差值 / 计划重量 × 100
    /// </summary>
    public decimal? DeviationRate { get; }

    /// <summary>
    /// 偏差结果
    /// </summary>
    public OffsetResultType OffsetResult { get; }

    /// <summary>
    /// 计算是否有效
    /// </summary>
    public bool IsValid { get; }

    #endregion

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
        if (!planQuantity.HasValue || !unitRate.HasValue || unitRate.Value == 0 || !actualWeight.HasValue)
        {
            IsValid = false;
            OffsetResult = OffsetResultType.Default;
            return;
        }

        IsValid = true;

        // 计划重量 = 计划件数 × 换算率
        PlanWeight = Math.Round(planQuantity.Value * unitRate.Value, 2, MidpointRounding.AwayFromZero);

        // 实际件数 = 实际重量 / 换算率
        ActualQuantity = Math.Round(actualWeight.Value / unitRate.Value, 4, MidpointRounding.AwayFromZero);

        // 差值 = 实际重量 - 计划重量
        Difference = actualWeight.Value - PlanWeight.Value;

        // 偏差率计算
        if (PlanWeight.Value != 0)
        {
            DeviationRate = Math.Round(Difference.Value * 100 / PlanWeight.Value, 4, MidpointRounding.AwayFromZero);
            OffsetResult = DetermineOffsetResult(DeviationRate.Value);
        }
        else
        {
            OffsetResult = OffsetResultType.Default;
        }
    }

    #endregion

    #region 辅助方法

    private OffsetResultType DetermineOffsetResult(decimal deviationRate)
    {
        if (!LowerLimit.HasValue && !UpperLimit.HasValue)
            return OffsetResultType.Default;

        if (LowerLimit.HasValue && LowerLimit < 0 && deviationRate < 0 && deviationRate < LowerLimit)
            return OffsetResultType.OverNegativeDeviation;

        if (UpperLimit.HasValue && UpperLimit > 0 && deviationRate > 0 && deviationRate > UpperLimit)
            return OffsetResultType.OverPositiveDeviation;

        return OffsetResultType.Normal;
    }

    /// <summary>
    /// 获取偏差结果的显示文本
    /// </summary>
    public string OffsetResultDisplay => OffsetResult switch
    {
        OffsetResultType.OverNegativeDeviation => "超负差",
        OffsetResultType.OverPositiveDeviation => "超正差",
        OffsetResultType.Normal => "正常",
        _ => "-"
    };

    #endregion
}

