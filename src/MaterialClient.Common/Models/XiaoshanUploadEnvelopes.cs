using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

public static class XiaoshanUploadModeNames
{
    public const string Weighbridge = "Weighbridge";
    public const string Gate = "Gate";
    public const string Product = "Product";

    public static readonly IReadOnlyList<string> All = [Weighbridge, Gate, Product];
}

public static class XiaoshanUploadDefaults
{
    public const string WeighbridgeDataSource = "WEIGHBRIDGE_XIAOSHAN";

    public const string WbInOutEnter = "0";
    public const string WbInOutExit = "1";

    public const string DeviceIdEnter = "01";
    public const string DeviceIdExit = "02";

    public const string SiteTypeConstruction = "1";
    public const string SiteTypeDisposal = "2";
}

public record XiaoshanUploadModeSettings
{
    public string? DeviceId { get; init; }
    public string? SiteType { get; init; }
    public string? InOutType { get; init; }
    public string? DataSource { get; init; }
}

public record XiaoshanUploadModesEnvelope
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("enabledModes")]
    public List<string> EnabledModes { get; init; } = [XiaoshanUploadModeNames.Weighbridge];

    [JsonPropertyName("modeSettings")]
    public Dictionary<string, XiaoshanUploadModeSettings> ModeSettings { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static XiaoshanUploadModesEnvelope CreateDefault() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        EnabledModes = [XiaoshanUploadModeNames.Weighbridge],
        ModeSettings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase)
        {
            [XiaoshanUploadModeNames.Weighbridge] = new()
            {
                DataSource = XiaoshanUploadDefaults.WeighbridgeDataSource
            },
            [XiaoshanUploadModeNames.Gate] = new(),
            [XiaoshanUploadModeNames.Product] = new()
        }
    };

    public XiaoshanUploadModeSettings GetSettings(string mode) =>
        ModeSettings.GetValueOrDefault(mode) ?? new XiaoshanUploadModeSettings();

    public bool IsEnabled(string mode) =>
        EnabledModes.Exists(m => string.Equals(m, mode, StringComparison.OrdinalIgnoreCase));
}

public static class XiaoshanUploadEnvelopeJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static XiaoshanUploadModesEnvelope ParseModes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() is "{}" or "null")
        {
            return XiaoshanUploadModesEnvelope.CreateDefault();
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<XiaoshanUploadModesEnvelope>(json, SerializerOptions)
                           ?? XiaoshanUploadModesEnvelope.CreateDefault();
            return MaterializeModes(envelope);
        }
        catch (JsonException)
        {
            return XiaoshanUploadModesEnvelope.CreateDefault();
        }
    }

    public static string SerializeModes(XiaoshanUploadModesEnvelope envelope) =>
        JsonSerializer.Serialize(MaterializeModes(envelope), SerializerOptions);

    public static XiaoshanUploadModesEnvelope MaterializeModes(XiaoshanUploadModesEnvelope envelope)
    {
        var defaults = XiaoshanUploadModesEnvelope.CreateDefault();
        var enabled = envelope.EnabledModes.Count == 0
            ? defaults.EnabledModes
            : envelope.EnabledModes
                .Where(m => XiaoshanUploadModeNames.All.Contains(m, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (enabled.Count == 0)
        {
            enabled = [XiaoshanUploadModeNames.Weighbridge];
        }

        var settings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase);
        // STJ deserializes Dictionary without OrdinalIgnoreCase; normalize keys first.
        var incoming = new Dictionary<string, XiaoshanUploadModeSettings>(
            envelope.ModeSettings,
            StringComparer.OrdinalIgnoreCase);

        foreach (var mode in XiaoshanUploadModeNames.All)
        {
            if (incoming.TryGetValue(mode, out var configured))
            {
                settings[mode] = configured;
            }
            else if (defaults.ModeSettings.TryGetValue(mode, out var fallback))
            {
                settings[mode] = fallback;
            }
            else
            {
                settings[mode] = new XiaoshanUploadModeSettings();
            }
        }

        return envelope with
        {
            SchemaVersion = XiaoshanUploadModesEnvelope.CurrentSchemaVersion,
            EnabledModes = enabled,
            ModeSettings = settings
        };
    }
}
