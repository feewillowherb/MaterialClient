using System.Reactive.Linq;
using System.Linq;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

public interface ILprGateIoControlService
{
    Task StartAsync();
    Task StopAsync();
}

/// <summary>
///     车牌识别后的道闸 I/O 控制服务。
///     通过 MessageBus 订阅识别事件，实现与识别服务解耦。
/// </summary>
public sealed class LprGateIoControlService : ILprGateIoControlService, ISingletonDependency
{
    private readonly object _sync = new();
    private readonly ISettingsService _settingsService;
    private readonly IVzvisionLprService _vzvisionLprService;
    private readonly ILogger<LprGateIoControlService>? _logger;

    private IDisposable? _lprSubscription;
    private IDisposable? _settingsSavedSubscription;
    private Dictionary<string, LicensePlateRecognitionConfig> _configByName = new(StringComparer.OrdinalIgnoreCase);
    private bool _started;

    public LprGateIoControlService(
        ISettingsService settingsService,
        IVzvisionLprService vzvisionLprService,
        ILogger<LprGateIoControlService>? logger = null)
    {
        _settingsService = settingsService;
        _vzvisionLprService = vzvisionLprService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        await RefreshRuntimeConfigAsync();
        lock (_sync)
        {
            if (_started)
                return;

            _lprSubscription = MessageBus.Current
                .Listen<LicensePlateRecognizedMessage>()
                .Subscribe(msg => _ = HandlePlateRecognizedAsync(msg));

            _settingsSavedSubscription = MessageBus.Current
                .Listen<SettingsSavedMessage>()
                .Subscribe(_msg =>
                {
                    _ = RefreshRuntimeConfigAsync();
                });

            _started = true;
            _logger?.LogInformation("LprGateIoControlService 已启动并订阅 MessageBus");
        }
    }

    public async Task StopAsync()
    {
        await Task.CompletedTask;
        lock (_sync)
        {
            _lprSubscription?.Dispose();
            _lprSubscription = null;

            _settingsSavedSubscription?.Dispose();
            _settingsSavedSubscription = null;

            _started = false;
            _logger?.LogInformation("LprGateIoControlService 已停止并释放 MessageBus 订阅");
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

    private async Task HandlePlateRecognizedAsync(LicensePlateRecognizedMessage message)
    {
        try
        {
            LicensePlateRecognitionConfig? config;
            lock (_sync)
            {
                _configByName.TryGetValue(message.DeviceName, out config);
            }

            if (config == null || !config.EnableGateIo)
                return;

            if (message.DeviceType != LprDeviceType.Vzvision)
            {
                _logger?.LogInformation(
                    "当前设备类型暂未支持道闸 I/O 功能: DeviceType={DeviceType}, Device={Device}",
                    message.DeviceType, message.DeviceName);
                return;
            }

            if (!uint.TryParse(config.IoChannel, out var ioChannel))
            {
                _logger?.LogWarning("I/O 通道配置无效，跳过开闸: Device={Device}, IoChannel={IoChannel}",
                    message.DeviceName, config.IoChannel);
                return;
            }

            await _vzvisionLprService.SetIoOutputAutoRespAsync(config, ioChannel, 500);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理道闸 I/O 触发失败: Device={Device}, Plate={Plate}",
                message.DeviceName, message.PlateNumber);
        }
    }
}
