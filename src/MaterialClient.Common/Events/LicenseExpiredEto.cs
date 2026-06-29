namespace MaterialClient.Common.Events;

/// <summary>
///     授权已过期事件。由 <see cref="Services.DeviceStatusSignalRClient" /> 在防篡改验签判定
///     <c>RevocationReason.Expired</c> 且本地 JWT 复验失败后发布，交由 Urban 层终止运行。
/// </summary>
public class LicenseExpiredEto(Guid projectId, string reason)
{
    public Guid ProjectId { get; } = projectId;

    public string Reason { get; } = reason;
}
