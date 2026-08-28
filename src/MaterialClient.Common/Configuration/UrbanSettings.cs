namespace MaterialClient.Common.Configuration;

/// <summary>
///     Aggregated Urban-mode settings persisted in <c>Settings.UrbanSettingsJson</c>.
/// </summary>
public class UrbanSettings
{
    /// <summary>
    ///     Local mirror of Xiaoshan upload configuration (server remains authority on sync).
    /// </summary>
    public XiaoshanUploadLocalConfig XiaoshanUpload { get; set; } = new();
}

/// <summary>
///     Client-side Xiaoshan upload config snapshot stored under <see cref="UrbanSettings"/>.
/// </summary>
public class XiaoshanUploadLocalConfig
{
    public string ModesJson { get; set; } = "{}";

    public string SettingsJson { get; set; } = "{}";
}
