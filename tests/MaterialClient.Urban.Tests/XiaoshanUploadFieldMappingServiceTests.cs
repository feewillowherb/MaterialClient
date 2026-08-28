using MaterialClient.Common.Models;
using MaterialClient.Urban.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Urban.Tests;

public class XiaoshanUploadFieldMappingServiceTests
{
    private readonly XiaoshanUploadFieldMappingService _sut = new();

    [Fact]
    public void Weighbridge_UsesModeSettingsDataSourceWhenPresent()
    {
        var result = _sut.MapForMode(
            XiaoshanUploadModeNames.Weighbridge,
            XiaoshanUploadSettingsEnvelope.CreateDefault(),
            new XiaoshanUploadModeSettings { DataSource = "CUSTOM_WB", InOutType = "0" },
            new XiaoshanWeighingContext("A12345", "blue", "truck", "1.2", DateTime.UtcNow, ["a.jpg"]));

        result.ResolvedFields["dataSource"].ShouldBe("CUSTOM_WB");
    }

    [Fact]
    public void Weighbridge_UsesDefaultDataSourceWhenModeValueEmpty()
    {
        var result = _sut.MapForMode(
            XiaoshanUploadModeNames.Weighbridge,
            XiaoshanUploadSettingsEnvelope.CreateDefault(),
            new XiaoshanUploadModeSettings { DataSource = "  ", InOutType = "0" },
            new XiaoshanWeighingContext("A12345", "blue", "truck", "1.2", DateTime.UtcNow, ["a.jpg"]));

        result.ResolvedFields["dataSource"].ShouldBe(XiaoshanUploadDefaults.WeighbridgeDataSource);
    }
}
