using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services;

public interface IGateIoControlService
{
    Task StartAsync();
    Task StopAsync();
}

/// <summary>
///     道闸 I/O 控制服务。
///     通过 ILocalEventBus 订阅识别事件和称重状态变化，实现会话状态管理和状态同步。
///     支持双控制模式架构（Lpr SDK 和 COM 直接控制）。
/// </summary>
public sealed class GateIoControlService : IGateIoControlService, ISingletonDependency
{
    /// <summary>
    ///     道闸会话状态（内部私有类）
    /// </summary>
    private sealed class GateIoSession
    {
        public bool SessionActive { get; set; }
        public LicensePlateDirection? EntrySide { get; set; }
        public bool ExitOpened { get; set; }
        public DateTime SessionStartedAt { get; set; }
        public string PlateNumber { get; set; } = string.Empty;

        public void Reset()
        {
            SessionActive = false;
            EntrySide = null;
            ExitOpened = false;
            SessionStartedAt = DateTime.MinValue;
            PlateNumber = string.Empty;
        }

        public string GetStatus()
        {
            if (!SessionActive)
                return "SessionActive=false";

            var duration = DateTime.UtcNow - SessionStartedAt;
            return $"SessionActive=true, EntrySide={EntrySide}, ExitOpened={ExitOpened}, Duration={duration:hh\\:mm\\:ss}, Plate={PlateNumber}";
        }
    }

    private readonly object _sync = new();
    private readonly ISettingsService _settingsService;
    private readonly IVzvisionLprService _vzvisionLprService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<GateIoControlService>? _logger;

    private IDisposable? _lprSubscription;
    private IDisposable? _settingsSavedSubscription;
    private IDisposable? _statusSubscription;
    private Dictionary<string, LicensePlateRecognitionConfig> _configByName = new(StringComparer.OrdinalIgnoreCase);
    private GateIoSession _session = new();
    private bool _started;
    private bool _gateIoEnabled = true;
    private AttendedWeighingStatus _currentWeighingStatus = AttendedWeighingStatus.OffScale;

    public GateIoControlService(
        ISettingsService settingsService,
        IVzvisionLprService vzvisionLprService,
        ILocalEventBus localEventBus,
        ILogger<GateIoControlService>? logger = null)
    {
        _settingsService = settingsService;
        _vzvisionLprService = vzvisionLprService;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        await RefreshRuntimeConfigAsync();

        // 配置校验
        var validationResult = ValidateGateConfiguration();
        if (!validationResult.IsValid)
        {
            _logger?.LogWarning("道闸配置校验失败: {Reason}, 道闸功能将进入降级模式",
                validationResult.Reason);
            _gateIoEnabled = false;
        }
        else
        {
            _gateIoEnabled = true;
            _logger?.LogInformation("道闸配置校验通过: A侧设备={DevicesA}, B侧设备={DevicesB}",
                string.Join(", ", validationResult.DevicesA), string.Join(", ", validationResult.DevicesB));
        }

        lock (_sync)
        {
            if (_started)
                return;

            _lprSubscription = _localEventBus
                .Subscribe<LicensePlateRecognizedEventData>(async msg => _ = HandlePlateRecognizedAsync(msg));

            _statusSubscription = _localEventBus
                .Subscribe<StatusChangedEventData>(async msg => OnStatusChanged(msg.Status));

            _settingsSavedSubscription = _localEventBus
                .Subscribe<SettingsSavedEventData>(async _msg =>
                {
                    _ = RefreshRuntimeConfigAsync();
                    // 配置保存后重新校验
                    var revalidationResult = ValidateGateConfiguration();
                    lock (_sync)
                    {
                        _gateIoEnabled = revalidationResult.IsValid;
                    }
                    if (!_gateIoEnabled)
                    {
                        _logger?.LogWarning("道闸配置更新后校验失败: {Reason}, 道闸功能进入降级模式",
                            revalidationResult.Reason);
                    }
                    else
                    {
                        _logger?.LogInformation("道闸配置更新后校验通过");
                    }
                });

            _started = true;
            _logger?.LogInformation("GateIoControlService 已启动并订阅 ILocalEventBus");
        }
    }

    public async Task StopAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            _lprSubscription?.Dispose();
            _lprSubscription = null;

            _statusSubscription?.Dispose();
            _statusSubscription = null;

            _settingsSavedSubscription?.Dispose();
            _settingsSavedSubscription = null;

            _started = false;
            _logger?.LogInformation("GateIoControlService 已停止并释放 ILocalEventBus 订阅");
        }
    }

    private async Task RefreshRuntimeConfigAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var map = settings.LicensePlateRecognitionConfigs
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            lock (_sync)
            {
                _configByName = map;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "刷新道闸 I/O 配置失败");
        }
    }

    /// <summary>
    ///     验证道闸配置的有效性（A/B 成对性校验）
    /// </summary>
    private GateConfigurationValidationResult ValidateGateConfiguration()
    {
        try
        {
            return GateConfigurationValidation.Validate(_configByName.Values);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "道闸配置校验过程中发生异常");
            return new GateConfigurationValidationResult
            {
                IsValid = false,
                Reason = "配置校验过程中发生异常"
            };
        }
    }

    /// <summary>
    ///     处理车牌识别事件
    /// </summary>
    private async Task HandlePlateRecognizedAsync(LicensePlateRecognizedEventData message)
    {
        try
        {
            // 检查道闸功能是否启用
            if (!_gateIoEnabled)
            {
                _logger?.LogDebug("道闸功能已禁用，跳过 Lpr 触发: Device={Device}", message.DeviceName);
                return;
            }

            // 状态门控逻辑：基于称重状态判断是否允许开闸
            if (!ShouldAllowGateOpen())
            {
                _logger?.LogDebug("称重状态为 {Status}，禁止 Lpr 开闸: Device={Device}",
                    _currentWeighingStatus, message.DeviceName);
                return;
            }

            LicensePlateRecognitionConfig? config;
            lock (_sync)
            {
                _configByName.TryGetValue(message.DeviceName, out config);
            }

            if (config == null || !config.EnableGateIo)
                return;

            var vendor = config.ResolvedDeviceType;
            if (vendor != LprDeviceType.Vzvision)
            {
                _logger?.LogInformation(
                    "当前设备类型暂未支持道闸 I/O 功能: DeviceType={DeviceType}, Device={Device}",
                    vendor, message.DeviceName);
                return;
            }

            if (!uint.TryParse(config.IoChannel, out var ioChannel))
            {
                _logger?.LogWarning("I/O 通道配置无效，跳过开闸: Device={Device}, IoChannel={IoChannel}",
                    message.DeviceName, config.IoChannel);
                return;
            }

            // 会话管理：检查并创建会话
            string? ghostAbandonedPlate = null;
            lock (_sync)
            {
                if (_session.SessionActive)
                {
                    if (!TryResetGhostSession(message.PlateNumber, message.DeviceName, out ghostAbandonedPlate))
                        return;
                }

                // 创建新会话
                _session.SessionActive = true;
                _session.EntrySide = config.Direction;
                _session.ExitOpened = false;
                _session.SessionStartedAt = DateTime.UtcNow;
                _session.PlateNumber = message.PlateNumber;

                _logger?.LogInformation("创建道闸会话: Device={Device}, EntrySide={EntrySide}, Plate={Plate}",
                    message.DeviceName, config.Direction, message.PlateNumber);
            }

            if (!string.IsNullOrWhiteSpace(ghostAbandonedPlate))
            {
                var ghostEventData = new GhostGateSessionResetEventData(
                    ghostAbandonedPlate,
                    message.PlateNumber,
                    message.DeviceName);
                _ = _localEventBus.PublishAsync(ghostEventData);
                _logger?.LogInformation(
                    "已发布幽灵会话重置事件: AbandonedPlate={AbandonedPlate}, NewPlate={NewPlate}, Device={Device}, OccurredAtUtc={OccurredAtUtc:O}",
                    ghostAbandonedPlate, message.PlateNumber, message.DeviceName, ghostEventData.OccurredAtUtc);
            }

            // 调用统一控制接口打开入口道闸
            await OpenGateAsync(config, ioChannel);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理道闸 I/O 触发失败: Device={Device}, Plate={Plate}",
                message.DeviceName, message.PlateNumber);
        }
    }

    /// <summary>
    ///     判断是否允许道闸打开（基于称重状态的门控逻辑）
    /// </summary>
    private bool ShouldAllowGateOpen()
    {
        return _currentWeighingStatus == AttendedWeighingStatus.OffScale;
    }

    /// <summary>
    ///     尝试重置幽灵会话（会话激活但车辆从未上磅）。
    ///     调用方 MUST 已持有 _sync 锁。
    /// </summary>
    /// <returns>true 表示检测到幽灵会话并已重置；false 表示未重置（正常拒绝或跳过）</returns>
    private bool TryResetGhostSession(string newPlateNumber, string newDeviceName, out string? abandonedPlate)
    {
        abandonedPlate = null;

        if (!_session.SessionActive)
            return false;

        var isNewPlate = !string.Equals(newPlateNumber, _session.PlateNumber, StringComparison.OrdinalIgnoreCase);

        // 同一车牌重复识别：车辆在闸口等待上磅，LRP 持续识别
        if (!isNewPlate)
        {
            _logger?.LogDebug("同一车牌重复识别，跳过: Plate={Plate}, SessionStatus={SessionStatus}",
                newPlateNumber, _session.GetStatus());
            return false;
        }

        // 幽灵会话：不同车牌 + 从未上磅
        if (!_session.ExitOpened && _currentWeighingStatus == AttendedWeighingStatus.OffScale)
        {
            abandonedPlate = _session.PlateNumber;
            _logger?.LogWarning(
                "检测到幽灵会话(从未上磅)，新车牌触发重置: " +
                "OldPlate={OldPlate}, OldEntrySide={OldEntrySide}, OldDuration={OldDuration}, " +
                "NewPlate={NewPlate}, NewDevice={NewDevice}",
                abandonedPlate, _session.EntrySide,
                DateTime.UtcNow - _session.SessionStartedAt,
                newPlateNumber, newDeviceName);
            _session.Reset();
            return true;
        }

        // 会话正在处理中（已上磅/正在称重），正常拒绝
        _logger?.LogInformation("道闸会话已激活且正在处理中，拒绝新车牌: Device={Device}, SessionPlate={SessionPlate}, NewPlate={NewPlate}",
            newDeviceName, _session.PlateNumber, newPlateNumber);
        return false;
    }

    /// <summary>
    ///     清理会话状态
    /// </summary>
    private void ClearSession()
    {
        try
        {
            string? sessionInfo = null;
            lock (_sync)
            {
                if (_session.SessionActive)
                {
                    var duration = DateTime.UtcNow - _session.SessionStartedAt;
                    sessionInfo = $"SessionDuration={duration:hh\\:mm\\:ss}, EntrySide={_session.EntrySide}";
                    _session.Reset();
                }
            }

            if (sessionInfo != null)
            {
                _logger?.LogInformation("道闸会话已清理: {SessionInfo}", sessionInfo);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "清理道闸会话时发生异常");
            // 强制重置会话状态，防止会话泄漏
            lock (_sync)
            {
                _session.Reset();
            }
        }
    }

    /// <summary>
    ///     处理称重状态变化事件
    /// </summary>
    private void OnStatusChanged(AttendedWeighingStatus newStatus)
    {
        try
        {
            if (!_gateIoEnabled)
            {
                _logger?.LogDebug("道闸功能已禁用，跳过状态变化处理: Status={Status}", newStatus);
                return;
            }

            _currentWeighingStatus = newStatus;
            _logger?.LogDebug("称重状态变化: {Status}", newStatus);

            switch (newStatus)
            {
                case AttendedWeighingStatus.OffScale:
                    // 清理会话
                    ClearSession();
                    _logger?.LogInformation("称重状态 OffScale，清理道闸会话");
                    break;

                case AttendedWeighingStatus.WaitingForStability:
                    _logger?.LogInformation("称重状态 WaitingForStability，禁止 Lpr 开闸");
                    break;

                case AttendedWeighingStatus.WeightStabilized:
                    _logger?.LogInformation("称重状态 WeightStabilized，禁止 Lpr 开闸");
                    break;

                case AttendedWeighingStatus.WaitingForDeparture:
                    // 触发出口开闸逻辑
                    _ = Task.Run(() => OpenExitGateAsync());
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理称重状态变化失败: Status={Status}", newStatus);
            // 不抛出异常，允许称重状态机继续工作
        }
    }

    /// <summary>
    ///     打开出口道闸（在 WaitingForDeparture 状态时触发）
    /// </summary>
    private async Task OpenExitGateAsync()
    {
        try
        {
            LicensePlateDirection? entrySide;
            bool exitOpened;

            lock (_sync)
            {
                if (!_session.SessionActive)
                {
                    _logger?.LogDebug("会话未激活，跳过出口开闸");
                    return;
                }

                entrySide = _session.EntrySide;
                exitOpened = _session.ExitOpened;

                if (exitOpened)
                {
                    _logger?.LogDebug("出口道闸已开，跳过重复触发");
                    return;
                }
            }

            if (!entrySide.HasValue)
            {
                _logger?.LogWarning("会话入口侧为空，跳过出口开闸");
                return;
            }

            // 计算出口侧（A ↔ B）
            var exitSide = entrySide.Value == LicensePlateDirection.A ? LicensePlateDirection.B : LicensePlateDirection.A;

            // 查找出口侧配置
            LicensePlateRecognitionConfig? exitConfig = null;
            lock (_sync)
            {
                exitConfig = _configByName.Values
                    .FirstOrDefault(c => c.EnableGateIo && c.Direction == exitSide);
            }

            if (exitConfig == null)
            {
                _logger?.LogWarning("未找到出口侧道闸配置: ExitSide={ExitSide}", exitSide);
                lock (_sync)
                {
                    _session.ExitOpened = true; // 标记为已开，避免重复日志
                }
                return;
            }

            if (!uint.TryParse(exitConfig.IoChannel, out var ioChannel))
            {
                _logger?.LogWarning("出口侧 I/O 通道配置无效: Device={Device}, IoChannel={IoChannel}",
                    exitConfig.Name, exitConfig.IoChannel);
                lock (_sync)
                {
                    _session.ExitOpened = true; // 标记为已开，避免重复日志
                }
                return;
            }

            // 调用统一控制接口打开出口道闸
            await OpenGateAsync(exitConfig, ioChannel);

            lock (_sync)
            {
                _session.ExitOpened = true;
            }

            _logger?.LogInformation("称重状态 WaitingForDeparture，打开出口道闸: ExitSide={ExitSide}, Device={Device}",
                exitSide, exitConfig.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "打开出口道闸失败");
            // 保持 ExitOpened = false，允许用户通过遥控器手动开闸
        }
    }

    /// <summary>
    ///     统一道闸控制接口（支持双控制模式）
    /// </summary>
    private async Task OpenGateAsync(LicensePlateRecognitionConfig config, uint ioChannel)
    {
        try
        {
            // 默认使用 Lpr SDK 控制方式
            // 未来可扩展：根据 config.GateIoControlMode 分发到不同的控制方法
            await OpenGateViaLprSdkAsync(config, ioChannel);
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogWarning(ex, "不支持的道闸控制方式");
            // 不抛出异常，允许主流程继续
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "道闸 I/O 控制失败: Device={Device}, IoChannel={IoChannel}",
                config.Name, config.IoChannel);
            // 不抛出异常，允许主流程继续
        }
    }

    /// <summary>
    ///     通过 Lpr SDK 控制道闸（方式 1：当前实现）
    /// </summary>
    private async Task OpenGateViaLprSdkAsync(LicensePlateRecognitionConfig config, uint ioChannel)
    {
        // 注意：当前实现仅支持 Vzvision 设备（由调用方按行 DeviceType 门控）
        await _vzvisionLprService.SetIoOutputAutoRespAsync(config, ioChannel, 500);
    }

    /// <summary>
    ///     直接通过 COM 控制道闸（方式 2：预留实现，抛出"不支持"异常）
    /// </summary>
    private async Task OpenGateViaComAsync(LicensePlateRecognitionConfig config, uint ioChannel)
    {
        throw new NotSupportedException("直接通过 COM 控制道闸 I/O 功能暂不支持，请使用 Lpr SDK 控制方式");
    }
}
