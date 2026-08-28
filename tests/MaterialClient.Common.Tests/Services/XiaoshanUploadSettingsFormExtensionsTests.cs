using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Services;

public class XiaoshanUploadSettingsFormExtensionsTests
{
    [Fact]
    public void ToUrbanConfigForm_EmptyJson_DefaultsWeighbridgeEnabled()
    {
        var form = new XiaoshanUploadLocalConfig { ModesJson = "{}" }.ToUrbanConfigForm();

        form.WeighbridgeEnabled.ShouldBeTrue();
        form.GateEnabled.ShouldBeFalse();
        form.ProductEnabled.ShouldBeFalse();
    }

    [Fact]
    public void ToModesJson_RoundTripsUiModeFieldsOnly()
    {
        var form = new XiaoshanUploadSettingsFormState(
            true, true, false, 1, 1, 1, 0, 0);

        var applied = form.ToModesJson().ToUrbanConfigForm();

        applied.WeighbridgeEnabled.ShouldBeTrue();
        applied.GateEnabled.ShouldBeTrue();
        applied.ProductEnabled.ShouldBeFalse();
        applied.WbInOutIndex.ShouldBe(1);
        applied.GateDeviceIndex.ShouldBe(1);
        applied.GateSiteIndex.ShouldBe(1);
    }
}
