using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Utils;

/// <summary>
///     物料计算工具类（静态方法）
/// </summary>
public static class MaterialMath
{
    /// <summary>
    ///     计算计划重量 = 计划件数 × 换算率
    /// </summary>
    /// <param name="planQuantity">计划件数</param>
    /// <param name="unitRate">单位换算率</param>
    /// <returns>计划重量，如果参数无效返回 null</returns>
    public static decimal? CalculatePlanWeight(decimal? planQuantity, decimal? unitRate)
    {
        if (!planQuantity.HasValue || !unitRate.HasValue || unitRate.Value == 0)
            return null;

        return Math.Round(planQuantity.Value * unitRate.Value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     计算实际件数 = 实际重量 / 换算率
    /// </summary>
    /// <param name="actualWeight">实际重量（货物重量）</param>
    /// <param name="unitRate">单位换算率</param>
    /// <returns>实际件数，如果参数无效返回 null</returns>
    public static decimal? CalculateActualQuantity(decimal? actualWeight, decimal? unitRate)
    {
        if (!actualWeight.HasValue || !unitRate.HasValue || unitRate.Value == 0)
            return null;

        return Math.Round(actualWeight.Value / unitRate.Value, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     计算差值 = 实际重量 - 计划重量
    /// </summary>
    /// <param name="actualWeight">实际重量</param>
    /// <param name="planWeight">计划重量</param>
    /// <returns>差值，如果参数无效返回 null</returns>
    public static decimal? CalculateDifference(decimal? actualWeight, decimal? planWeight)
    {
        if (!actualWeight.HasValue || !planWeight.HasValue)
            return null;

        return actualWeight.Value - planWeight.Value;
    }

    /// <summary>
    ///     计算偏差率 = 差值 / 计划重量 × 100
    /// </summary>
    /// <param name="difference">差值</param>
    /// <param name="planWeight">计划重量</param>
    /// <returns>偏差率（百分比），如果参数无效或计划重量为0返回 null</returns>
    public static decimal? CalculateDeviationRate(decimal? difference, decimal? planWeight)
    {
        if (!difference.HasValue || !planWeight.HasValue || planWeight.Value == 0)
            return null;

        return Math.Round(difference.Value * 100 / planWeight.Value, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     确定偏差结果类型
    /// </summary>
    /// <param name="deviationRate">偏差率（百分比）</param>
    /// <param name="lowerLimit">下限阈值</param>
    /// <param name="upperLimit">上限阈值</param>
    /// <returns>偏差结果类型</returns>
    public static OffsetResultType DetermineOffsetResult(decimal? deviationRate, decimal? lowerLimit,
        decimal? upperLimit)
    {
        if (!deviationRate.HasValue)
            return OffsetResultType.Default;

        if (!lowerLimit.HasValue && !upperLimit.HasValue)
            return OffsetResultType.Default;

        var rate = deviationRate.Value;

        if (lowerLimit.HasValue && rate < lowerLimit)
            return OffsetResultType.OverNegativeDeviation;

        if (upperLimit.HasValue && rate > upperLimit)
            return OffsetResultType.OverPositiveDeviation;

        return OffsetResultType.Normal;
    }

    /// <summary>
    ///     验证计算参数是否有效
    /// </summary>
    /// <param name="planQuantity">计划件数</param>
    /// <param name="actualWeight">实际重量</param>
    /// <param name="unitRate">单位换算率</param>
    /// <returns>如果参数有效返回 true，否则返回 false</returns>
    public static bool IsValidCalculation(decimal? planQuantity, decimal? actualWeight, decimal? unitRate)
    {
        return planQuantity.HasValue
               && unitRate.HasValue
               && unitRate.Value != 0
               && actualWeight.HasValue;
    }

    /// <summary>
    ///     将重量从千克(kg)转换为吨(t)，保留两位小数
    /// </summary>
    /// <param name="weightInKg">重量（千克）</param>
    /// <returns>重量（吨），保留两位小数</returns>
    public static decimal ConvertKgToTon(decimal weightInKg)
    {
        return Math.Round(weightInKg / 1000m, 2, MidpointRounding.AwayFromZero);
    }
}