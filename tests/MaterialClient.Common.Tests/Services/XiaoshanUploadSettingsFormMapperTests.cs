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
        var local = new XiaoshanUploadLocalConfig { ModesJson = "{}", SettingsJson = "{}" };

        var result = _mapper.ApplyToForm(local);

        result.Form.WeighbridgeEnabled.ShouldBeTrue();
        result.Form.GateEnabled.ShouldBeFalse();
        result.Form.ProductEnabled.ShouldBeFalse();
        _mapper.HasAtLeastOneEnabledMode(result.Form).ShouldBeTrue();
    }

    [Fact]
    public void TryCreateDraft_AllModesDisabled_Fails()
    {
        var form = new XiaoshanUploadSettingsFormState(
            false, false, false, 0, 0, 0, 0, 0);
        var preserved = new XiaoshanUploadPreservedStatics(null, null, null, null, null, null);

        var result = _mapper.TryCreateDraft(form, preserved);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe(XiaoshanUploadSettingsFormMapper.ErrorNoEnabledMode);
        result.Draft.ShouldBeNull();
    }

    [Fact]
    public void TryCreateDraft_RoundTripsStaticFields()
    {
        var form = new XiaoshanUploadSettingsFormState(
            true, false, false, 0, 0, 0, 0, 0);
        var preserved = new XiaoshanUploadPreservedStatics(
            "Site A",
            "note",
            "330106202212120101",
            "330106",
            "Yard",
            "CUSTOM_DS");

        var draftResult = _mapper.TryCreateDraft(form, preserved);
        draftResult.Success.ShouldBeTrue();
        draftResult.Draft.ShouldNotBeNull();

        var applied = _mapper.ApplyToForm(
            draftResult.Draft!.DisplayName,
            draftResult.Draft.Remark,
            draftResult.Draft.ModesJson,
            draftResult.Draft.SettingsJson);

        applied.Preserved.DisplayName.ShouldBe("Site A");
        applied.Preserved.Remark.ShouldBe("note");
        applied.Preserved.BuildLicenseNo.ShouldBe("330106202212120101");
        applied.Preserved.AreaCode.ShouldBe("330106");
        applied.Preserved.SpaceName.ShouldBe("Yard");
        applied.Preserved.WeighbridgeDataSource.ShouldBe("CUSTOM_DS");
        applied.Form.WeighbridgeEnabled.ShouldBeTrue();
    }
}
