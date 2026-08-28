using MaterialClient.Common.Entities.Enums;

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
///     Client-side Xiaoshan mode settings stored under <see cref="UrbanSettings"/>.
/// </summary>
public class XiaoshanUploadLocalConfig
{
    public List<XiaoshanUploadMode> EnabledModes { get; set; } = [XiaoshanUploadMode.Weighbridge];

    public UrbanInOutType WeighbridgeInOut { get; set; } = UrbanInOutType.Enter;

    public UrbanInOutType GateInOut { get; set; } = UrbanInOutType.Enter;

    public UrbanSiteType GateSiteType { get; set; } = UrbanSiteType.Construction;

    public UrbanInOutType ProductInOut { get; set; } = UrbanInOutType.Enter;

    public UrbanSiteType ProductSiteType { get; set; } = UrbanSiteType.Construction;
}
