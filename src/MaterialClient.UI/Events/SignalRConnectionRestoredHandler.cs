using MaterialClient.Common.Events;
using MaterialClient.UI.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.UI.Events;

/// <summary>
///     Re-publishes current device statuses when SignalR reconnects.
/// </summary>
public class SignalRConnectionRestoredHandler :
    ILocalEventHandler<SignalRConnectionRestoredEventData>,
    ISingletonDependency
{
    private readonly ISharedDeviceStatusTrackerRegistry _trackerRegistry;

    public SignalRConnectionRestoredHandler(ISharedDeviceStatusTrackerRegistry trackerRegistry)
    {
        _trackerRegistry = trackerRegistry;
    }

    public Task HandleEventAsync(SignalRConnectionRestoredEventData eventData)
    {
        _trackerRegistry.RepublishActiveStatuses();
        return Task.CompletedTask;
    }
}
