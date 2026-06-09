using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Services;

/// <summary>
///     Tracks the active <see cref="SharedDeviceStatusTracker" /> instance for reconnect uploads.
/// </summary>
public interface ISharedDeviceStatusTrackerRegistry
{
    void Register(SharedDeviceStatusTracker tracker);

    void Unregister(SharedDeviceStatusTracker tracker);

    void RepublishActiveStatuses();
}

public class SharedDeviceStatusTrackerRegistry : ISharedDeviceStatusTrackerRegistry, ISingletonDependency
{
    private SharedDeviceStatusTracker? _activeTracker;

    public void Register(SharedDeviceStatusTracker tracker)
    {
        _activeTracker = tracker;
    }

    public void Unregister(SharedDeviceStatusTracker tracker)
    {
        if (ReferenceEquals(_activeTracker, tracker))
        {
            _activeTracker = null;
        }
    }

    public void RepublishActiveStatuses()
    {
        _activeTracker?.RepublishCurrentStatuses();
    }
}
