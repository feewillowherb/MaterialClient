using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     收发料类型变化消息（用于 ReactiveUI MessageBus）
/// </summary>
public class DeliveryTypeChangedMessage(DeliveryType deliveryType)
{
    /// <summary>
    ///     新的收发料类型
    /// </summary>
    public DeliveryType DeliveryType { get; } = deliveryType;
}

