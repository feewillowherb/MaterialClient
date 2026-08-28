namespace MaterialClient.Common.Configuration;

/// <summary>
///     Aggregated Urban-mode settings persisted in <c>Settings.UrbanSettingsJson</c>.
/// </summary>
public class UrbanSettings
{
    /// <summary>
    ///     Local Xiaoshan mode settings persisted with system settings (not synced to UrbanManagement).
    /// </summary>
    public XiaoshanUploadLocalConfig XiaoshanUpload { get; set; } = new();
}

/// <summary>
///     Client-side Xiaoshan mode envelope stored under <see cref="UrbanSettings"/>.
/// </summary>
public class XiaoshanUploadLocalConfig
{
    public string ModesJson { get; set; } = "{}";
}
