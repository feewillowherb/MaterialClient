using System.Text.Json;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Services;

public class XiaoshanUploadSettingsFormExtensionsTests
{
    [Fact]
    public void FromLocalConfig_EmptyEnabledModes_DefaultsWeighbridgeEnabled()
    {
        var form = XiaoshanUploadSettingsFormState.FromLocalConfig(new XiaoshanUploadLocalConfig
        {
            EnabledModes = []
        });

        form.WeighbridgeEnabled.ShouldBeTrue();
        form.GateEnabled.ShouldBeFalse();
        form.ProductEnabled.ShouldBeFalse();
    }

    [Fact]
    public void ToLocalConfig_RoundTripsUiModeFields()
    {
        var form = new XiaoshanUploadSettingsFormState(
            true,
            true,
            false,
            UrbanInOutType.Exit,
            UrbanInOutType.Exit,
            UrbanSiteType.Disposal,
            UrbanInOutType.Enter,
            UrbanSiteType.Construction);

        var local = form.ToLocalConfig();
        var applied = XiaoshanUploadSettingsFormState.FromLocalConfig(local);

        applied.WeighbridgeEnabled.ShouldBeTrue();
        applied.GateEnabled.ShouldBeTrue();
        applied.ProductEnabled.ShouldBeFalse();
        applied.WeighbridgeInOut.ShouldBe(UrbanInOutType.Exit);
        applied.GateInOut.ShouldBe(UrbanInOutType.Exit);
        applied.GateSiteType.ShouldBe(UrbanSiteType.Disposal);
    }

    [Fact]
    public void LocalConfig_Json_UsesEnumMemberNames()
    {
        var json = JsonSerializer.Serialize(new XiaoshanUploadLocalConfig
        {
            EnabledModes = [XiaoshanUploadMode.Gate],
            WeighbridgeInOut = UrbanInOutType.Exit,
            GateInOut = UrbanInOutType.Exit,
            GateSiteType = UrbanSiteType.Disposal
        });

        json.ShouldContain("\"Gate\"");
        json.ShouldContain("\"Exit\"");
        json.ShouldContain("\"Disposal\"");
    }
}
