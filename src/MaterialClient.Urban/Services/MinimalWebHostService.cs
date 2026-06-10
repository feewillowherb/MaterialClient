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
using Volo.Abp.EventBus.Local;
using Volo.Abp.DependencyInjection;

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

    private static string SetScaleWeightApiPath = "/api/scale/weight";
    private static string SetTestPlateApiPath = "/api/lpr/test-plate";
    private static string DeviceOnlineStatusApiPath = "/api/device/online-status";

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

            var urls = _configuration["UrbanWebHost:Urls"];
            if (string.IsNullOrWhiteSpace(urls))
            {
                urls = "http://localhost:9961";
            }
            else
            {
                urls = urls.Trim();
                if (!urls.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !urls.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    urls = "http://" + urls;
                }
            }

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

    private void ConfigureEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            service = "MaterialClient.Urban Diagnostic API",
            version = "1.0",
            endpoints = new[] { SetScaleWeightApiPath, SetTestPlateApiPath, DeviceOnlineStatusApiPath }
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
                    Timestamp = request.Timestamp ?? DateTime.Now
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

        // GET /api/device/online-status — Device online status query API
        app.MapGet(DeviceOnlineStatusApiPath, () =>
        {
            try
            {
                var tracker = _serviceProvider.GetService<SharedDeviceStatusTracker>();
                if (tracker == null)
                {
                    // Task 5.5: Graceful degradation — return isOnline: false for all device types
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

                // Task 5.4: Ensure all required device types are covered
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
                // Task 5.5: Graceful degradation — return HTTP 200 with all isOnline: false
                return Results.Ok(new
                {
                    devices = BuildDegradedDeviceStatuses()
                });
            }
        });
    }

    /// <summary>
    /// Builds a degraded device status response with all devices offline.
    /// Task 5.5: Returns HTTP 200 with isOnline: false instead of 5xx errors.
    /// </summary>
    private static object[] BuildDegradedDeviceStatuses()
    {
        return new object[]
        {
            new { deviceType = "Scale", isOnline = false, deviceName = DeviceStatusCatalog.ScaleName },
            new { deviceType = "Camera", isOnline = false, deviceName = DeviceStatusCatalog.CameraName },
            new { deviceType = "Lpr", isOnline = false, deviceName = DeviceStatusCatalog.LprName },
            new { deviceType = "Printer", isOnline = false, deviceName = DeviceStatusCatalog.PrinterName }
        };
    }

    private record SetWeightRequest(decimal Weight);

    private record SetTestPlateRequest(
        string? PlateNumber,
        LprDeviceType? DeviceType,
        string? DeviceName,
        VzvisionColorType? ColorType,
        DateTime? Timestamp);
}