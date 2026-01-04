using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services;

/// <summary>
///     称重服务统一状态
/// </summary>
public record WeighingServiceState
{
    /// <summary>
    ///     当前称重状态
    /// </summary>
    public AttendedWeighingStatus Status { get; init; } = AttendedWeighingStatus.OffScale;

    /// <summary>
    ///     当前重量（吨）
    /// </summary>
    public decimal Weight { get; init; } = 0m;

    /// <summary>
    ///     重量稳定性信息
    /// </summary>
    public WeightStabilityInfo Stability { get; init; } = new WeightStabilityInfo
    {
        Weight = 0m,
        IsStable = false,
        StableWeight = null,
        Min = 0m,
        Max = 0m,
        Range = 0m
    };

    /// <summary>
    ///     收发料类型
    /// </summary>
    public DeliveryType DeliveryType { get; init; } = DeliveryType.Receiving;

    /// <summary>
    ///     最近创建的称重记录ID（null表示未称重）
    /// </summary>
    public long? LastCreatedWeighingRecordId { get; init; } = null;

    /// <summary>
    ///     车牌号缓存
    /// </summary>
    public ConcurrentDictionary<string, PlateNumberCacheRecord> PlateNumberCache { get; init; } = new();

    /// <summary>
    ///     配置参数
    /// </summary>
    public WeighingConfiguration Config { get; init; } = new();

    /// <summary>
    ///     初始状态
    /// </summary>
    public static WeighingServiceState Initial => new();
}

/// <summary>
///     状态转换 Action 基类
/// </summary>
public abstract record StateAction;

/// <summary>
///     重量更新 Action
/// </summary>
public record WeightUpdatedAction(decimal Weight) : StateAction;

/// <summary>
///     稳定性更新 Action
/// </summary>
public record StabilityUpdatedAction(WeightStabilityInfo Stability) : StateAction;

/// <summary>
///     设置收发料类型 Action
/// </summary>
public record SetDeliveryTypeAction(DeliveryType DeliveryType) : StateAction;

/// <summary>
///     车牌识别 Action
/// </summary>
public record PlateNumberRecognizedAction(string PlateNumber) : StateAction;

/// <summary>
///     称重记录创建 Action
/// </summary>
public record WeighingRecordCreatedAction(long? RecordId) : StateAction;

/// <summary>
///     重置称重周期 Action
/// </summary>
public record ResetWeighingCycleAction : StateAction;

/// <summary>
///     配置更新 Action
/// </summary>
public record ConfigurationUpdatedAction(WeighingConfiguration Config) : StateAction;

