using MaterialClient.Common.Configuration;
using MaterialClient.Common.Models;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

public interface IXiaoshanUploadSettingsFormMapper : ITransientDependency
{
    XiaoshanUploadSettingsFormState ApplyToForm(XiaoshanUploadLocalConfig local);

    XiaoshanUploadSettingsFormState ApplyToForm(string modesJson);

    XiaoshanUploadModesPersistResult TryCreateModesJson(XiaoshanUploadSettingsFormState form);
}

public class XiaoshanUploadSettingsFormMapper : IXiaoshanUploadSettingsFormMapper
{
    public XiaoshanUploadSettingsFormState ApplyToForm(XiaoshanUploadLocalConfig local) =>
        ApplyToForm(local.ModesJson);

    public XiaoshanUploadSettingsFormState ApplyToForm(string modesJson)
    {
        var modes = XiaoshanUploadEnvelopeJson.ParseModes(modesJson);
        var wb = modes.GetSettings(XiaoshanUploadModeNames.Weighbridge);
        var gate = modes.GetSettings(XiaoshanUploadModeNames.Gate);
        var product = modes.GetSettings(XiaoshanUploadModeNames.Product);

        return new XiaoshanUploadSettingsFormState(
            WeighbridgeEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Weighbridge),
            GateEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Gate),
            ProductEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Product),
            WbInOutIndex: IndexFromWbInOut(wb.InOutType),
            GateDeviceIndex: IndexFromDeviceId(gate.DeviceId),
            GateSiteIndex: IndexFromSiteType(gate.SiteType),
            ProductDeviceIndex: IndexFromDeviceId(product.DeviceId),
            ProductSiteIndex: IndexFromSiteType(product.SiteType));
    }

    public XiaoshanUploadModesPersistResult TryCreateModesJson(XiaoshanUploadSettingsFormState form)
    {
        var enabled = new List<string>();
        if (form.WeighbridgeEnabled) enabled.Add(XiaoshanUploadModeNames.Weighbridge);
        if (form.GateEnabled) enabled.Add(XiaoshanUploadModeNames.Gate);
        if (form.ProductEnabled) enabled.Add(XiaoshanUploadModeNames.Product);

        var modes = new XiaoshanUploadModesEnvelope
        {
            EnabledModes = enabled.Count == 0 ? [XiaoshanUploadModeNames.Weighbridge] : enabled,
            ModeSettings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase)
            {
                [XiaoshanUploadModeNames.Weighbridge] = new()
                {
                    InOutType = WireFromWbInOutIndex(form.WbInOutIndex),
                    DataSource = XiaoshanUploadDefaults.WeighbridgeDataSource
                },
                [XiaoshanUploadModeNames.Gate] = new()
                {
                    DeviceId = WireFromDeviceIndex(form.GateDeviceIndex),
                    SiteType = WireFromSiteIndex(form.GateSiteIndex)
                },
                [XiaoshanUploadModeNames.Product] = new()
                {
                    DeviceId = WireFromDeviceIndex(form.ProductDeviceIndex),
                    SiteType = WireFromSiteIndex(form.ProductSiteIndex)
                }
            }
        };

        return new XiaoshanUploadModesPersistResult(
            true,
            XiaoshanUploadEnvelopeJson.SerializeModes(modes));
    }

    private static int IndexFromWbInOut(string? value) =>
        string.Equals(value, XiaoshanUploadDefaults.WbInOutExit, StringComparison.Ordinal) ? 1 : 0;

    private static int IndexFromDeviceId(string? value) =>
        string.Equals(value, XiaoshanUploadDefaults.DeviceIdExit, StringComparison.Ordinal) ? 1 : 0;

    private static int IndexFromSiteType(string? value) =>
        string.Equals(value, XiaoshanUploadDefaults.SiteTypeDisposal, StringComparison.Ordinal) ? 1 : 0;

    private static string WireFromWbInOutIndex(int index) =>
        index == 1 ? XiaoshanUploadDefaults.WbInOutExit : XiaoshanUploadDefaults.WbInOutEnter;

    private static string WireFromDeviceIndex(int index) =>
        index == 1 ? XiaoshanUploadDefaults.DeviceIdExit : XiaoshanUploadDefaults.DeviceIdEnter;

    private static string WireFromSiteIndex(int index) =>
        index == 1 ? XiaoshanUploadDefaults.SiteTypeDisposal : XiaoshanUploadDefaults.SiteTypeConstruction;
}
