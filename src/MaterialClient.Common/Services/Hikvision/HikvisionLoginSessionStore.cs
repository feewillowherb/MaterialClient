using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Process-wide Hikvision login session store shared by LPR and monitoring capture.
/// </summary>
public interface IHikvisionLoginSessionStore
{
    string BuildKey(string ip, int port, string userName);

    HikvisionSessionAcquireResult Acquire(string ip, int port, string userName, string password);

    void Release(string key);

    void Invalidate(string key);

    void InvalidateAll();

    HikvisionSessionProbeResult Probe(string key);

    bool TryGetUserId(string key, out int userId);

    void NotifySdkReinitialized();

    /// <summary>
    ///     Raised after all sessions are force-cleared for SDK soft-reset / Cleanup.
    /// </summary>
    event Action? SessionsClearedForSdkReset;

    /// <summary>
    ///     Raised after soft-reset has re-Init'd the SDK; listeners should re-Acquire and StartListen.
    /// </summary>
    event Action? SdkReinitialized;
}

/// <summary>
///     Shared HCNetSDK login sessions keyed by Ip:Port:Username.
/// </summary>
public sealed class HikvisionLoginSessionStore : IHikvisionLoginSessionStore, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<HikvisionLoginSessionStore>? _logger;
    private readonly object _gate = new();

    public HikvisionLoginSessionStore(ILogger<HikvisionLoginSessionStore>? logger = null)
    {
        _logger = logger;
    }

    public event Action? SessionsClearedForSdkReset;
    public event Action? SdkReinitialized;

    public string BuildKey(string ip, int port, string userName) =>
        $"{ip}:{port}:{userName}";

    public HikvisionSessionAcquireResult Acquire(string ip, int port, string userName, string password)
    {
        var key = BuildKey(ip, port, userName ?? string.Empty);
        lock (_gate)
        {
            if (_sessions.TryGetValue(key, out var existing) && existing.UserId >= 0)
            {
                var probe = ProbeUnlocked(key, existing, force: false);
                if (probe.Valid)
                {
                    existing.RefCount++;
                    return new HikvisionSessionAcquireResult(true, existing.UserId, 0, "reused cached session");
                }

                LogoutQuiet(existing.UserId, key);
                _sessions.TryRemove(key, out _);
            }

            var login = Login(ip, port, userName ?? string.Empty, password ?? string.Empty);
            if (login.UserId < 0)
            {
                return new HikvisionSessionAcquireResult(false, -1, login.ErrorCode, login.Message);
            }

            _sessions[key] = new SessionEntry(login.UserId);
            _logger?.LogInformation("Hikvision session acquired: Key={Key}, UserId={UserId}", key, login.UserId);
            return new HikvisionSessionAcquireResult(true, login.UserId, 0, "login ok");
        }
    }

    public void Release(string key)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(key, out var entry))
                return;

            entry.RefCount = Math.Max(0, entry.RefCount - 1);
            if (entry.RefCount > 0)
                return;

            LogoutQuiet(entry.UserId, key);
            _sessions.TryRemove(key, out _);
            _logger?.LogInformation("Hikvision session released: Key={Key}, UserId={UserId}", key, entry.UserId);
        }
    }

    public void Invalidate(string key)
    {
        lock (_gate)
        {
            if (!_sessions.TryRemove(key, out var entry))
                return;

            LogoutQuiet(entry.UserId, key);
            _logger?.LogWarning("Hikvision session invalidated: Key={Key}, UserId={UserId}", key, entry.UserId);
        }
    }

    public void InvalidateAll()
    {
        lock (_gate)
        {
            foreach (var pair in _sessions.ToArray())
            {
                LogoutQuiet(pair.Value.UserId, pair.Key);
            }

            _sessions.Clear();
            _logger?.LogWarning("Hikvision sessions InvalidateAll completed");
        }

        try
        {
            SessionsClearedForSdkReset?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SessionsClearedForSdkReset handler failed");
        }
    }

    public void NotifySdkReinitialized()
    {
        try
        {
            SdkReinitialized?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SdkReinitialized handler failed");
        }
    }

    public HikvisionSessionProbeResult Probe(string key)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(key, out var entry) || entry.UserId < 0)
            {
                return new HikvisionSessionProbeResult(false, 0, "no cached session");
            }

            return ProbeUnlocked(key, entry, force: true);
        }
    }

    public bool TryGetUserId(string key, out int userId)
    {
        if (_sessions.TryGetValue(key, out var entry) && entry.UserId >= 0)
        {
            userId = entry.UserId;
            return true;
        }

        userId = -1;
        return false;
    }

    private HikvisionSessionProbeResult ProbeUnlocked(string key, SessionEntry entry, bool force)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && !HikvisionSessionProbePolicy.CanProbe(now, entry.LastProbeSucceededAt))
        {
            return new HikvisionSessionProbeResult(true, 0, "throttled; treating cache as valid");
        }

        var ok = HikvisionSdk.NET_DVR_RemoteControl(
            entry.UserId,
            HikvisionSdk.NET_DVR_CHECK_USER_STATUS,
            IntPtr.Zero,
            0);
        if (ok)
        {
            entry.LastProbeSucceededAt = now;
            _logger?.LogDebug("CHECK_USER_STATUS ok: Key={Key}, UserId={UserId}", key, entry.UserId);
            return new HikvisionSessionProbeResult(true, 0, "CHECK_USER_STATUS ok");
        }

        var err = HikvisionSdk.NET_DVR_GetLastError();
        _logger?.LogWarning(
            "CHECK_USER_STATUS failed: Key={Key}, UserId={UserId}, ErrorCode={ErrorCode}",
            key, entry.UserId, err);
        return new HikvisionSessionProbeResult(false, err, $"CHECK_USER_STATUS failed ({err})");
    }

    private LoginAttempt Login(string ip, int port, string userName, string password)
    {
        try
        {
            var loginInfo = new HikvisionSdk.NET_DVR_USER_LOGIN_INFO
            {
                sDeviceAddress = ToFixedBytes(ip, 129),
                sUserName = ToFixedBytes(userName, 64),
                sPassword = ToFixedBytes(password, 64),
                wPort = (ushort)port,
                bUseAsynLogin = 0
            };
            var deviceInfo = new HikvisionSdk.NET_DVR_DEVICEINFO_V40();
            var userId = HikvisionSdk.NET_DVR_Login_V40(ref loginInfo, ref deviceInfo);
            if (userId < 0)
            {
                var err = HikvisionSdk.NET_DVR_GetLastError();
                return new LoginAttempt(userId, err, $"Login failed ErrorCode={err}");
            }

            return new LoginAttempt(userId, 0, "ok");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Login exception: Ip={Ip}, Port={Port}", ip, port);
            return new LoginAttempt(-1, 0, ex.Message);
        }
    }

    private void LogoutQuiet(int userId, string key)
    {
        if (userId < 0)
            return;

        try
        {
            HikvisionSdk.NET_DVR_Logout(userId);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Logout failed: Key={Key}, UserId={UserId}", key, userId);
        }
    }

    private static byte[] ToFixedBytes(string value, int size)
    {
        var bytes = new byte[size];
        var src = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Buffer.BlockCopy(src, 0, bytes, 0, Math.Min(src.Length, size - 1));
        return bytes;
    }

    private sealed class SessionEntry
    {
        public SessionEntry(int userId)
        {
            UserId = userId;
            RefCount = 1;
            LastProbeSucceededAt = DateTimeOffset.UtcNow;
        }

        public int UserId { get; }
        public int RefCount { get; set; }
        public DateTimeOffset? LastProbeSucceededAt { get; set; }
    }

    private sealed record LoginAttempt(int UserId, uint ErrorCode, string Message);
}
