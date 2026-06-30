namespace MaterialClient.Common.Events;

/// <summary>
///     授权设备变更事件（F4）。由 DeviceStatusSignalRClient 在 SignalR 防篡改验签判定
///     <c>RevocationReason.DeviceChanged</c> 时经 ABP <c>ILocalEventBus</c> 发布，
///     交由 MaterialClient.Urban 层处理「清除本地授权 + 弹出仅在线激活窗 + 终止运行」。
/// </summary>
public class LicenseDeviceRevokedEto(Guid projectId, string reason)
{
    /// <summary>
    ///     发生设备变更的项目 ID。
    /// </summary>
    public Guid ProjectId { get; } = projectId;

    /// <summary>
    ///     面向用户/日志的失败原因（来自 JwtAntiTamperResult.Reason）。
    /// </summary>
    public string Reason { get; } = reason;
}
