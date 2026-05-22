using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using MaterialClient.UI.Models;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     ViewModel for the shared DeviceStatusBar control.
///     Manages a collection of device status items, queries initial state,
///     and subscribes to device status change events via ILocalEventBus.
///     Registered as ITransientDependency (per-window scope).
/// </summary>
public class DeviceStatusBarViewModel : ReactiveObject, IDisposable, ITransientDependency
{
    private readonly ILocalEventBus? _localEventBus;
    private readonly ILogger<DeviceStatusBarViewModel> _logger;
    private readonly CompositeDisposable _subscriptions = [];

    public DeviceStatusBarViewModel(
        ILocalEventBus localEventBus,
        ILogger<DeviceStatusBarViewModel> logger)
    {
        _localEventBus = localEventBus;
        _logger = logger;
    }

    /// <summary>
    ///     Collection of device status items displayed in the status bar.
    /// </summary>
    [Reactive]
    public ObservableCollection<DeviceStatusItem> Devices { get; set; } = [];

    /// <summary>
    ///     Initialize the device list with the specified device names.
    ///     Each consuming app calls this with its own device set.
    /// </summary>
    public void InitializeDevices(IReadOnlyList<string> deviceNames)
    {
        var items = deviceNames.Select(name => new DeviceStatusItem(name, false)).ToList();
        Devices = new ObservableCollection<DeviceStatusItem>(items);
    }

    /// <summary>
    ///     Update a specific device's online status by name.
    /// </summary>
    public void UpdateDeviceStatus(string deviceName, bool isOnline)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            var index = Devices.ToList().FindIndex(d => d.Name == deviceName);
            if (index >= 0)
            {
                Devices[index] = new DeviceStatusItem(deviceName, isOnline);
            }
        });
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}
