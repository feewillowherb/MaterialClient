using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services;

/// <summary>
///     状态转换器（纯函数）
/// </summary>
internal static class WeighingServiceStateReducer
{
    /// <summary>
    ///     状态转换主函数
    /// </summary>
    public static WeighingServiceState ReduceState(
        WeighingServiceState currentState,
        StateAction action)
    {
        return action switch
        {
            WeightUpdatedAction weightAction => ReduceWeightUpdate(currentState, weightAction),
            StabilityUpdatedAction stabilityAction => ReduceStabilityUpdate(currentState, stabilityAction),
            SetDeliveryTypeAction deliveryTypeAction => currentState with { DeliveryType = deliveryTypeAction.DeliveryType },
            PlateNumberRecognizedAction plateAction => ReducePlateNumberRecognized(currentState, plateAction),
            WeighingRecordCreatedAction recordAction => currentState with { LastCreatedWeighingRecordId = recordAction.RecordId },
            ResetWeighingCycleAction => ReduceResetCycle(currentState),
            ConfigurationUpdatedAction configAction => currentState with { Config = configAction.Config },
            _ => currentState
        };
    }

    /// <summary>
    ///     处理重量更新的状态转换
    /// </summary>
    private static WeighingServiceState ReduceWeightUpdate(
        WeighingServiceState state,
        WeightUpdatedAction action)
    {
        var config = state.Config;
        var newState = state with { Weight = action.Weight };

        // 关键修复：如果已创建记录，强制使用正确的状态
        if (state.LastCreatedWeighingRecordId != null && action.Weight > config.MinWeightThreshold)
        {
            // 如果已创建记录，应该保持在 WaitingForDeparture
            if (state.Status == AttendedWeighingStatus.WeightStabilized ||
                state.Status == AttendedWeighingStatus.WaitingForDeparture ||
                state.Status == AttendedWeighingStatus.WaitingForStability) // 防止状态不同步
            {
                return newState with { Status = AttendedWeighingStatus.WaitingForDeparture };
            }
        }

        // 基于重量的状态转换
        var newStatus = state.Status switch
        {
            // 上磅：OffScale -> WaitingForStability
            AttendedWeighingStatus.OffScale when action.Weight > config.MinWeightThreshold
                => AttendedWeighingStatus.WaitingForStability,
            // 异常下磅1：WaitingForStability -> OffScale (未稳定就下磅)
            AttendedWeighingStatus.WaitingForStability when action.Weight < config.MinWeightThreshold
                => AttendedWeighingStatus.OffScale,
            // 异常下磅2：WeightStabilized -> OffScale (稳定后突然下磅，跳过WaitingForDeparture)
            AttendedWeighingStatus.WeightStabilized when action.Weight < config.MinWeightThreshold
                => AttendedWeighingStatus.OffScale,
            // 正常下磅：WaitingForDeparture -> OffScale
            AttendedWeighingStatus.WaitingForDeparture when action.Weight < config.MinWeightThreshold
                => AttendedWeighingStatus.OffScale,
            _ => state.Status // No state change
        };

        // 稳定性触发的状态转换
        // 上磅阶段：WaitingForStability -> WeightStabilized
        if (newStatus == AttendedWeighingStatus.WaitingForStability &&
            state.Stability.IsStable &&
            state.LastCreatedWeighingRecordId == null) // 检查是否已经称重过（null表示未称重）
        {
            newStatus = AttendedWeighingStatus.WeightStabilized;
        }

        // 下磅阶段：WeightStabilized -> WaitingForDeparture
        if (newStatus == AttendedWeighingStatus.WeightStabilized &&
            action.Weight > config.MinWeightThreshold &&
            state.LastCreatedWeighingRecordId != null) // 已经创建了称重记录
        {
            newStatus = AttendedWeighingStatus.WaitingForDeparture;
        }

        return newState with { Status = newStatus };
    }

    /// <summary>
    ///     处理稳定性更新的状态转换
    /// </summary>
    private static WeighingServiceState ReduceStabilityUpdate(
        WeighingServiceState state,
        StabilityUpdatedAction action)
    {
        var newState = state with { Stability = action.Stability };

        // 稳定性触发的状态转换
        // 上磅阶段：WaitingForStability -> WeightStabilized
        if (state.Status == AttendedWeighingStatus.WaitingForStability &&
            action.Stability.IsStable &&
            state.LastCreatedWeighingRecordId == null) // 检查是否已经称重过（null表示未称重）
        {
            return newState with { Status = AttendedWeighingStatus.WeightStabilized };
        }

        return newState;
    }

    /// <summary>
    ///     处理车牌识别的状态转换
    /// </summary>
    private static WeighingServiceState ReducePlateNumberRecognized(
        WeighingServiceState state,
        PlateNumberRecognizedAction action)
    {
        // 只在特定状态下缓存车牌号
        if (state.Status != AttendedWeighingStatus.WaitingForStability &&
            state.Status != AttendedWeighingStatus.WeightStabilized)
        {
            return state;
        }

        var cache = new ConcurrentDictionary<string, PlateNumberCacheRecord>(state.PlateNumberCache);
        cache.AddOrUpdate(
            action.PlateNumber,
            new PlateNumberCacheRecord { Count = 1, LastUpdateTime = DateTime.UtcNow },
            (key, oldValue) => new PlateNumberCacheRecord
                { Count = oldValue.Count + 1, LastUpdateTime = DateTime.UtcNow });

        return state with { PlateNumberCache = cache };
    }

    /// <summary>
    ///     处理重置称重周期的状态转换
    /// </summary>
    private static WeighingServiceState ReduceResetCycle(WeighingServiceState state)
    {
        return state with
        {
            LastCreatedWeighingRecordId = null,
            PlateNumberCache = new ConcurrentDictionary<string, PlateNumberCacheRecord>()
        };
    }
}

