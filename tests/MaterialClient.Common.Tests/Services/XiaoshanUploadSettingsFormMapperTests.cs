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
        _mapper.HasAtLeastOneEnabledMode(form).ShouldBeTrue();
    }

    [Fact]
    public void TryCreateDraft_AllModesDisabled_Fails()
    {
        var form = new XiaoshanUploadSettingsFormState(
            false, false, false, 0, 0, 0, 0, 0);

        var result = _mapper.TryCreateDraft(form);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe(XiaoshanUploadSettingsFormMapper.ErrorNoEnabledMode);
        result.Draft.ShouldBeNull();
    }

    [Fact]
    public void TryCreateDraft_RoundTripsUiModeFieldsOnly()
    {
        var form = new XiaoshanUploadSettingsFormState(
            true, true, false, 1, 1, 1, 0, 0);

        var draftResult = _mapper.TryCreateDraft(form);
        draftResult.Success.ShouldBeTrue();
        draftResult.Draft.ShouldNotBeNull();

        var applied = _mapper.ApplyToForm(draftResult.Draft.ModesJson);

        applied.WeighbridgeEnabled.ShouldBeTrue();
        applied.GateEnabled.ShouldBeTrue();
        applied.ProductEnabled.ShouldBeFalse();
        applied.WbInOutIndex.ShouldBe(1);
        applied.GateDeviceIndex.ShouldBe(1);
        applied.GateSiteIndex.ShouldBe(1);
    }
}
