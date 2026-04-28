using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     收发料类型变化事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class DeliveryTypeChangedEventData
{
    public DeliveryTypeChangedEventData(DeliveryType deliveryType)
    {
        DeliveryType = deliveryType;
    }

    /// <summary>
    ///     新的收发料类型
    /// </summary>
    public DeliveryType DeliveryType { get; }
}
