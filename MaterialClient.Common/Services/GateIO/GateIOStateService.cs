using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.GateIO;

/// <summary>
///     道闸 IO 状态管理服务
///     实现状态机逻辑、事件订阅和 IO 控制器调用
/// </summary>
public interface IGateIOStateService
{
    /// <summary>
    ///     启动服务
    /// </summary>
    Task StartAsync();

    /// <summary>
    ///     停止服务
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     获取当前状态（同步）
    /// </summary>
    GateIOState GetState();

    /// <summary>
    ///     获取当前状态（异步）
    /// </summary>
    Task<GateIOState> GetStateAsync();

    /// <summary>
    ///     获取状态详细信息
    /// </summary>
    Task<GateIOStateDetails> GetStateWithDetailsAsync();

    /// <summary>
    ///     重置异常状态为 Idle
    /// </summary>
    Task<bool> ResetAsync();

    /// <summary>
    ///     强制解锁锁定状态
    /// </summary>
    Task<bool> ForceUnlockAsync();

    /// <summary>
    ///     获取操作历史
    /// </summary>
    Task<List<GateIOOperationRecord>> GetOperationHistoryAsync();
}

/// <summary>
///     操作记录
/// </summary>
public class GateIOOperationRecord
{
    public DateTime Timestamp { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public GateIOState PreviousState { get; set; }
    public GateIOState NewState { get; set; }
}

/// <inheritdoc />
public sealed class GateIOStateService : IGateIOStateService, ISingletonDependency
{
    private readonly object _sync = new();
    private readonly BehaviorSubject<GateIOState> _stateSubject;
    private readonly ISettingsService _settingsService;
    private readonly IGateIOConfigurationValidator _configurationValidator;
    private readonly VzLPRGateIOController _ioController;
    private readonly ILogger<GateIOStateService>? _logger;

    private readonly List<GateIOOperationRecord> _operationHistory = new();

    // 事件订阅
    private IDisposable? _lprSubscription;
    private IDisposable? _statusSubscription;

    // 锁定状态相关
    private IDisposable? _lockTimer;
    private GateIODirection? _lockedDirection;
    private DateTime _lockedTime;
    private string? _lastError;
    private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(60);

    // 识别事件记录（用于协调地磅状态）
    private bool _plateRecognized;
    private LicensePlateDirection _lastRecognizedDirection;

    // 配置缓存
    private Dictionary<GateIODirection, LicensePlateRecognitionConfig?> _configByDirection = new();

    private bool _started;

    public GateIOStateService(
        ISettingsService settingsService,
        IGateIOConfigurationValidator configurationValidator,
        VzLPRGateIOController ioController,
        ILogger<GateIOStateService>? logger = null)
    {
        _settingsService = settingsService;
        _configurationValidator = configurationValidator;
        _ioController = ioController;
        _logger = logger;
        _stateSubject = new BehaviorSubject<GateIOState>(GateIOState.Idle);
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            if (_started)
                return;

            // 订阅车牌识别事件
            _lprSubscription = MessageBus.Current
                .Listen<LicensePlateRecognizedMessage>()
                .Subscribe(async msg => await HandlePlateRecognizedAsync(msg));

            // 订阅地磅状态事件
            _statusSubscription = MessageBus.Current
                .Listen<StatusChangedMessage>()
                .Subscribe(async msg => await HandleStatusChangedAsync(msg));

            _started = true;
            _logger?.LogInformation("GateIOStateService 已启动");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            if (!_started)
                return;

            // 停止定时器
            _lockTimer?.Dispose();
            _lockTimer = null;

            // 取消订阅
            _lprSubscription?.Dispose();
            _lprSubscription = null;
            _statusSubscription?.Dispose();
            _statusSubscription = null;

            // 重置状态
            _stateSubject.OnNext(GateIOState.Idle);
            _started = false;
            _logger?.LogInformation("GateIOStateService 已停止");
        }
    }

    /// <inheritdoc />
    public GateIOState GetState()
    {
        return _stateSubject.Value;
    }

    /// <inheritdoc />
    public async Task<GateIOState> GetStateAsync()
    {
        await Task.CompletedTask;
        return _stateSubject.Value;
    }

    /// <inheritdoc />
    public async Task<GateIOStateDetails> GetStateWithDetailsAsync()
    {
        await Task.CompletedTask;
        var state = _stateSubject.Value;
        var details = new GateIOStateDetails
        {
            State = state,
            LastStateUpdateTime = DateTime.Now
        };

        if (state == GateIOState.Locked)
        {
            details.LockedDirection = _lockedDirection;
            details.LockedDuration = DateTime.Now - _lockedTime;
        }

        if (state == GateIOState.Error)
        {
            details.LastError = _lastError;
        }

        return details;
    }

    /// <inheritdoc />
    public async Task<bool> ResetAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            var currentState = _stateSubject.Value;
            if (currentState != GateIOState.Error)
            {
                _logger?.LogWarning("在非 Error 状态下执行重置: {State}", currentState);
            }

            // 停止定时器
            _lockTimer?.Dispose();
            _lockTimer = null;

            // 关闭所有道闸
            _ = CloseAllGatesAsync();

            // 转换状态
            TransitionToState(GateIOState.Idle, "人工重置");

            _logger?.LogInformation("人工重置道闸 IO 状态");
            RecordOperation("Reset", currentState, GateIOState.Idle);

            return true;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ForceUnlockAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            var currentState = _stateSubject.Value;
            if (currentState != GateIOState.Locked)
            {
                _logger?.LogWarning("当前状态未锁定，无需解锁: {State}", currentState);
                return true;
            }

            // 停止定时器
            _lockTimer?.Dispose();
            _lockTimer = null;

            // 转换状态
            TransitionToState(GateIOState.Idle, "人工强制解锁");

            _logger?.LogWarning("人工强制解锁道闸 IO，可能有安全风险");
            RecordOperation("ForceUnlock", currentState, GateIOState.Idle);

            return true;
        }
    }

    /// <inheritdoc />
    public async Task<List<GateIOOperationRecord>> GetOperationHistoryAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            return _operationHistory.TakeLast(100).ToList();
        }
    }

    /// <summary>
    ///     处理车牌识别事件
    /// </summary>
    private async Task HandlePlateRecognizedAsync(LicensePlateRecognizedMessage message)
    {
        try
        {
            // 记录识别方向
            _plateRecognized = true;
            _lastRecognizedDirection = message.Direction;

            _logger?.LogDebug("收到车牌识别事件: Plate={Plate}, Direction={Direction}",
                message.PlateNumber, message.Direction);

            // 检查当前状态，如果在 Idle 且地磅未稳定，进入 Locked 状态
            var currentState = _stateSubject.Value;
            if (currentState == GateIOState.Idle)
            {
                // 查询地磅状态（通过服务获取）
                // 注意：这里需要一种方式获取当前地磅状态
                // 可以通过注入的服务或者通过某种状态查询
                // 暂时假设如果收到识别消息就进入锁定状态
                // 实际实现需要检查地磅状态
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理车牌识别事件失败");
        }
    }

    /// <summary>
    ///     处理地磅状态变更事件
    /// </summary>
    private async Task HandleStatusChangedAsync(StatusChangedMessage message)
    {
        try
        {
            _logger?.LogDebug("收到地磅状态变更事件: Status={Status}", message.Status);

            switch (message.Status)
            {
                case AttendedWeighingStatus.WaitingForStability:
                    // 如果已收到识别消息，进入 Locked 状态
                    if (_plateRecognized && _stateSubject.Value == GateIOState.Idle)
                    {
                        await EnterLockedStateAsync();
                    }
                    break;

                case AttendedWeighingStatus.WeightStabilized:
                    // 如果当前为 Locked，解锁并开闸
                    if (_stateSubject.Value == GateIOState.Locked)
                    {
                        await HandleWeightStabilizedAsync();
                    }
                    break;

                case AttendedWeighingStatus.OffScale:
                    // 车辆下磅，重置为 Idle
                    if (_stateSubject.Value is GateIOState.Locked or GateIOState.Opening)
                    {
                        await TransitionToIdleAsync("车辆下磅");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理地磅状态变更事件失败");
        }
    }

    /// <summary>
    ///     进入 Locked 状态
    /// </summary>
    private async Task EnterLockedStateAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            if (_stateSubject.Value != GateIOState.Idle)
                return;

            // 记录锁定方向
            _lockedDirection = VzLPRGateIOController.MapLprDirectionToGateIODirection(_lastRecognizedDirection);
            _lockedTime = DateTime.Now;

            // 转换状态
            TransitionToState(GateIOState.Locked, "车辆上磅未稳定");

            // 启动定时器，持续写入 0
            _lockTimer = Observable.Interval(TimeSpan.FromMilliseconds(100))
                .Subscribe(async _ => await WriteZeroToAllGatesAsync());

            // 启动超时检测
            Observable.Timer(_lockTimeout)
                .Subscribe(async _ =>
                {
                    if (_stateSubject.Value == GateIOState.Locked)
                    {
                        _lastError = "锁定超时";
                        TransitionToState(GateIOState.Error, "锁定超时");
                    }
                });

            _logger?.LogInformation("进入 Locked 状态，方向: {Direction}", _lockedDirection);
        }
    }

    /// <summary>
    ///     地磅稳定后处理
    /// </summary>
    private async Task HandleWeightStabilizedAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            if (_stateSubject.Value != GateIOState.Locked)
                return;

            // 停止定时器
            _lockTimer?.Dispose();
            _lockTimer = null;

            // 转换状态
            TransitionToState(GateIOState.Opening, "地磅稳定");

            // 根据进入方向打开对应出口
            _ = OpenGateByDirectionAsync();
        }
    }

    /// <summary>
    ///     根据方向打开道闸
    /// </summary>
    private async Task OpenGateByDirectionAsync()
    {
        if (!_lockedDirection.HasValue)
        {
            _logger?.LogWarning("锁定方向为空，无法开闸");
            await TransitionToIdleAsync("方向信息缺失");
            return;
        }

        // 确定开闸目标：Entry → Exit，Exit → Entry
        var targetDirection = _lockedDirection.Value == GateIODirection.Entry
            ? GateIODirection.Exit
            : GateIODirection.Entry;

        // 获取对应方向的配置
        var config = _configByDirection.GetValueOrDefault(targetDirection);
        if (config == null)
        {
            _logger?.LogError("未找到 {Direction} 方向的道闸配置", targetDirection);
            _lastError = $"未找到 {targetDirection} 方向的道闸配置";
            TransitionToState(GateIOState.Error, "配置缺失");
            return;
        }

        // 开闸
        try
        {
            await _ioController.OpenGateAsync(config, 500);
            _logger?.LogInformation("已开闸: Direction={Direction}, Device={Name}",
                targetDirection, config.Name);

            // 开闸完成后重置为 Idle
            await Task.Delay(500);
            await TransitionToIdleAsync("开闸完成");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "开闸失败: Direction={Direction}", targetDirection);
            _lastError = $"开闸失败: {ex.Message}";
            TransitionToState(GateIOState.Error, "开闸失败");
        }
    }

    /// <summary>
    ///     向所有道闸写入 0
    /// </summary>
    private async Task WriteZeroToAllGatesAsync()
    {
        try
        {
            foreach (var kvp in _configByDirection)
            {
                if (kvp.Value != null)
                {
                    await _ioController.WriteOutputAsync(kvp.Value, false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "写入 0 失败");
        }
    }

    /// <summary>
    ///     关闭所有道闸
    /// </summary>
    private async Task CloseAllGatesAsync()
    {
        try
        {
            foreach (var kvp in _configByDirection)
            {
                if (kvp.Value != null)
                {
                    await _ioController.CloseGateAsync(kvp.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "关闭道闸失败");
        }
    }

    /// <summary>
    ///     转换到 Idle 状态
    /// </summary>
    private async Task TransitionToIdleAsync(string reason)
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            TransitionToState(GateIOState.Idle, reason);
            _lockedDirection = null;
            _plateRecognized = false;
        }
    }

    /// <summary>
    ///     状态转换
    /// </summary>
    private void TransitionToState(GateIOState newState, string reason)
    {
        var oldState = _stateSubject.Value;

        // 验证状态转换是否合法
        if (!IsValidTransition(oldState, newState))
        {
            _logger?.LogWarning("非法状态转换: {OldState} → {NewState}", oldState, newState);
            return;
        }

        _stateSubject.OnNext(newState);

        // 广播状态变更消息
        MessageBus.Current.SendMessage(new GateIOStateChangedMessage(oldState, newState, reason));

        _logger?.LogInformation("状态转换: {OldState} → {NewState}, 原因: {Reason}", oldState, newState, reason);

        RecordOperation("StateChange", oldState, newState);
    }

    /// <summary>
    ///     验证状态转换是否合法
    /// </summary>
    private static bool IsValidTransition(GateIOState from, GateIOState to)
    {
        return (from, to) switch
        {
            (GateIOState.Idle, GateIOState.Locked) => true,
            (GateIOState.Locked, GateIOState.Opening) => true,
            (GateIOState.Locked, GateIOState.Error) => true,
            (GateIOState.Opening, GateIOState.Idle) => true,
            (GateIOState.Error, GateIOState.Idle) => true,
            (GateIOState.Locked, GateIOState.Idle) => true, // 强制解锁
            _ => false
        };
    }

    /// <summary>
    ///     记录操作
    /// </summary>
    private void RecordOperation(string operationType, GateIOState previousState, GateIOState newState)
    {
        _operationHistory.Add(new GateIOOperationRecord
        {
            Timestamp = DateTime.Now,
            OperationType = operationType,
            PreviousState = previousState,
            NewState = newState
        });

        // 保持历史记录不超过 100 条
        if (_operationHistory.Count > 100)
        {
            _operationHistory.RemoveAt(0);
        }
    }
}
