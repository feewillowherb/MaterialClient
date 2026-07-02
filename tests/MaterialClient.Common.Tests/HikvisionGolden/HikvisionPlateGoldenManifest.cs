using System.Text.Json.Serialization;

namespace MaterialClient.Common.Tests.HikvisionGolden;

/// <summary>
///     Golden binary 夹具清单（TestData/HikvisionGolden/manifest.json）。
/// </summary>
public sealed class HikvisionPlateGoldenManifest
{
    public const string ManifestFileName = "manifest.json";

    [JsonPropertyName("fixtures")]
    public List<HikvisionPlateGoldenFixtureEntry> Fixtures { get; set; } = [];
}

public sealed class HikvisionPlateGoldenFixtureEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = string.Empty;

    [JsonPropertyName("lCommand")]
    public int LCommand { get; set; }

    [JsonPropertyName("deviceIp")]
    public string DeviceIp { get; set; } = string.Empty;

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("expectedPlate")]
    public string ExpectedPlate { get; set; } = string.Empty;

    [JsonPropertyName("expectedPlateColor")]
    public string? ExpectedPlateColor { get; set; }

    [JsonPropertyName("expectedVehicleColor")]
    public string? ExpectedVehicleColor { get; set; }

    [JsonPropertyName("expectedVehicleType")]
    public string? ExpectedVehicleType { get; set; }

    [JsonPropertyName("alarmerFile")]
    public string AlarmerFile { get; set; } = "alarmer.bin";

    [JsonPropertyName("alarmInfoFile")]
    public string AlarmInfoFile { get; set; } = "plate_result.bin";

    [JsonPropertyName("imageFile")]
    public string? ImageFile { get; set; }

    [JsonPropertyName("imageBinding")]
    public HikvisionPlateGoldenImageBinding? ImageBinding { get; set; }
}

public sealed class HikvisionPlateGoldenImageBinding
{
    /// <summary>
    ///     upload：NET_DVR_PLATE_RESULT.pBuffer1；its：NET_ITS_PLATE_RESULT.struPicInfo[picIndex]
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("picIndex")]
    public int PicIndex { get; set; }
}
