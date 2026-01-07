using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Services;

/// <summary>
///     Web Host 服务，负责启动和管理 Web API 服务
///     与桌面应用共享同一个 ServiceProvider 和 DbContext
/// </summary>
public class MinimalWebHostService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Lock _lock = new();
    private readonly IServiceProvider _sharedServiceProvider;
    private bool _isRunning;
    private WebApplication? _webApplication;
    
    private static string ApiPath ="/api/CarLicense/CallDeviceMessage";
    private static string HuaXiaZhiXingApiPath = "/api/CarLicense/CallDeviceMessageHuaXiaZhiXing";

    /// <summary>
    ///     构造函数，注入共享的服务提供者
    /// </summary>
    /// <param name="serviceProvider">来自桌面应用的共享服务提供者</param>
    /// <param name="configuration">应用配置</param>
    public MinimalWebHostService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _sharedServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    ///     获取 Web Host 运行状态
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

    public async ValueTask DisposeAsync()
    {
        if (_webApplication != null) await StopAsync();
    }

    /// <summary>
    ///     启动 Web Host
    /// </summary>
    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_isRunning) throw new InvalidOperationException("Web Host is already running");

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

            // Configure URLs from SystemSettings
            var settingsService = _sharedServiceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();
            var urls = settings.SystemSettings.Urls;
            if (string.IsNullOrWhiteSpace(urls))
            {
                urls = "http://localhost:9960";
            }
            else
            {
                // 如果没有协议前缀，自动添加 http://
                urls = urls.Trim();
                if (!urls.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !urls.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    urls = "http://" + urls;
                }
            }
            _webApplication.Urls.Add(urls);

            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogInformation("启动 Web 服务于 {Urls}", urls);
            logger?.LogInformation("API 端点: {Urls}{ApiPath}", urls,ApiPath);

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
    ///     停止 Web Host
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
    ///     配置 API 端点
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
                ApiPath,
                HuaXiaZhiXingApiPath
            }
        }));

        // 车牌识别 - 设备回调接口（海康威视）
        app.MapPost(ApiPath, async (HikVisionPlateCallback? callback) =>
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

        // 车牌识别 - 设备回调接口（华夏智信）
        app.MapPost(HuaXiaZhiXingApiPath, async (HttpContext context) =>
        {
            var logger = _sharedServiceProvider.GetRequiredService<ILogger<MinimalWebHostService>>();
            var result = new ResultInfoHuaXiaZhiXing();

            try
            {
                IFormCollection? form = null;

                // 启用缓冲，允许多次读取请求体
                context.Request.EnableBuffering();

                // 优先尝试读取标准表单数据
                if (context.Request.HasFormContentType)
                {
                    try
                    {
                        // 使用 ReadFormAsync 而不是直接访问 Form 属性
                        form = await context.Request.ReadFormAsync();
                    }
                    catch (BadHttpRequestException ex)
                    {
                        // 请求体不完整或格式错误，记录日志并尝试从原始请求体读取
                        logger.LogWarning(ex, "无法读取标准表单数据，尝试从原始请求体解析");
                        form = null;
                    }
                }

                // 如果标准表单读取失败，尝试从原始请求体解析
                if (form == null || form.Count == 0)
                {
                    context.Request.Body.Position = 0;
                    using var reader = new System.IO.StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    var raw = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        // 解析查询字符串格式的原始数据
                        var queryParams = QueryHelpers.ParseQuery(raw);
                        var formDictionary = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
                        foreach (var kvp in queryParams)
                        {
                            formDictionary[kvp.Key] = kvp.Value;
                        }
                        form = new FormCollection(formDictionary);
                    }
                }

                if (form == null || form.Count == 0)
                {
                    result.error_num = -1;
                    result.error_str = "无效的请求";
                    logger.LogWarning("接收到无效的华夏智信请求（无表单数据）");
                    return Results.Json(result);
                }

                var type = form["type"].ToString() ?? string.Empty;
                
                if (type.Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    var plateNum = form["plate_num"].ToString();
                    if (!string.IsNullOrWhiteSpace(plateNum))
                    {
                        var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();
                        weighingService.OnPlateNumberRecognized(plateNum);
                        
                        logger.LogInformation($"华夏智信抓拍车牌号：{plateNum}");
                        
                        // 可选：访问其他字段
                        // var plateColor = form["plate_color"].ToString();
                        // var pictureBase64 = form["picture"].ToString();
                        // var closeupBase64 = form["closeup_pic"].ToString();
                    }
                }
                else if (type.Equals("heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    var camIp = form["cam_ip"].ToString();
                    if (!string.IsNullOrEmpty(camIp))
                    {
                        logger.LogDebug($"华夏智信设备心跳：{camIp}");
                        // 注意：原代码中的 Params 和 onlineTime 更新逻辑需要根据实际需求实现
                        // 这里仅记录日志，如果需要设备状态管理，可以添加相应的服务
                    }
                }

                result.error_num = 0;
                result.error_str = string.Empty;
            }
            catch (BadHttpRequestException ex)
            {
                // 专门处理请求体相关的异常
                result.error_num = -1;
                result.error_str = "请求格式错误";
                logger.LogWarning(ex, "华夏智信设备回调请求格式错误");
            }
            catch (Exception ex)
            {
                result.error_num = -1;
                result.error_str = string.Empty;
                logger.LogError(ex, "处理华夏智信设备回调失败");
            }

            return Results.Json(result);
        });
    }


    #region 华夏智信响应数据模型

    /// <summary>
    ///     华夏智信设备回调响应结果
    /// </summary>
    private class ResultInfoHuaXiaZhiXing
    {
        public int error_num { get; set; }
        public string error_str { get; set; } = string.Empty;
    }

    #endregion

    #region 海康威视车牌识别数据模型

    /// <summary>
    ///     海康威视车牌识别回调数据模型
    /// </summary>
    private record HikVisionPlateCallback(
        [property: JsonPropertyName("AlarmInfoPlate")]
        AlarmInfoPlate? AlarmInfoPlate
    );

    /// <summary>
    ///     报警信息
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
    ///     车牌结果包装
    /// </summary>
    private record PlateResultWrapper(
        [property: JsonPropertyName("PlateResult")]
        PlateResult? PlateResult
    );

    /// <summary>
    ///     车牌结果
    /// </summary>
    private record PlateResult(
        [property: JsonPropertyName("license")]
        string? License
    );

    #endregion
}