using System.Reactive.Linq;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     称重流管道接口
/// </summary>
public interface IWeighingStreamPipeline
{
    /// <summary>
    ///     构建称重流管道，返回组合后的状态流
    /// </summary>
    IObservable<AttendedWeighingStatus> Build(
        IObservable<decimal> sharedSource,
        WeighingConfiguration config,
        WeighingStateManager stateManager);
}

/// <summary>
///     称重流管道
///     构建和管理响应式重量、稳定性和状态 Rx 流
/// </summary>
public class WeighingStreamPipeline : IWeighingStreamPipeline, ISingletonDependency
{
    private readonly ILogger<WeighingStreamPipeline> _logger;

    public WeighingStreamPipeline(ILogger<WeighingStreamPipeline> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IObservable<AttendedWeighingStatus> Build(
        IObservable<decimal> sharedSource,
        WeighingConfiguration config,
        WeighingStateManager stateManager)
    {
        var weightStream = CreateWeightStream(sharedSource, config);
        var stabilityStream = CreateStabilityStream(sharedSource, config);
        return CreateStatusStream(weightStream, stabilityStream, config, stateManager);
    }

    /// <summary>
    ///     创建重量流（更频繁，用于状态转换）
    /// </summary>
    internal IObservable<decimal> CreateWeightStream(IObservable<decimal> sharedWeightSource,
        WeighingConfiguration config)
    {
        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Where(buffer => buffer.Count > 0)
            .Select(buffer => buffer.Last())
            .StartWith(0m);
    }

    /// <summary>
    ///     创建稳定性流（较慢，用于稳定性检查）
    /// </summary>
    internal IObservable<WeightStabilityInfo> CreateStabilityStream(IObservable<decimal> sharedWeightSource,
        WeighingConfiguration config)
    {
        var minDataPointsRequired =
            Math.Max(8, (int)(config.StabilityWindowMs / config.StabilityCheckIntervalMs * 0.5));

        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
                TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Select(buffer =>
            {
                if (buffer.Count > 0)
                {
                    var validDataPoints = buffer.Where(w => w > config.MinWeightThreshold).ToList();

                    if (validDataPoints.Count == 0)
                    {
                        return new WeightStabilityInfo
                        {
                            Weight = 0m,
                            IsStable = false,
                            StableWeight = null,
                            Min = 0,
                            Max = 0,
                            Range = 0
                        };
                    }

                    var min = validDataPoints.Min();
                    var max = validDataPoints.Max();
                    var range = max - min;

                    var rangeStable = range <= config.WeightStabilityThreshold * 2;
                    var hasEnoughDataPoints = validDataPoints.Count >= minDataPointsRequired;
                    var isStable = rangeStable && hasEnoughDataPoints;
                    var stableWeight = isStable ? (min + max) / 2 : (decimal?)null;

                    _logger.LogDebug(
                        "Weight stability: {IsStable} (range: {Range:F3} kg, min: {Min:F3}, max: {Max:F3}, " +
                        "stableWeight: {StableWeight:F3}, validDataPoints: {ValidCount}/{MinRequired} (total: {Total}), " +
                        "rangeStable: {RangeStable}, hasEnoughData: {HasEnough})",
                        isStable, range, min, max, stableWeight, validDataPoints.Count, minDataPointsRequired,
                        buffer.Count, rangeStable, hasEnoughDataPoints);

                    return new WeightStabilityInfo
                    {
                        Weight = 0m,
                        IsStable = isStable,
                        StableWeight = stableWeight,
                        Min = min,
                        Max = max,
                        Range = range
                    };
                }

                return new WeightStabilityInfo
                {
                    Weight = 0m,
                    IsStable = false,
                    StableWeight = null,
                    Min = 0m,
                    Max = 0m,
                    Range = 0m
                };
            })
            .StartWith(new WeightStabilityInfo
            {
                Weight = 0m,
                IsStable = false,
                StableWeight = null,
                Min = 0m,
                Max = 0m,
                Range = 0m
            })
            .DistinctUntilChanged(info => info.IsStable)
            .Replay(1)
            .RefCount();
    }

    /// <summary>
    ///     创建状态流
    /// </summary>
    internal IObservable<AttendedWeighingStatus> CreateStatusStream(
        IObservable<decimal> weightStream,
        IObservable<WeightStabilityInfo> stabilityStream,
        WeighingConfiguration config,
        WeighingStateManager stateManager)
    {
        var recordIdStream = stateManager.RecordIdSubject
            .DistinctUntilChanged();

        return stateManager.StatusSubject
            .CombineLatest(
                weightStream,
                stabilityStream,
                recordIdStream,
                (status, weight, stability, recordId) =>
                {
                    // Force WaitingForDeparture when record exists and weight is above threshold
                    if (recordId != null && weight > config.MinWeightThreshold)
                    {
                        if (status == AttendedWeighingStatus.WeightStabilized ||
                            status == AttendedWeighingStatus.WaitingForDeparture ||
                            status == AttendedWeighingStatus.WaitingForStability)
                        {
                            _logger.LogDebug(
                                "Forcing WaitingForDeparture: recordId={RecordId}, currentStatus={Status}, weight={Weight:F3}t",
                                recordId, status, weight);
                            return AttendedWeighingStatus.WaitingForDeparture;
                        }
                    }

                    // Weight-based state transitions
                    var newStatus = status switch
                    {
                        AttendedWeighingStatus.OffScale when weight > config.MinWeightThreshold
                            => AttendedWeighingStatus.WaitingForStability,
                        AttendedWeighingStatus.WaitingForStability when weight < config.MinWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        AttendedWeighingStatus.WeightStabilized when weight < config.MinWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        AttendedWeighingStatus.WaitingForDeparture when weight < config.MinWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        _ => status
                    };

                    // Stability-based: WaitingForStability → WeightStabilized
                    if (newStatus == AttendedWeighingStatus.WaitingForStability &&
                        stability.IsStable &&
                        recordId == null)
                    {
                        _logger.LogInformation(
                            "Converting WaitingForStability -> WeightStabilized: weight={Weight:F3}t, stability.IsStable={IsStable}",
                            weight, stability.IsStable);
                        return AttendedWeighingStatus.WeightStabilized;
                    }

                    // WeightStabilized → WaitingForDeparture (when record exists)
                    if (newStatus == AttendedWeighingStatus.WeightStabilized &&
                        weight > config.MinWeightThreshold &&
                        recordId != null)
                    {
                        _logger.LogInformation(
                            "Converting WeightStabilized -> WaitingForDeparture: recordId={RecordId}, weight={Weight:F3}t",
                            recordId, weight);
                        return AttendedWeighingStatus.WaitingForDeparture;
                    }

                    return newStatus;
                })
            .DistinctUntilChanged();
    }
}
