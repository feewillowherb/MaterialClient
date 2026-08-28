using MaterialClient.Common.Configuration;
using MaterialClient.Common.Models;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

public interface IXiaoshanUploadSettingsFormMapper : ITransientDependency
{
    XiaoshanUploadFormApplyResult ApplyToForm(XiaoshanUploadLocalConfig local);

    XiaoshanUploadFormApplyResult ApplyToForm(
        string? displayName,
        string? remark,
        string modesJson,
        string settingsJson);

    bool HasAtLeastOneEnabledMode(XiaoshanUploadSettingsFormState form);

    XiaoshanUploadFormDraftResult TryCreateDraft(
        XiaoshanUploadSettingsFormState form,
        XiaoshanUploadPreservedStatics preserved);
}

public class XiaoshanUploadSettingsFormMapper : IXiaoshanUploadSettingsFormMapper
{
    public const string ErrorNoEnabledMode = "NoEnabledMode";

    public XiaoshanUploadFormApplyResult ApplyToForm(XiaoshanUploadLocalConfig local) =>
        ApplyToForm(local.DisplayName, local.Remark, local.ModesJson, local.SettingsJson);

    public XiaoshanUploadFormApplyResult ApplyToForm(
        string? displayName,
        string? remark,
        string modesJson,
        string settingsJson)
    {
        var modes = XiaoshanUploadEnvelopeJson.ParseModes(modesJson);
        var settings = XiaoshanUploadEnvelopeJson.ParseSettings(settingsJson);
        var wb = modes.GetSettings(XiaoshanUploadModeNames.Weighbridge);
        var gate = modes.GetSettings(XiaoshanUploadModeNames.Gate);
        var product = modes.GetSettings(XiaoshanUploadModeNames.Product);

        var form = new XiaoshanUploadSettingsFormState(
            WeighbridgeEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Weighbridge),
            GateEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Gate),
            ProductEnabled: modes.IsEnabled(XiaoshanUploadModeNames.Product),
            WbInOutIndex: IndexFromWbInOut(wb.InOutType),
            GateDeviceIndex: IndexFromDeviceId(gate.DeviceId),
            GateSiteIndex: IndexFromSiteType(gate.SiteType),
            ProductDeviceIndex: IndexFromDeviceId(product.DeviceId),
            ProductSiteIndex: IndexFromSiteType(product.SiteType));

        var preserved = new XiaoshanUploadPreservedStatics(
            displayName,
            remark,
            settings.BuildLicenseNo,
            settings.AreaCode,
            settings.SpaceName,
            wb.DataSource);

        return new XiaoshanUploadFormApplyResult(form, preserved);
    }

    public bool HasAtLeastOneEnabledMode(XiaoshanUploadSettingsFormState form) =>
        form.WeighbridgeEnabled || form.GateEnabled || form.ProductEnabled;

    public XiaoshanUploadFormDraftResult TryCreateDraft(
        XiaoshanUploadSettingsFormState form,
        XiaoshanUploadPreservedStatics preserved)
    {
        if (!HasAtLeastOneEnabledMode(form))
        {
            return new XiaoshanUploadFormDraftResult(false, ErrorNoEnabledMode, null);
        }

        var enabled = new List<string>();
        if (form.WeighbridgeEnabled) enabled.Add(XiaoshanUploadModeNames.Weighbridge);
        if (form.GateEnabled) enabled.Add(XiaoshanUploadModeNames.Gate);
        if (form.ProductEnabled) enabled.Add(XiaoshanUploadModeNames.Product);

        var dataSource = string.IsNullOrWhiteSpace(preserved.WeighbridgeDataSource)
            ? XiaoshanUploadDefaults.WeighbridgeDataSource
            : preserved.WeighbridgeDataSource;

        var modes = new XiaoshanUploadModesEnvelope
        {
            EnabledModes = enabled,
            ModeSettings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase)
            {
                [XiaoshanUploadModeNames.Weighbridge] = new()
                {
                    InOutType = WireFromWbInOutIndex(form.WbInOutIndex),
                    DataSource = dataSource
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

        var settings = new XiaoshanUploadSettingsEnvelope
        {
            BuildLicenseNo = preserved.BuildLicenseNo,
            AreaCode = preserved.AreaCode,
            SpaceName = preserved.SpaceName
        };

        var draft = new XiaoshanUploadConfigDraft(
            preserved.DisplayName,
            preserved.Remark,
            XiaoshanUploadEnvelopeJson.SerializeModes(modes),
            XiaoshanUploadEnvelopeJson.SerializeSettings(settings));

        return new XiaoshanUploadFormDraftResult(true, null, draft);
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
