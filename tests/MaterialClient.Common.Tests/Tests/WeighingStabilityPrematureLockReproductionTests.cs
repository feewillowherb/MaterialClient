using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Regression for Urban 2026-10-21 premature lock (0.54~0.55t mid-ascent).
/// After fix-weighing-stability-full-window: must fill all periods before IsStable.
/// </summary>
public class WeighingStabilityPrematureLockReproductionTests
{
    private readonly ITestOutputHelper _output;

    public WeighingStabilityPrematureLockReproductionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static WeighingConfiguration CreateLogSiteConfig() => new()
    {
        MinWeightThreshold = 0.5m,
        WeightStabilityThreshold = 0.05m,
        StabilityWindowMs = 3000,
        StabilityCheckIntervalMs = 200
    };

    [Fact]
    public void LogSiteConfig_PeriodsAreFifteen_For3000Over200()
    {
        var config = CreateLogSiteConfig();
        WeightStabilityEvaluator.ResolvePeriods(config).ShouldBe(15);
    }

    [Fact]
    public void Evaluator_ShortPlateauWindow_IsNotStable_UntilFullPeriods()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);

        // 8 samples (~old minRequired) at 0.54~0.55 — must NOT be stable under full-window rules
        var shortPlateau = new[]
        {
            0.550m, 0.540m, 0.545m, 0.550m, 0.540m, 0.545m, 0.550m, 0.540m
        };
        WeightStabilityEvaluator.FromDownsampledWindow(shortPlateau, periods, config)
            .IsStable.ShouldBeFalse("Window shorter than periods must not evaluate as stable.");

        // Full 15 periods at plateau — may be stable by range
        var fullPlateau = Enumerable.Range(0, periods)
            .Select(i => i % 2 == 0 ? 0.550m : 0.540m)
            .ToList();
        var fullResult = WeightStabilityEvaluator.FromDownsampledWindow(fullPlateau, periods, config);
        fullResult.IsStable.ShouldBeTrue();
        fullResult.StableWeight.ShouldNotBeNull();
        fullResult.StableWeight!.Value.ShouldBeInRange(0.530m, 0.560m);
    }

    [Fact]
    public void Evaluator_BelowThresholdInWindow_IsNotStable()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);
        var window = Enumerable.Repeat(1.05m, periods).ToList();
        window[7] = 0.40m; // below MinWeightThreshold

        var result = WeightStabilityEvaluator.FromDownsampledWindow(window, periods, config);
        result.IsStable.ShouldBeFalse();
        result.StableWeight.ShouldBeNull();
    }

    [Fact]
    public void Evaluator_FullWindowRangeOverThreshold_IsNotStable()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);
        // range 0.20 > 0.05*2
        var window = Enumerable.Range(0, periods)
            .Select(i => i < periods / 2 ? 1.00m : 1.20m)
            .ToList();

        var result = WeightStabilityEvaluator.FromDownsampledWindow(window, periods, config);
        result.IsStable.ShouldBeFalse();
        result.StableWeight.ShouldBeNull();
    }

    [Fact]
    public void Evaluator_FullWindowAtTrueWeight_IsStable()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);
        var window = Enumerable.Range(0, periods)
            .Select(i => 1.05m + (i % 3 - 1) * 0.005m)
            .ToList();

        var result = WeightStabilityEvaluator.FromDownsampledWindow(window, periods, config);
        result.IsStable.ShouldBeTrue();
        result.StableWeight.ShouldNotBeNull();
        result.StableWeight!.Value.ShouldBeInRange(1.00m, 1.10m);
    }

    [Fact]
    public async Task CreateStabilityStream_MidAscentShortPlateau_DoesNotLockBeforeFullWindow()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);
        var pipeline = new WeighingStreamPipeline(Substitute.For<ILogger<WeighingStreamPipeline>>());
        var weightSubject = new Subject<decimal>();
        var stableSeen = false;

        using var subscription = pipeline
            .CreateStabilityStream(weightSubject.AsObservable(), config)
            .Subscribe(info =>
            {
                _output.WriteLine($"IsStable={info.IsStable}, StableWeight={info.StableWeight}");
                if (info.IsStable)
                {
                    stableSeen = true;
                }
            });

        // Feed ~800ms of 0.54~0.55 plateau (~4 downsampled periods at 200ms) — must stay unstable
        var plateauPattern = new[] { 0.550m, 0.540m, 0.545m };
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 900)
        {
            weightSubject.OnNext(plateauPattern[(int)(sw.ElapsedMilliseconds / 50) % plateauPattern.Length]);
            await Task.Delay(50);
        }

        // Wait one more interval tick
        await Task.Delay(config.StabilityCheckIntervalMs + 50);

        stableSeen.ShouldBeFalse(
            $"Must not lock mid-ascent plateau before full {periods} periods (~{config.StabilityWindowMs}ms).");

        weightSubject.OnCompleted();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task CreateStabilityStream_FullPeriodsAtTrueWeight_BecomesStable()
    {
        var config = CreateLogSiteConfig();
        var periods = WeightStabilityEvaluator.ResolvePeriods(config);
        var pipeline = new WeighingStreamPipeline(Substitute.For<ILogger<WeighingStreamPipeline>>());
        var weightSubject = new Subject<decimal>();
        var stableGate = new TaskCompletionSource<WeightStabilityInfo>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = pipeline
            .CreateStabilityStream(weightSubject.AsObservable(), config)
            .Subscribe(info =>
            {
                _output.WriteLine($"IsStable={info.IsStable}, StableWeight={info.StableWeight}");
                if (info.IsStable)
                {
                    stableGate.TrySetResult(info);
                }
            });

        // Feed slightly more than periods * interval of stable ~1.05t samples
        var feedMs = periods * config.StabilityCheckIntervalMs + 400;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < feedMs && !stableGate.Task.IsCompleted)
        {
            var noise = ((int)(sw.ElapsedMilliseconds / 50) % 3 - 1) * 0.005m;
            weightSubject.OnNext(1.05m + noise);
            await Task.Delay(50);
        }

        var finished = await Task.WhenAny(stableGate.Task, Task.Delay(feedMs + 1000));
        finished.ShouldBe(stableGate.Task, "Full-window stable platform at ~1.05t should become IsStable.");

        var locked = await stableGate.Task;
        locked.StableWeight.ShouldNotBeNull();
        locked.StableWeight!.Value.ShouldBeInRange(1.00m, 1.10m);

        weightSubject.OnCompleted();
        weightSubject.Dispose();
    }
}
