namespace MaterialClient.Common.Events;

/// <summary>
///     Request to push Xiaoshan upload config to the server (Urban LocalEvent handler).
/// </summary>
public sealed class XiaoshanUploadConfigSaveRequestedEventData
{
    public required string ModesJson { get; init; }

    public required string SettingsJson { get; init; }

    public TaskCompletionSource<XiaoshanUploadConfigSyncResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
///     Outcome of a Xiaoshan upload config push (server is authority).
/// </summary>
public record XiaoshanUploadConfigSyncResult(
    bool Success,
    string? Message,
    string ModesJson,
    string SettingsJson,
    long ConfigVersion = 0)
{
    public bool HasServerRow => ConfigVersion > 0;
}
