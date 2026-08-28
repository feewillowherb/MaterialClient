using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services;

public record XiaoshanUploadSettingsFormState(
    bool WeighbridgeEnabled,
    bool GateEnabled,
    bool ProductEnabled,
    UrbanInOutType WeighbridgeInOut,
    UrbanInOutType GateInOut,
    UrbanSiteType GateSiteType,
    UrbanInOutType ProductInOut,
    UrbanSiteType ProductSiteType)
{
    public static XiaoshanUploadSettingsFormState FromLocalConfig(XiaoshanUploadLocalConfig local)
    {
        var enabled = local.EnabledModes ?? [];
        var hasAny = enabled.Count > 0;

        return new XiaoshanUploadSettingsFormState(
            WeighbridgeEnabled: !hasAny || enabled.Contains(XiaoshanUploadMode.Weighbridge),
            GateEnabled: hasAny && enabled.Contains(XiaoshanUploadMode.Gate),
            ProductEnabled: hasAny && enabled.Contains(XiaoshanUploadMode.Product),
            WeighbridgeInOut: local.WeighbridgeInOut,
            GateInOut: local.GateInOut,
            GateSiteType: local.GateSiteType,
            ProductInOut: local.ProductInOut,
            ProductSiteType: local.ProductSiteType);
    }
}

public static class XiaoshanUploadSettingsFormExtensions
{
    public static XiaoshanUploadLocalConfig ToLocalConfig(this XiaoshanUploadSettingsFormState form)
    {
        var enabled = new List<XiaoshanUploadMode>();
        if (form.WeighbridgeEnabled) enabled.Add(XiaoshanUploadMode.Weighbridge);
        if (form.GateEnabled) enabled.Add(XiaoshanUploadMode.Gate);
        if (form.ProductEnabled) enabled.Add(XiaoshanUploadMode.Product);

        return new XiaoshanUploadLocalConfig
        {
            EnabledModes = enabled.Count == 0 ? [XiaoshanUploadMode.Weighbridge] : enabled,
            WeighbridgeInOut = form.WeighbridgeInOut,
            GateInOut = form.GateInOut,
            GateSiteType = form.GateSiteType,
            ProductInOut = form.ProductInOut,
            ProductSiteType = form.ProductSiteType
        };
    }
}
