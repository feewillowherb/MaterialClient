using MaterialClient.Common.Configuration;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Services;

public class XiaoshanUploadSettingsFormMapperTests
{
    private readonly XiaoshanUploadSettingsFormMapper _mapper = new();

    [Fact]
    public void ApplyToForm_EmptyJson_DefaultsWeighbridgeEnabled()
    {
        var form = _mapper.ApplyToForm(new XiaoshanUploadLocalConfig { ModesJson = "{}" });

        form.WeighbridgeEnabled.ShouldBeTrue();
        form.GateEnabled.ShouldBeFalse();
        form.ProductEnabled.ShouldBeFalse();
    }

    [Fact]
    public void TryCreateModesJson_RoundTripsUiModeFieldsOnly()
    {
        var form = new XiaoshanUploadSettingsFormState(
            true, true, false, 1, 1, 1, 0, 0);

        var persist = _mapper.TryCreateModesJson(form);
        persist.Success.ShouldBeTrue();

        var applied = _mapper.ApplyToForm(persist.ModesJson);

        applied.WeighbridgeEnabled.ShouldBeTrue();
        applied.GateEnabled.ShouldBeTrue();
        applied.ProductEnabled.ShouldBeFalse();
        applied.WbInOutIndex.ShouldBe(1);
        applied.GateDeviceIndex.ShouldBe(1);
        applied.GateSiteIndex.ShouldBe(1);
    }
}
