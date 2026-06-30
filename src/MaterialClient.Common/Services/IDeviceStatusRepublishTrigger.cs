using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Triggers a full republish of current device statuses to SignalR (e.g. after reconnect).
///     Implemented by UI-layer device status tracker registry when available.
/// </summary>
public interface IDeviceStatusRepublishTrigger : ISingletonDependency
{
    void RepublishActiveStatuses();
}
