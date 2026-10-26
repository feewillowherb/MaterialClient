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
/// Reproduction of Urban field log 2026-10-21 15:02 (MaterialClient.Urban-20261021.log).
/// <para>
/// Config: MinWeightThreshold=0.5, WeightStabilityThreshold=0.05,
/// StabilityWindowMs=3000, StabilityCheckIntervalMs=200.
/// </para>
/// <para>
/// Log: Entered WaitingForStability at 0.550t → ~773ms later IsStable=true,
/// creating record at 0.545t, while true settled weight continued toward ~1.05t.
/// </para>
/// Root condition: CreateStabilityStream treats a short dense plateau as stable when
/// validCount &gt;= max(8, window/interval*0.5) and range &lt;= threshold*2, without
/// requiring the full StabilityWindowMs of above-threshold samples.
/// </summary>
public class WeighingStabilityPrematureLockReproductionTests
{
    private readonly ITestOutputHelper _output;

    public WeighingStabilityPrematureLockReproductionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Same weighing config as Urban Loaded configuration line / settings screenshot.
    /// </summary>
    private static WeighingConfiguration CreateLogSiteConfig() => new()
    {
        MinWeightThreshold = 0.5m,
        WeightStabilityThreshold = 0.05m,
        StabilityWindowMs = 3000,
        StabilityCheckIntervalMs = 200
    };

    /// <summary>
    /// Mirrors CreateStabilityStream / AttendedWeighingService stability Select body.
    /// </summary>
    private static WeightStabilityInfo EvaluateStabilityLikeProduction(
        IReadOnlyList<decimal> bufferWindow,
        WeighingConfiguration config)
    {
        var minDataPointsRequired =
            Math.Max(8, (int)(config.StabilityWindowMs / (double)config.StabilityCheckIntervalMs * 0.5));

        if (bufferWindow.Count == 0)
        {
            return new WeightStabilityInfo { IsStable = false };
        }

        var validDataPoints = bufferWindow.Where(w => w > config.MinWeightThreshold).ToList();
        if (validDataPoints.Count == 0)
        {
            return new WeightStabilityInfo { IsStable = false };
        }

        var min = validDataPoints.Min();
        var max = validDataPoints.Max();
        var range = max - min;
        var isStable = range <= config.WeightStabilityThreshold * 2 &&
                       validDataPoints.Count >= minDataPointsRequired;

        return new WeightStabilityInfo
        {
            IsStable = isStable,
            StableWeight = isStable ? (min + max) / 2 : null,
            Min = min,
            Max = max,
            Range = range
        };
    }

    [Fact]
    public void LogSiteConfig_MatchesUrbanScreenshotAndLoadedConfigurationLine()
    {
        var config = CreateLogSiteConfig();

        config.MinWeightThreshold.ShouldBe(0.5m);
        config.WeightStabilityThreshold.ShouldBe(0.05m);
        config.StabilityWindowMs.ShouldBe(3000);
        config.StabilityCheckIntervalMs.ShouldBe(200);

        var minDataPointsRequired =
            Math.Max(8, (int)(config.StabilityWindowMs / (double)config.StabilityCheckIntervalMs * 0.5));
        minDataPointsRequired.ShouldBe(8);

        var allowedRange = config.WeightStabilityThreshold * 2;
        allowedRange.ShouldBe(0.1m);
        (0.550m - 0.540m).ShouldBeLessThanOrEqualTo(allowedRange);
    }

    [Fact]
    public void Reproduce_UrbanLog_20261021_1502_BufferSnapshot_LocksAtPlateauBeforeTrueWeight()
    {
        var config = CreateLogSiteConfig();

        // ~800ms of 50ms scale ticks at mid-ascent plateau (matches ~773ms log gap)
        const int sampleIntervalMs = 50;
        var plateauMs = 800;
        var sampleCount = plateauMs / sampleIntervalMs; // 16
        var plateauPattern = new[] { 0.550m, 0.540m, 0.545m, 0.550m, 0.540m, 0.545m };

        // Sliding window contents after 800ms of ascent: only plateau samples (prior OffScale
        // points were <=0.5 and filtered out). True weight 1.05t has not arrived yet.
        var bufferWindow = Enumerable.Range(0, sampleCount)
            .Select(i => plateauPattern[i % plateauPattern.Length])
            .ToList();

        var elapsedMs = sampleCount * sampleIntervalMs;
        var result = EvaluateStabilityLikeProduction(bufferWindow, config);

        _output.WriteLine(
            $"After {elapsedMs}ms plateau: valid={bufferWindow.Count}, " +
            $"min={bufferWindow.Min():F3}, max={bufferWindow.Max():F3}, " +
            $"range={bufferWindow.Max() - bufferWindow.Min():F3}, " +
            $"IsStable={result.IsStable}, StableWeight={result.StableWeight}, " +
            $"windowMs={config.StabilityWindowMs}, minPointsRequired=8");

        elapsedMs.ShouldBeLessThan(config.StabilityWindowMs);
        bufferWindow.Count.ShouldBeGreaterThanOrEqualTo(8);
        result.IsStable.ShouldBeTrue(
            "Production formula marks mid-ascent 0.54~0.55 plateau as stable.");
        result.StableWeight.ShouldNotBeNull();
        result.StableWeight!.Value.ShouldBeInRange(0.530m, 0.560m,
            "Log created record at 0.545t.");

        const decimal trueSettledWeight = 1.05m;
        result.StableWeight.Value.ShouldBeLessThan(0.7m);
        (trueSettledWeight - result.StableWeight.Value).ShouldBeGreaterThan(0.4m,
            "Locked plateau weight is far below true ~1.05t — premature mid-ascent capture.");
    }

    [Fact]
    public async Task Reproduce_UrbanLog_20261021_1502_CreateStabilityStream_LocksBeforeFullWindow()
    {
        var config = CreateLogSiteConfig();
        var pipeline = new WeighingStreamPipeline(Substitute.For<ILogger<WeighingStreamPipeline>>());
        var weightSubject = new Subject<decimal>();
        var updates = new List<(TimeSpan Elapsed, WeightStabilityInfo Info)>();
        var stableGate = new TaskCompletionSource<WeightStabilityInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? streamError = null;
        var sw = Stopwatch.StartNew();

        // Also watch raw time-buffer fills to diagnose Rx scheduling
        var rawBufferCounts = new List<int>();
        using var rawSub = weightSubject
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
                TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Subscribe(b =>
            {
                rawBufferCounts.Add(b.Count);
                _output.WriteLine($"[raw Buffer] count={b.Count} at {sw.Elapsed.TotalMilliseconds:F0}ms");
            });

        using var subscription = pipeline
            .CreateStabilityStream(weightSubject.AsObservable(), config)
            .Subscribe(
                info =>
                {
                    updates.Add((sw.Elapsed, info));
                    _output.WriteLine(
                        $"[{sw.Elapsed.TotalMilliseconds,7:F0}ms] IsStable={info.IsStable}, " +
                        $"StableWeight={info.StableWeight}, Min={info.Min:F3}, Max={info.Max:F3}, Range={info.Range:F3}");
                    if (info.IsStable)
                    {
                        stableGate.TrySetResult(info);
                    }
                },
                ex =>
                {
                    streamError = ex;
                    _output.WriteLine("CreateStabilityStream OnError: " + ex);
                    stableGate.TrySetException(ex);
                });

        // Warm up like a long-running Urban process: Buffer(window, skip) only starts
        // closing windows after the first StabilityWindowMs. Field log at 15:02 had the
        // pipeline already running since 14:59, so skips fire every 200ms.
        var warmupDeadline = DateTime.UtcNow.AddMilliseconds(config.StabilityWindowMs + 400);
        while (DateTime.UtcNow < warmupDeadline)
        {
            weightSubject.OnNext(0.10m);
            await Task.Delay(100);
        }

        rawBufferCounts.Count.ShouldBeGreaterThan(0,
            "After warmup, sliding Buffer should already be emitting.");

        sw.Restart();
        var plateauPattern = new[] { 0.550m, 0.540m, 0.545m, 0.550m, 0.540m, 0.545m };
        for (var i = 0; i < 40 && !stableGate.Task.IsCompleted; i++)
        {
            weightSubject.OnNext(plateauPattern[i % plateauPattern.Length]);
            await Task.Delay(50);
        }

        var finished = await Task.WhenAny(stableGate.Task, Task.Delay(2500));

        _output.WriteLine(
            $"updates={updates.Count}, rawBuffers={rawBufferCounts.Count}, streamError={streamError?.Message}");

        streamError.ShouldBeNull();

        finished.ShouldBe(stableGate.Task,
            $"IsStable never became true after plateau. updates={updates.Count}, rawBuffers={rawBufferCounts.Count}");

        var locked = await stableGate.Task;
        var timeToStableMs = updates.First(u => u.Info.IsStable).Elapsed.TotalMilliseconds;

        _output.WriteLine($"Time to IsStable after plateau start: {timeToStableMs:F0}ms, StableWeight={locked.StableWeight}");

        // Field log ~773ms; allow jitter but must be well under full window
        timeToStableMs.ShouldBeLessThan(config.StabilityWindowMs);
        timeToStableMs.ShouldBeLessThan(1500);
        locked.StableWeight.ShouldNotBeNull();
        locked.StableWeight!.Value.ShouldBeInRange(0.530m, 0.560m);

        weightSubject.OnCompleted();
        weightSubject.Dispose();
    }
}
