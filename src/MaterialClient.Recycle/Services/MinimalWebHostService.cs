using System;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Recycle.Services;

public interface IMinimalWebHostService : ISingletonDependency, IAsyncDisposable
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Recycle 进程内诊断 Web Host，提供地磅测试重量注入与车牌测试注入接口。
///     与 Urban/主项目保持相同的 MinimalWebHost 配置节约定：
///     <c>MinimalWebHost:Urls</c>、<c>MinimalWebHost:EnableOnStartup</c>。
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

    private const string DefaultUrls = "http://localhost:9960";
    private const string SetScaleWeightApiPath = "/api/scale/weight";
    private const string SetTestPlateApiPath = "/api/lpr/test-plate";

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

            _logger.LogInformation("Recycle minimal web host started on {Url}", urls);
            await _webApplication.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _isRunning = false;
            }

            _logger.LogError(ex, "Failed to start recycle minimal web host");
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
            _logger.LogInformation("Recycle minimal web host stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping recycle minimal web host");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    /// <summary>
    ///     解析监听地址，优先级：<see cref="MinimalWebHost:Urls" /> 配置 &gt; 默认值。
    /// </summary>
    private string ResolveUrls()
    {
        var urls = _configuration["MinimalWebHost:Urls"];
        if (string.IsNullOrWhiteSpace(urls))
        {
            return DefaultUrls;
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
            service = "MaterialClient.Recycle Diagnostic API",
            version = "1.0",
            endpoints = new[] { SetScaleWeightApiPath, SetTestPlateApiPath }
        }));

        app.MapPost(SetScaleWeightApiPath, async (SetWeightRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { success = false, message = "请求体为空" });
            }

            if (request.Weight < 0)
            {
                return Results.BadRequest(new { success = false, message = "重量必须为非负数（单位：吨）" });
            }

            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();

                if (settings.ScaleSettings.ScaleType != ScaleType.TestMode)
                {
                    return Results.BadRequest(new { success = false, message = "当前不是地磅测试模式" });
                }

                var preprocessor = _serviceProvider.GetRequiredService<IScaleTestWeightPreprocessorService>();
                preprocessor.Enqueue(request.Weight);

                _logger.LogInformation("地磅测试模式设置重量: {Weight} t", request.Weight);
                return Results.Ok(new { success = true, message = "完成", weight = request.Weight });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "地磅测试模式设置重量失败");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });

        app.MapPost(SetTestPlateApiPath, (SetTestPlateRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { success = false, message = "请求体为空" });
            }

            var plateNumber = request.PlateNumber?.Trim();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                return Results.BadRequest(new { success = false, message = "车牌号不能为空" });
            }

            try
            {
                var eventData = new LicensePlateRecognizedEventData
                {
                    PlateNumber = plateNumber,
                    ColorType = request.ColorType,
                    DeviceType = request.DeviceType ?? LprDeviceType.Huaxiazhixin,
                    DeviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                        ? "TestApi"
                        : request.DeviceName.Trim(),
                    Timestamp = request.Timestamp ?? DateTime.Now
                };

                _ = _localEventBus.PublishAsync(eventData);
                _logger.LogInformation(
                    "测试车牌注入成功：Plate={Plate}, DeviceType={DeviceType}, DeviceName={DeviceName}",
                    eventData.PlateNumber, eventData.DeviceType, eventData.DeviceName);

                return Results.Ok(new
                {
                    success = true,
                    message = "完成",
                    plateNumber = eventData.PlateNumber,
                    deviceType = eventData.DeviceType.ToString(),
                    deviceName = eventData.DeviceName,
                    colorType = eventData.ColorType?.ToString(),
                    timestamp = eventData.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试车牌注入失败");
                return Results.InternalServerError(new { success = false, message = ex.Message });
            }
        });
    }

    private record SetWeightRequest(decimal Weight);

    private record SetTestPlateRequest(
        string? PlateNumber,
        LprDeviceType? DeviceType,
        string? DeviceName,
        VzvisionColorType? ColorType,
        DateTime? Timestamp);
}
