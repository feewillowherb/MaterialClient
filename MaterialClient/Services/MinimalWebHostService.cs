using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Services;

namespace MaterialClient.Services;

/// <summary>
/// Web Host 服务，负责启动和管理 Web API 服务
/// 与桌面应用共享同一个 ServiceProvider 和 DbContext
/// </summary>
public class MinimalWebHostService : IDisposable
{
    private WebApplication? _webApplication;
    private bool _isRunning;
    private readonly Lock _lock = new();
    private readonly IServiceProvider _sharedServiceProvider;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 构造函数，注入共享的服务提供者
    /// </summary>
    /// <param name="serviceProvider">来自桌面应用的共享服务提供者</param>
    /// <param name="configuration">应用配置</param>
    public MinimalWebHostService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _sharedServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// 启动 Web Host
    /// </summary>
    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Web Host is already running");
            }

            _isRunning = true;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();

            // Add ABP with HttpHost module
            builder.Services.AddSingleton(_sharedServiceProvider);

            _webApplication = builder.Build();

            // 配置 API 端点
            ConfigureEndpoints(_webApplication);

            // Configure URLs from appsettings
            var urls = builder.Configuration["Urls"] ?? "http://localhost:9960";
            _webApplication.Urls.Add(urls);

            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogInformation("启动 Web 服务于 {Urls}", urls);
            logger?.LogInformation("API 端点: {Urls}/api/hardware/plate-number", urls);

            // Start the web application
            await _webApplication.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogError(ex, "Web Host 启动失败");
            lock (_lock)
            {
                _isRunning = false;
            }

            throw;
        }
    }

    /// <summary>
    /// 停止 Web Host
    /// </summary>
    public async Task StopAsync()
    {
        if (_webApplication != null)
        {
            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogInformation("正在停止 Web Host...");

            try
            {
                await _webApplication.StopAsync();
                await _webApplication.DisposeAsync();
                _webApplication = null;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "停止 Web Host 时出错");
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }
    }

    /// <summary>
    /// 获取 Web Host 运行状态
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public void Dispose()
    {
        if (_webApplication != null)
        {
            Task.Run(async () => await StopAsync()).Wait();
        }
    }

    /// <summary>
    /// 配置 API 端点
    /// </summary>
    private void ConfigureEndpoints(WebApplication app)
    {
        var logger = _sharedServiceProvider.GetRequiredService<ILogger<MinimalWebHostService>>();

        // 根路由
        app.MapGet("/", () => Results.Ok(new
        {
            service = "MaterialClient API",
            version = "1.0",
            endpoints = new[]
            {
                "/api/hardware/plate-number"
            }
        }));

        // 车牌识别 - 设备回调接口（海康威视）
        app.MapPost("/api/hardware/plate-number", async (HikVisionPlateCallback? callback) =>
        {
            try
            {
                var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();

                // 解析海康设备数据
                var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;

                if (!string.IsNullOrWhiteSpace(license))
                {
                    weighingService.OnPlateNumberRecognized(license);
                    logger.LogInformation(
                        $"接收到车牌识别: {license} (设备: {callback?.AlarmInfoPlate?.DeviceName}, IP: {callback?.AlarmInfoPlate?.IpAddr})");

                    return Results.Ok(new
                    {
                        result = 1,
                        success = true,
                        msg = "完成",
                        data = new { license }
                    });
                }

                logger.LogWarning("接收到无效的车牌数据");
                return Results.BadRequest(new
                {
                    result = 0,
                    success = false,
                    msg = "无效的车牌数据"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "处理车牌识别回调失败");
                return Results.Ok(new
                {
                    result = 1,
                    success = false,
                    msg = ex.Message
                });
            }
        });
    }


    #region 海康威视车牌识别数据模型

    /// <summary>
    /// 海康威视车牌识别回调数据模型
    /// </summary>
    private record HikVisionPlateCallback(
        [property: JsonPropertyName("AlarmInfoPlate")]
        AlarmInfoPlate? AlarmInfoPlate
    );

    /// <summary>
    /// 报警信息
    /// </summary>
    private record AlarmInfoPlate(
        [property: JsonPropertyName("channel")]
        int Channel,
        [property: JsonPropertyName("deviceName")]
        string? DeviceName,
        [property: JsonPropertyName("ipaddr")] string? IpAddr,
        [property: JsonPropertyName("result")] PlateResultWrapper? Result
    );

    /// <summary>
    /// 车牌结果包装
    /// </summary>
    private record PlateResultWrapper(
        [property: JsonPropertyName("PlateResult")]
        PlateResult? PlateResult
    );

    /// <summary>
    /// 车牌结果
    /// </summary>
    private record PlateResult(
        [property: JsonPropertyName("license")]
        string? License
    );

    #endregion
}