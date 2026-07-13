using MaterialClient.Common.Events;
using ReactiveUI;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.UI.Events;

/// <summary>
///     将 ABP ILocalEventBus 事件桥接到 ReactiveUI MessageBus。
///     放在 MaterialClient.UI，供 MaterialClient / Urban / Recycle 等所有宿主共享注册。
/// </summary>
public class LicensePlateRecognizedEventToMessageBusBridge
    : ILocalEventHandler<LicensePlateRecognizedEventData>, ITransientDependency
{
    public Task HandleEventAsync(LicensePlateRecognizedEventData eventData)
    {
        MessageBus.Current.SendMessage(new LicensePlateRecognizedMessage
        {
            PlateNumber = eventData.PlateNumber,
            ColorType = eventData.ColorType,
            VehicleColor = eventData.VehicleColor,
            VehicleType = eventData.VehicleType,
            PlateColor = eventData.PlateColor,
            DeviceType = eventData.DeviceType,
            DeviceName = eventData.DeviceName,
            Timestamp = eventData.Timestamp
        });
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 StatusChangedEventData 桥接到 StatusChangedMessage
/// </summary>
public class StatusChangedEventToMessageBusBridge
    : ILocalEventHandler<StatusChangedEventData>, ITransientDependency
{
    public Task HandleEventAsync(StatusChangedEventData eventData)
    {
        MessageBus.Current.SendMessage(new StatusChangedMessage(eventData.Status));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 PlateNumberChangedEventData 桥接到 PlateNumberChangedMessage
/// </summary>
public class PlateNumberChangedEventToMessageBusBridge
    : ILocalEventHandler<PlateNumberChangedEventData>, ITransientDependency
{
    public Task HandleEventAsync(PlateNumberChangedEventData eventData)
    {
        MessageBus.Current.SendMessage(new PlateNumberChangedMessage(eventData.PlateNumber));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 DeliveryTypeChangedEventData 桥接到 DeliveryTypeChangedMessage
/// </summary>
public class DeliveryTypeChangedEventToMessageBusBridge
    : ILocalEventHandler<DeliveryTypeChangedEventData>, ITransientDependency
{
    public Task HandleEventAsync(DeliveryTypeChangedEventData eventData)
    {
        MessageBus.Current.SendMessage(new DeliveryTypeChangedMessage(eventData.DeliveryType));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 WeighingRecordCreatedEventData 桥接到 WeighingRecordCreatedMessage
/// </summary>
public class WeighingRecordCreatedEventToMessageBusBridge
    : ILocalEventHandler<WeighingRecordCreatedEventData>, ITransientDependency
{
    public Task HandleEventAsync(WeighingRecordCreatedEventData eventData)
    {
        MessageBus.Current.SendMessage(new WeighingRecordCreatedMessage(eventData.WeighingRecordId));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 UpdatePlateNumberEventData 桥接到 UpdatePlateNumberMessage
/// </summary>
public class UpdatePlateNumberEventToMessageBusBridge
    : ILocalEventHandler<UpdatePlateNumberEventData>, ITransientDependency
{
    public Task HandleEventAsync(UpdatePlateNumberEventData eventData)
    {
        MessageBus.Current.SendMessage(
            new UpdatePlateNumberMessage(eventData.WeighingRecordId, eventData.PlateNumber));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 MatchSucceededEventData 桥接到 MatchSucceededMessage
/// </summary>
public class MatchSucceededEventToMessageBusBridge
    : ILocalEventHandler<MatchSucceededEventData>, ITransientDependency
{
    public Task HandleEventAsync(MatchSucceededEventData eventData)
    {
        MessageBus.Current.SendMessage(
            new MatchSucceededMessage(eventData.WaybillId, eventData.WeighingRecordId));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 SettingsSavedEventData 桥接到 SettingsSavedMessage
/// </summary>
public class SettingsSavedEventToMessageBusBridge
    : ILocalEventHandler<SettingsSavedEventData>, ITransientDependency
{
    public Task HandleEventAsync(SettingsSavedEventData eventData)
    {
        MessageBus.Current.SendMessage(new SettingsSavedMessage());
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 GhostGateSessionResetEventData 桥接到 GhostGateSessionResetMessage
/// </summary>
public class GhostGateSessionResetEventToMessageBusBridge
    : ILocalEventHandler<GhostGateSessionResetEventData>, ITransientDependency
{
    public Task HandleEventAsync(GhostGateSessionResetEventData eventData)
    {
        MessageBus.Current.SendMessage(new GhostGateSessionResetMessage(
            eventData.AbandonedPlateNumber,
            eventData.NewPlateNumber,
            eventData.DeviceName,
            eventData.OccurredAtUtc));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 UploadCompletedEventData 桥接到 UploadCompletedMessage
/// </summary>
public class UploadCompletedEventToMessageBusBridge
    : ILocalEventHandler<UploadCompletedEventData>, ITransientDependency
{
    public Task HandleEventAsync(UploadCompletedEventData eventData)
    {
        MessageBus.Current.SendMessage(new UploadCompletedMessage(eventData.WeighingRecordId));
        return Task.CompletedTask;
    }
}

/// <summary>
///     将 ServerApprovalSyncedEventData 桥接到 ServerApprovalSyncedMessage
/// </summary>
public class ServerApprovalSyncedEventToMessageBusBridge
    : ILocalEventHandler<ServerApprovalSyncedEventData>, ITransientDependency
{
    public Task HandleEventAsync(ServerApprovalSyncedEventData eventData)
    {
        MessageBus.Current.SendMessage(new ServerApprovalSyncedMessage(eventData.WeighingRecordId));
        return Task.CompletedTask;
    }
}
