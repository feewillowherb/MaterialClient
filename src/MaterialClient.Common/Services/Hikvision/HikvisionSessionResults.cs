namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Result of a lightweight session probe (no re-login).
/// </summary>
public sealed record HikvisionSessionProbeResult(bool Valid, uint ErrorCode, string Message);

/// <summary>
///     Result of acquiring a shared HCNetSDK login session.
/// </summary>
public sealed record HikvisionSessionAcquireResult(bool Success, int UserId, uint ErrorCode, string Message);
