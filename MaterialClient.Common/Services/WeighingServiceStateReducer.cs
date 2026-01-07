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

        // 关键修复：优先处理下磅情况，确保立即响应，不阻塞
        // 当重量下降到阈值以下时，立即转换到 OffScale，不依赖其他条件
        if (action.Weight < config.MinWeightThreshold)
        {
            var offScaleStatus = state.Status switch
            {
                // 异常下磅1：WaitingForStability -> OffScale (未稳定就下磅)
                AttendedWeighingStatus.WaitingForStability => AttendedWeighingStatus.OffScale,
                // 异常下磅2：WeightStabilized -> OffScale (稳定后突然下磅，跳过WaitingForDeparture)
                AttendedWeighingStatus.WeightStabilized => AttendedWeighingStatus.OffScale,
                // 正常下磅：WaitingForDeparture -> OffScale
                AttendedWeighingStatus.WaitingForDeparture => AttendedWeighingStatus.OffScale,
                _ => state.Status // 已经是 OffScale 或其他状态，保持不变
            };

            // 如果状态转换到 OffScale，立即清空所有相关数据
            if (offScaleStatus == AttendedWeighingStatus.OffScale && 
                state.Status != AttendedWeighingStatus.OffScale)
            {
                return newState with 
                { 
                    Status = offScaleStatus,
                    Weight = 0m, // 重置重量为0，避免残留重量导致立即重新上磅
                    LastCreatedWeighingRecordId = null,
                    PlateNumberCache = new ConcurrentDictionary<string, PlateNumberCacheRecord>(),
                    Stability = new WeightStabilityInfo
                    {
                        Weight = 0m,
                        IsStable = false,
                        StableWeight = null,
                        Min = 0m,
                        Max = 0m,
                        Range = 0m
                    }
                };
            }

            // 如果状态没有变化，直接返回
            if (offScaleStatus == state.Status)
            {
                return newState;
            }

            return newState with { Status = offScaleStatus };
        }

        // 关键修复：如果已创建记录且重量大于阈值，强制使用正确的状态
        if (state.LastCreatedWeighingRecordId != null && action.Weight > config.MinWeightThreshold)
        {
            // 如果已创建记录，应该保持在 WaitingForDeparture
            if (state.Status == AttendedWeighingStatus.WeightStabilized ||
                state.Status == AttendedWeighingStatus.WaitingForDeparture ||
                state.Status == AttendedWeighingStatus.WaitingForStability) // 防止状态不同步
            {
                // 保持在 WaitingForDeparture 时，将 IsStable 设置为 false
                return newState with 
                { 
                    Status = AttendedWeighingStatus.WaitingForDeparture,
                    Stability = newState.Stability with { IsStable = false }
                };
            }
        }

        // 基于重量的状态转换（重量大于阈值的情况）
        var newStatus = state.Status switch
        {
            // 上磅：OffScale -> WaitingForStability
            AttendedWeighingStatus.OffScale when action.Weight > config.MinWeightThreshold
                => AttendedWeighingStatus.WaitingForStability,
            _ => state.Status // No state change
        };

        // 稳定性触发的状态转换
        // 上磅阶段：WaitingForStability -> WeightStabilized
        if (newStatus == AttendedWeighingStatus.WaitingForStability &&
            state.Stability.IsStable &&
            state.LastCreatedWeighingRecordId == null) // 检查是否已经称重过（null表示未称重）
        {
            // 转换到 WeightStabilized 后，将 IsStable 设置为 false
            // 因为稳定性判断已经完成，IsStable 只在 WaitingForStability 状态下有意义
            return newState with 
            { 
                Status = AttendedWeighingStatus.WeightStabilized,
                Stability = state.Stability with { IsStable = false }
            };
        }

        // 下磅阶段：WeightStabilized -> WaitingForDeparture
        if (newStatus == AttendedWeighingStatus.WeightStabilized &&
            action.Weight > config.MinWeightThreshold &&
            state.LastCreatedWeighingRecordId != null) // 已经创建了称重记录
        {
            // 转换到 WaitingForDeparture 后，将 IsStable 设置为 false
            return newState with 
            { 
                Status = AttendedWeighingStatus.WaitingForDeparture,
                Stability = newState.Stability with { IsStable = false }
            };
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
            // 转换到 WeightStabilized 后，将 IsStable 设置为 false
            // 因为稳定性判断已经完成，IsStable 只在 WaitingForStability 状态下有意义
            return newState with 
            { 
                Status = AttendedWeighingStatus.WeightStabilized,
                Stability = action.Stability with { IsStable = false }
            };
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
        // 只在车辆上磅期间缓存车牌号（OffScale 状态下不缓存）
        // 允许在 WaitingForStability、WeightStabilized、WaitingForDeparture 状态下接收车牌
        if (state.Status == AttendedWeighingStatus.OffScale)
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
            PlateNumberCache = new ConcurrentDictionary<string, PlateNumberCacheRecord>(),
            Stability = new WeightStabilityInfo
            {
                Weight = 0m,
                IsStable = false,
                StableWeight = null,
                Min = 0m,
                Max = 0m,
                Range = 0m
            }
        };
    }
}

