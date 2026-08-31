using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Vzvision;
using MaterialClient.UI;
using MaterialClient.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.Services;

public interface IMinimalWebHostService : ISingletonDependency, IAsyncDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal in-process web host for Urban diagnostics.
/// NOTE: this service intentionally copies the MinimalWebHost pattern instead of sharing implementation.
/// </summary>
[AutoConstructor]
public partial class MinimalWebHostService : IMinimalWebHostService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinimalWebHostService> _logger;
    private readonly ILocalEventBus _localEventBus;
    private readonly IServiceProvider _serviceProvider;

    private readonly Lock _stateLock = new();
    private WebApplication? _webApplication;
    private bool _isRunning;

    private const string SetScaleWeightApiPath = "/api/scale/weight";
    private const string SetTestPlateApiPath = "/api/lpr/test-plate";
    private const string SetTestPassageApiPath = "/api/lpr/test-passage";
    private const string SettingsApiPath = "/api/settings";
    private const string DeviceOnlineStatusApiPath = "/api/device/online-status";

    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _isRunning;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();
            _webApplication = builder.Build();
            ConfigureEndpoints(_webApplication);

            var urls = ResolveUrls();
            _webApplication.Urls.Add(urls);

            _logger.LogInformation("Urban minimal web host started on {Url}", urls);
            await _webApplication.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _isRunning = false;
            }

            _logger.LogError(ex, "Failed to start urban minimal web host");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        WebApplication? app;

        lock (_stateLock)
        {
            app = _webApplication;
            _webApplication = null;
            _isRunning = false;
        }

        if (app == null)
        {
            return;
        }

        try
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            _logger.LogInformation("Urban minimal web host stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping urban minimal web host");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private string ResolveUrls()
    {
        var urls = _configuration["MinimalWebHost:Urls"];
        if (string.IsNullOrWhiteSpace(urls))
        {
            return "http://localhost:9961";
        }

        urls = urls.Trim();
        if (!urls.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !urls.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            urls = "http://" + urls;
        }

        return urls;
    }

    private void ConfigureEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            service = "MaterialClient.Urban Diagnostic API",
            version = "1.3",
            endpoints = new[]
            {
                $"POST {SetScaleWeightApiPath}",
                $"POST {SetTestPlateApiPath}",
                $"POST {SetTestPassageApiPath}",
                $"GET {SettingsApiPath}",
                $"POST {SettingsApiPath}",
                $"GET {DeviceOnlineStatusApiPath}"
            }
        }));

        app.MapPost(SetScaleWeightApiPath, async (SetWeightRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { success = false, message = "Request body is empty." });
            }

            if (request.Weight < 0)
            {
                return Results.BadRequest(new { success = false, message = "Weight must be non-negative." });
            }

            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();

                if (settings.ScaleSettings.ScaleType != ScaleType.TestMode)
                {
                    return Results.BadRequest(new { success = false, message = "Scale is not in test mode." });
                }

                var preprocessor = _serviceProvider.GetRequiredService<IScaleTestWeightPreprocessorService>();
                preprocessor.Enqueue(request.Weight);

                _logger.LogInformation("Scale test weight injected: {Weight} t", request.Weight);
                return Results.Ok(new { success = true, message = "Ok", weight = request.Weight });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject scale test weight");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        app.MapPost(SetTestPlateApiPath, (SetTestPlateRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { success = false, message = "Request body is empty." });
            }

            var plateNumber = request.PlateNumber?.Trim();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                return Results.BadRequest(new { success = false, message = "Plate number is required." });
            }

            try
            {
                var eventData = new LicensePlateRecognizedEventData
                {
                    PlateNumber = plateNumber,
                    ColorType = request.ColorType,
                    DeviceType = request.DeviceType ?? LprDeviceType.Hikvision,
                    DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? "TestApi" : request.DeviceName.Trim(),
                    Timestamp = request.Timestamp ?? DateTime.Now,
                    PlateColor = request.PlateColor,
                    VehicleType = request.VehicleType,
                    LprImagePath = request.LprImagePath
                };

                _ = _localEventBus.PublishAsync(eventData);
                _logger.LogInformation(
                    "Test plate injected: Plate={Plate}, DeviceType={DeviceType}, DeviceName={DeviceName}",
                    eventData.PlateNumber, eventData.DeviceType, eventData.DeviceName);

                return Results.Ok(new
                {
                    success = true,
                    message = "Ok",
                    plateNumber = eventData.PlateNumber,
                    deviceType = eventData.DeviceType.ToString(),
                    deviceName = eventData.DeviceName,
                    colorType = eventData.ColorType?.ToString(),
                    timestamp = eventData.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject test plate");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        // POST /api/lpr/test-passage — publish LicensePlateRecognizedEventData for Checkpoint / FinishedProduct
        app.MapPost(SetTestPassageApiPath, async (SetTestPassageRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { success = false, message = "Request body is empty." });
            }

            var plateNumber = request.PlateNumber?.Trim();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                return Results.BadRequest(new { success = false, message = "Plate number is required." });
            }

            if (!TryParsePassageSiteType(request.SiteType, out var siteType, out var siteError))
            {
                return Results.BadRequest(new { success = false, message = siteError });
            }

            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                var configs = settings.LicensePlateRecognitionConfigs ?? [];

                LicensePlateRecognitionConfig? matched = null;
                if (!string.IsNullOrWhiteSpace(request.DeviceName))
                {
                    matched = LicensePlateRecognitionConfig.FindByDeviceName(configs, request.DeviceName.Trim());
                    if (matched is null)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message =
                                $"No LPR config named '{request.DeviceName.Trim()}'. Use GET/POST {SettingsApiPath}."
                        });
                    }

                    if (matched.SiteType != siteType)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message =
                                $"Device '{matched.Name}' SiteType is {matched.SiteType}, expected {siteType}."
                        });
                    }
                }
                else
                {
                    matched = configs.FirstOrDefault(c => c.SiteType == siteType);
                    if (matched is null)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message =
                                $"No LPR config with SiteType={siteType}. Create one via POST {SettingsApiPath}."
                        });
                    }
                }

                var eventData = new LicensePlateRecognizedEventData
                {
                    PlateNumber = plateNumber,
                    ColorType = request.ColorType,
                    DeviceType = request.DeviceType ?? matched.ResolvedDeviceType,
                    DeviceName = matched.Name,
                    Timestamp = request.Timestamp ?? DateTime.Now,
                    PlateColor = request.PlateColor,
                    VehicleType = request.VehicleType,
                    LprImagePath = string.IsNullOrWhiteSpace(request.LprImagePath)
                        ? null
                        : request.LprImagePath.Trim()
                };

                await _localEventBus.PublishAsync(eventData);
                _logger.LogInformation(
                    "Test passage plate injected: Plate={Plate}, DeviceName={DeviceName}, SiteType={SiteType}",
                    eventData.PlateNumber, eventData.DeviceName, siteType);

                return Results.Ok(new
                {
                    success = true,
                    message = "Ok",
                    published = true,
                    eventType = nameof(LicensePlateRecognizedEventData),
                    plateNumber = eventData.PlateNumber,
                    deviceName = eventData.DeviceName,
                    deviceType = eventData.DeviceType.ToString(),
                    siteType = siteType.ToString(),
                    plateColor = eventData.PlateColor,
                    vehicleType = eventData.VehicleType,
                    timestamp = eventData.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject test passage plate");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        // GET /api/settings — full Settings payload
        app.MapGet(SettingsApiPath, async () =>
        {
            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                return Results.Ok(new
                {
                    success = true,
                    settings = SettingsPayload.FromEntity(settings)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get settings");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        // POST /api/settings — replace full settings (no PUT)
        app.MapPost(SettingsApiPath, async (SettingsPayload? payload) =>
        {
            if (payload == null)
            {
                return Results.BadRequest(new { success = false, message = "Request body is empty." });
            }

            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                payload.ApplyTo(settings);
                await settingsService.SaveSettingsAsync(settings);
                await _localEventBus.PublishAsync(new SettingsSavedEventData());

                _logger.LogInformation("Full settings saved via diagnostic API");
                return Results.Ok(new
                {
                    success = true,
                    message = "Ok",
                    settings = SettingsPayload.FromEntity(settings)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        app.MapGet(DeviceOnlineStatusApiPath, () =>
        {
            try
            {
                var tracker = _serviceProvider.GetService<SharedDeviceStatusTracker>();
                if (tracker == null)
                {
                    return Results.Ok(new
                    {
                        devices = BuildDegradedDeviceStatuses()
                    });
                }

                var currentStatuses = tracker.GetCurrentStatuses();
                var devices = currentStatuses
                    .Where(s => DeviceStatusCatalog.TryMapToServerDeviceType(s.Name, out _))
                    .Select(s =>
                    {
                        DeviceStatusCatalog.TryMapToServerDeviceType(s.Name, out var deviceType);
                        return new
                        {
                            deviceType,
                            isOnline = s.IsOnline,
                            deviceName = s.Name
                        };
                    })
                    .ToList();

                var coveredTypes = devices.Select(d => d.deviceType).ToHashSet();
                var requiredTypes = new[] { "Scale", "Camera", "Lpr", "Printer" };
                foreach (var requiredType in requiredTypes)
                {
                    if (!coveredTypes.Contains(requiredType))
                    {
                        var displayName = requiredType switch
                        {
                            "Scale" => DeviceStatusCatalog.ScaleName,
                            "Camera" => DeviceStatusCatalog.CameraName,
                            "Lpr" => DeviceStatusCatalog.LprName,
                            "Printer" => DeviceStatusCatalog.PrinterName,
                            _ => requiredType
                        };

                        devices.Add(new
                        {
                            deviceType = requiredType,
                            isOnline = false,
                            deviceName = displayName
                        });
                    }
                }

                return Results.Ok(new { devices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query device online status");
                return Results.Ok(new
                {
                    devices = BuildDegradedDeviceStatuses()
                });
            }
        });
    }

    private static bool TryParsePassageSiteType(
        string? raw,
        out LprSiteType siteType,
        out string? error)
    {
        siteType = default;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "siteType is required. Use Checkpoint or FinishedProduct.";
            return false;
        }

        var value = raw.Trim();
        if (value.Equals("Scale", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Weighbridge", StringComparison.OrdinalIgnoreCase))
        {
            error = "siteType Scale is not allowed on /api/lpr/test-passage.";
            return false;
        }

        if (value.Equals("Gate", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PassageSource.Checkpoint", StringComparison.OrdinalIgnoreCase))
        {
            siteType = LprSiteType.Checkpoint;
            return true;
        }

        if (value.Equals("Product", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PassageSource.FinishedProduct", StringComparison.OrdinalIgnoreCase))
        {
            siteType = LprSiteType.FinishedProduct;
            return true;
        }

        if (Enum.TryParse(value, ignoreCase: true, out siteType) &&
            siteType is LprSiteType.Checkpoint or LprSiteType.FinishedProduct)
        {
            return true;
        }

        if (int.TryParse(value, out var numeric) &&
            Enum.IsDefined(typeof(LprSiteType), numeric))
        {
            siteType = (LprSiteType)numeric;
            if (siteType is LprSiteType.Checkpoint or LprSiteType.FinishedProduct)
            {
                return true;
            }
        }

        error = "siteType must be Checkpoint or FinishedProduct.";
        return false;
    }

    private static object[] BuildDegradedDeviceStatuses() =>
    [
        new { deviceType = "Scale", isOnline = false, deviceName = DeviceStatusCatalog.ScaleName },
        new { deviceType = "Camera", isOnline = false, deviceName = DeviceStatusCatalog.CameraName },
        new { deviceType = "Lpr", isOnline = false, deviceName = DeviceStatusCatalog.LprName },
        new { deviceType = "Printer", isOnline = false, deviceName = DeviceStatusCatalog.PrinterName }
    ];

    private record SetWeightRequest(decimal Weight);

    private record SetTestPlateRequest(
        string? PlateNumber,
        LprDeviceType? DeviceType,
        string? DeviceName,
        VzvisionColorType? ColorType,
        string? PlateColor,
        string? VehicleType,
        string? LprImagePath,
        DateTime? Timestamp);

    /// <summary>
    /// Publishes <see cref="LicensePlateRecognizedEventData"/> for Checkpoint / FinishedProduct testing.
    /// </summary>
    private record SetTestPassageRequest(
        string? SiteType,
        string? PlateNumber,
        string? DeviceName,
        LprDeviceType? DeviceType,
        VzvisionColorType? ColorType,
        string? PlateColor,
        string? VehicleType,
        string? LprImagePath,
        DateTime? Timestamp);

    /// <summary>
    /// Typed full-settings body for diagnostic get/save (avoids SettingsEntity *Json columns).
    /// </summary>
    private sealed class SettingsPayload
    {
        public ScaleSettings? ScaleSettings { get; set; }
        public DocumentScannerConfig? DocumentScannerConfig { get; set; }
        public SystemSettings? SystemSettings { get; set; }
        public List<CameraConfig>? CameraConfigs { get; set; }
        public List<LicensePlateRecognitionConfig>? LicensePlateRecognitionConfigs { get; set; }
        public WeighingConfiguration? WeighingConfiguration { get; set; }
        public SoundDeviceSettings? SoundDeviceSettings { get; set; }
        public UrbanSettings? UrbanSettings { get; set; }

        public static SettingsPayload FromEntity(SettingsEntity entity) => new()
        {
            ScaleSettings = entity.ScaleSettings,
            DocumentScannerConfig = entity.DocumentScannerConfig,
            SystemSettings = entity.SystemSettings,
            CameraConfigs = entity.CameraConfigs,
            LicensePlateRecognitionConfigs = entity.LicensePlateRecognitionConfigs,
            WeighingConfiguration = entity.WeighingConfiguration,
            SoundDeviceSettings = entity.SoundDeviceSettings,
            UrbanSettings = entity.UrbanSettings
        };

        public void ApplyTo(SettingsEntity entity)
        {
            entity.ScaleSettings = ScaleSettings ?? new ScaleSettings();
            entity.DocumentScannerConfig = DocumentScannerConfig ?? new DocumentScannerConfig();
            entity.SystemSettings = SystemSettings ?? new SystemSettings();
            entity.CameraConfigs = CameraConfigs ?? [];
            var lpr = LicensePlateRecognitionConfigs ?? [];
            foreach (var config in lpr)
            {
                config.ApplyVendorDefaults();
            }

            entity.LicensePlateRecognitionConfigs = lpr;
            entity.SystemSettings.EchoLegacyLprDeviceType(lpr);
            entity.WeighingConfiguration = WeighingConfiguration ?? new WeighingConfiguration();
            entity.SoundDeviceSettings = SoundDeviceSettings ?? new SoundDeviceSettings();
            entity.UrbanSettings = UrbanSettings ?? new UrbanSettings();
        }
    }
}
