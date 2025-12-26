using System.Text.Json;
using System.Text.Json.Serialization;
using MaterialClient.Common.Configuration;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     System settings entity
/// </summary>
public class SettingsEntity : Entity<int>
{
    /// <summary>
    ///     Constructor for EF Core
    /// </summary>
    protected SettingsEntity()
    {
        ScaleSettingsJson = string.Empty;
        DocumentScannerConfigJson = string.Empty;
        SystemSettingsJson = string.Empty;
        CameraConfigsJson = string.Empty;
        LicensePlateRecognitionConfigsJson = string.Empty;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    public SettingsEntity(
        ScaleSettings scaleSettings,
        DocumentScannerConfig documentScannerConfig,
        SystemSettings systemSettings,
        List<CameraConfig> cameraConfigs,
        List<LicensePlateRecognitionConfig> licensePlateRecognitionConfigs)
    {
        ScaleSettings = scaleSettings;
        DocumentScannerConfig = documentScannerConfig;
        SystemSettings = systemSettings;
        CameraConfigs = cameraConfigs;
        LicensePlateRecognitionConfigs = licensePlateRecognitionConfigs;
    }

    /// <summary>
    ///     Scale settings JSON (serialized)
    /// </summary>
    public string ScaleSettingsJson { get; set; } = string.Empty;

    /// <summary>
    ///     Document scanner config JSON (serialized)
    /// </summary>
    public string DocumentScannerConfigJson { get; set; } = string.Empty;

    /// <summary>
    ///     System settings JSON (serialized)
    /// </summary>
    public string SystemSettingsJson { get; set; } = string.Empty;

    /// <summary>
    ///     Camera configs JSON (serialized list)
    /// </summary>
    public string CameraConfigsJson { get; set; } = string.Empty;

    /// <summary>
    ///     License plate recognition configs JSON (serialized list)
    /// </summary>
    public string LicensePlateRecognitionConfigsJson { get; set; } = string.Empty;

    /// <summary>
    ///     Scale settings (deserialized)
    /// </summary>
    [JsonIgnore]
    public ScaleSettings ScaleSettings
    {
        get
        {
            if (string.IsNullOrEmpty(ScaleSettingsJson))
                return new ScaleSettings();

            try
            {
                return JsonSerializer.Deserialize<ScaleSettings>(ScaleSettingsJson) ?? new ScaleSettings();
            }
            catch
            {
                return new ScaleSettings();
            }
        }
        set => ScaleSettingsJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     Document scanner config (deserialized)
    /// </summary>
    [JsonIgnore]
    public DocumentScannerConfig DocumentScannerConfig
    {
        get
        {
            if (string.IsNullOrEmpty(DocumentScannerConfigJson))
                return new DocumentScannerConfig();

            try
            {
                return JsonSerializer.Deserialize<DocumentScannerConfig>(DocumentScannerConfigJson) ??
                       new DocumentScannerConfig();
            }
            catch
            {
                return new DocumentScannerConfig();
            }
        }
        set => DocumentScannerConfigJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     System settings (deserialized)
    /// </summary>
    [JsonIgnore]
    public SystemSettings SystemSettings
    {
        get
        {
            if (string.IsNullOrEmpty(SystemSettingsJson))
                return new SystemSettings();

            try
            {
                return JsonSerializer.Deserialize<SystemSettings>(SystemSettingsJson) ?? new SystemSettings();
            }
            catch
            {
                return new SystemSettings();
            }
        }
        set => SystemSettingsJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     Camera configs (deserialized list)
    /// </summary>
    [JsonIgnore]
    public List<CameraConfig> CameraConfigs
    {
        get
        {
            if (string.IsNullOrEmpty(CameraConfigsJson))
                return new List<CameraConfig>();

            try
            {
                return JsonSerializer.Deserialize<List<CameraConfig>>(CameraConfigsJson) ?? new List<CameraConfig>();
            }
            catch
            {
                return new List<CameraConfig>();
            }
        }
        set => CameraConfigsJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     License plate recognition configs (deserialized list)
    /// </summary>
    [JsonIgnore]
    public List<LicensePlateRecognitionConfig> LicensePlateRecognitionConfigs
    {
        get
        {
            if (string.IsNullOrEmpty(LicensePlateRecognitionConfigsJson))
                return new List<LicensePlateRecognitionConfig>();

            try
            {
                return JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(
                    LicensePlateRecognitionConfigsJson) ?? new List<LicensePlateRecognitionConfig>();
            }
            catch
            {
                return new List<LicensePlateRecognitionConfig>();
            }
        }
        set => LicensePlateRecognitionConfigsJson = JsonSerializer.Serialize(value);
    }
}