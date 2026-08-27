namespace MaterialClient.Common.Services;

/// <summary>
///     Optional Urban-only facade for Xiaoshan upload config (server is authority; no local cache table).
/// </summary>
public interface IXiaoshanUploadConfigClientFacade
{
    Task<XiaoshanUploadConfigSnapshot> GetFromServerAsync(CancellationToken cancellationToken = default);

    Task<XiaoshanUploadConfigSyncPushResult> PushToServerAsync(
        XiaoshanUploadConfigDraft draft,
        CancellationToken cancellationToken = default);
}

public record XiaoshanUploadConfigSnapshot(
    string? DisplayName,
    string? Remark,
    string ModesJson,
    string SettingsJson,
    long ConfigVersion = 0)
{
    /// <summary>True when UrbanManagement has a persisted authoritative row.</summary>
    public bool HasServerRow => ConfigVersion > 0;
}

public record XiaoshanUploadConfigDraft(
    string? DisplayName,
    string? Remark,
    string ModesJson,
    string SettingsJson);

public record XiaoshanUploadConfigSyncPushResult(
    bool Success,
    string? Message,
    XiaoshanUploadConfigSnapshot? Config);
