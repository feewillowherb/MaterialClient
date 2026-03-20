using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
///     Preprocess weights sent to truck scale in test mode.
///     It converts a single target weight (B) into a smooth transition from the current weight (A),
///     and continues sending the final weight until the stability window duration elapses.
/// </summary>
public interface IScaleTestWeightPreprocessorService
{
    /// <summary>
    ///     Enqueue a target weight (unit: ton) to be applied in test mode.
    /// </summary>
    /// <param name="targetWeight">Target weight in ton</param>
    void Enqueue(decimal targetWeight);
}

public sealed partial class ScaleTestWeightPreprocessorService : IScaleTestWeightPreprocessorService,
    IAsyncDisposable, ISingletonDependency
{
    private const int TickMs = 200;
    private const int TransitionSteps = 5; // 5 steps => 1s transition (200ms * 5)
    private const int DefaultHoldDurationMs = 3000; // match WeighingConfiguration.StabilityWindowMs default

    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly ILogger<ScaleTestWeightPreprocessorService>? _logger;

    private readonly ConcurrentQueue<decimal> _targetQueue = new();
    private readonly object _sync = new();

    private readonly Timer _timer;
    private bool _timerRunning;

    // Transition state A -> B
    private bool _isTransitioning;
    private int _transitionStepIndex; // 0..TransitionSteps
    private decimal _fromWeight;
    private decimal _toWeight;

    // Holding final weight (B) for stability checks
    private bool _isHolding;
    private long _holdUntilTick;
    private decimal _stableValue;

    public ScaleTestWeightPreprocessorService(
        ITruckScaleWeightService truckScaleWeightService,
        ILogger<ScaleTestWeightPreprocessorService>? logger = null)
    {
        _truckScaleWeightService = truckScaleWeightService;
        _logger = logger;

        // Keep timer disabled until first enqueue.
        _timer = new Timer(TimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Enqueue(decimal targetWeight)
    {
        lock (_sync)
        {
            _targetQueue.Enqueue(targetWeight);

            if (!_timerRunning)
            {
                _stableValue = _truckScaleWeightService.GetCurrentWeight();
                _isTransitioning = false;
                _isHolding = false;
                _transitionStepIndex = 0;

                _timerRunning = true;
                // First tick after TickMs, then every TickMs.
                _timer.Change(TickMs, TickMs);
            }
        }
    }

    private void TimerTick(object? state)
    {
        decimal? toSend = null;

        try
        {
            lock (_sync)
            {
                var now = Environment.TickCount64;

                if (_isTransitioning)
                {
                    _transitionStepIndex++;
                    toSend = Interpolate(_fromWeight, _toWeight, _transitionStepIndex,
                        TransitionSteps);

                    if (_transitionStepIndex >= TransitionSteps)
                    {
                        _isTransitioning = false;
                        _stableValue = _toWeight;

                        // Once the curve reaches B, keep sending B for stability window.
                        _isHolding = true;
                        _holdUntilTick = now + DefaultHoldDurationMs;
                    }
                }
                else if (_isHolding)
                {
                    if (now < _holdUntilTick)
                    {
                        toSend = _stableValue;
                    }
                    else
                    {
                        _isHolding = false;

                        // If there is another target in queue, start next transition now.
                        if (_targetQueue.TryDequeue(out var nextTarget))
                        {
                            _fromWeight = _truckScaleWeightService.GetCurrentWeight();
                            _toWeight = nextTarget;

                            _isTransitioning = true;
                            _transitionStepIndex = 1;
                            toSend = Interpolate(_fromWeight, _toWeight, _transitionStepIndex,
                                TransitionSteps);
                        }
                        else
                        {
                            StopTimerLocked();
                            return;
                        }
                    }
                }
                else
                {
                    if (!_targetQueue.TryDequeue(out var nextTarget))
                    {
                        StopTimerLocked();
                        return;
                    }

                    // Start transition from current weight (A) to queued target (B).
                    _fromWeight = _truckScaleWeightService.GetCurrentWeight();
                    _toWeight = nextTarget;

                    _isTransitioning = true;
                    _transitionStepIndex = 0;

                    // Send step 1 immediately on this tick.
                    _transitionStepIndex = 1;
                    toSend = Interpolate(_fromWeight, _toWeight, _transitionStepIndex,
                        TransitionSteps);
                }
            }

            if (toSend.HasValue)
            {
                _truckScaleWeightService.SetWeight(toSend.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ScaleTestWeightPreprocessorService tick failed");
        }
    }

    private void StopTimerLocked()
    {
        _timerRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _isTransitioning = false;
        _isHolding = false;
        _transitionStepIndex = 0;
    }

    /// <summary>
    ///     smoothstep curve: t * t * (3 - 2 * t)
    /// </summary>
    public static decimal Smoothstep(decimal t)
    {
        return t * t * (3m - 2m * t);
    }

    public static decimal Interpolate(decimal a, decimal b, int stepIndex, int steps)
    {
        if (steps <= 0) return b;
        if (stepIndex <= 0) return a;
        if (stepIndex >= steps) return b;

        var t = (decimal)stepIndex / steps;
        var s = Smoothstep(t);
        return a + (b - a) * s;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            StopTimerLocked();
            while (_targetQueue.TryDequeue(out _))
            {
                // Clear queue
            }
        }

        await Task.CompletedTask;
    }
}

