using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services;

/// <summary>
///     有人值守称重服务接口
/// </summary>
public interface IAttendedWeighingService : IAsyncDisposable
{
    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    DeliveryType CurrentDeliveryType { get; }

    /// <summary>
    ///     启动监听
    /// </summary>
    Task StartAsync();

    /// <summary>
    ///     停止监听
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     获取当前状态
    /// </summary>
    AttendedWeighingStatus GetCurrentStatus();

    /// <summary>
    ///     获取当前推荐车牌号（启用"最新车牌"时按最近更新时间优先）
    /// </summary>
    string? GetMostFrequentPlateNumber();

    /// <summary>
    ///     设置收发料类型
    /// </summary>
    void SetDeliveryType(DeliveryType deliveryType);
}
