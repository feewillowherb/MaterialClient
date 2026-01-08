using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BadHttpRequestException = Microsoft.AspNetCore.Http.BadHttpRequestException;

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
    private static string CallDeviceStatusPath = "/api/CarLicense/CallDeviceStatus";

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
                HuaXiaZhiXingApiPath,
                CallDeviceStatusPath
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
                Dictionary<string, string>? form = null;
                string? rawRequestBody = null; // 保存原始请求体用于日志
                string contentType = context.Request.ContentType ?? "unknown";

                // 优先尝试读取标准表单数据（不检查Content-Type，直接尝试读取，与旧代码保持一致）
                // 注意：在 Minimal API 中，需要先调用 EnableBuffering 和 ReadFormAsync 才能访问 Form
                context.Request.EnableBuffering();
                
                IFormCollection? ctxForm = null;
                try
                {
                    // 尝试读取表单（即使Content-Type不是标准的表单类型也尝试）
                    if (context.Request.HasFormContentType)
                    {
                        ctxForm = await context.Request.ReadFormAsync();
                    }
                    else
                    {
                        // 即使Content-Type不是表单类型，也尝试读取（与旧代码行为一致）
                        // 某些设备可能发送错误的Content-Type
                        try
                        {
                            ctxForm = await context.Request.ReadFormAsync();
                        }
                        catch
                        {
                            // 忽略读取失败，继续尝试从原始请求体读取
                        }
                    }
                }
                catch
                {
                    // 忽略读取失败，继续尝试从原始请求体读取
                }

                if (ctxForm != null && ctxForm.Count > 0)
                {
                    // 将 IFormCollection 转换为 Dictionary（值已自动URL解码）
                    form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in ctxForm)
                    {
                        // IFormCollection 的值已经是解码后的
                        form[kvp.Key] = kvp.Value.ToString();
                    }
                }
                else
                {
                    // 如果表单为空，尝试从原始请求体读取（与旧代码逻辑一致）
                    context.Request.Body.Position = 0;
                    using var reader = new System.IO.StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    rawRequestBody = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(rawRequestBody))
                    {
                        // 使用 QueryHelpers.ParseQuery 解析（自动URL解码，与 HttpUtility.ParseQueryString 行为一致）
                        var queryParams = QueryHelpers.ParseQuery(rawRequestBody);
                        form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in queryParams)
                        {
                            // QueryHelpers.ParseQuery 返回的值已经是URL解码后的
                            // 但需要处理多个值的情况（取第一个）
                            var value = kvp.Value.Count > 0 ? kvp.Value[0] : string.Empty;
                            // 如果值仍然是URL编码的，进行解码（双重保险）
                            if (!string.IsNullOrEmpty(value) && value.Contains('%'))
                            {
                                value = WebUtility.UrlDecode(value) ?? string.Empty;
                            }
                            form[kvp.Key] = value ?? string.Empty;
                        }
                    }
                }

                if (form == null || form.Count == 0)
                {
                    result.error_num = -1;
                    result.error_str = "无效的请求";
                    // 打印原始请求信息用于调试
                    var headers = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value.ToArray())}"));
                    logger.LogWarning(
                        "接收到无效的华夏智信请求（无表单数据）。Content-Type: {ContentType}, RawBody: {RawBody}, Headers: {Headers}",
                        contentType,
                        rawRequestBody ?? "empty",
                        headers);
                    return Results.Json(result);
                }

                var type = form.GetValueOrDefault("type") ?? string.Empty;
                logger.LogInformation($"form: type={type}, keys={string.Join(",", form.Keys)}");
                
                if (type.Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    var plateNum = form.GetValueOrDefault("plate_num"); // 已经自动解码（京A12345）
                    if (!string.IsNullOrWhiteSpace(plateNum))
                    {
                        var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();
                        weighingService.OnPlateNumberRecognized(plateNum);
                        
                        logger.LogInformation($"华夏智信抓拍车牌号：{plateNum}");
                        
                        // 可选：访问其他字段
                        // var plateColor = form.GetValueOrDefault("plate_color");
                        // var pictureBase64 = form.GetValueOrDefault("picture");
                        // var closeupBase64 = form.GetValueOrDefault("closeup_pic");
                    }
                }
                else if (type.Equals("heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    var camIp = form.GetValueOrDefault("cam_ip");
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
            catch (Exception ex)
            {
                result.error_num = -1;
                result.error_str = string.Empty;
                
                // 尝试读取原始请求信息用于错误日志
                string? rawBody = null;
                string contentType = context.Request.ContentType ?? "unknown";
                try
                {
                    if (context.Request.Body.CanSeek)
                    {
                        context.Request.Body.Position = 0;
                        using var reader = new System.IO.StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                        rawBody = await reader.ReadToEndAsync();
                        context.Request.Body.Position = 0;
                    }
                }
                catch
                {
                    // 忽略读取错误
                }
                
                var headers = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value.ToArray())}"));
                logger.LogError(ex,
                    "处理华夏智信设备回调失败。Content-Type: {ContentType}, RawBody: {RawBody}, Headers: {Headers}",
                    contentType,
                    rawBody ?? "unable to read",
                    headers);
            }

            return Results.Json(result);
        });

        // LPRAllInOne comet 轮询端点 - 设备状态查询
        // 设备会轮询此端点（GET 或 POST），如果需要触发抓拍，在响应中返回触发消息
        // 根据 cap.md，设备会发送设备注册消息（心跳），包含 ipaddr 字段
        app.MapMethods(CallDeviceStatusPath, new[] { "GET", "POST" }, async (HttpContext context) =>
        {
            var statusLogger = _sharedServiceProvider.GetRequiredService<ILogger<MinimalWebHostService>>();
            
            try
            {
                string? deviceIp = null;
                
                // 首先尝试从查询参数中获取设备IP（GET 请求）
                deviceIp = context.Request.Query["ipaddr"].ToString();
                
                // 如果查询参数中没有，尝试从表单数据中获取（POST 请求，comet 轮询通常使用 POST）
                if (string.IsNullOrWhiteSpace(deviceIp))
                {
                    if (context.Request.HasFormContentType)
                    {
                        try
                        {
                            context.Request.EnableBuffering();
                            var form = await context.Request.ReadFormAsync();
                            deviceIp = form["ipaddr"].ToString();
                        }
                        catch
                        {
                            // 忽略表单读取错误
                        }
                    }
                }

                // 如果仍然没有，尝试从 RemoteIpAddress 获取
                if (string.IsNullOrWhiteSpace(deviceIp))
                {
                    deviceIp = context.Connection.RemoteIpAddress?.ToString();
                }

                if (string.IsNullOrWhiteSpace(deviceIp))
                {
                    statusLogger.LogWarning("Cannot determine device IP from CallDeviceStatus request");
                    return Results.Ok(new
                    {
                        success = true,
                        msg = ""
                    });
                }

                // 检查是否需要触发抓拍
                var lprService = _sharedServiceProvider.GetService<MaterialClient.Common.Services.LPRAllInOne.ILPRAllInOneService>();
                if (lprService != null && lprService.CheckAndClearTriggerFlag(deviceIp))
                {
                    // 需要触发抓拍，返回触发消息
                    // 根据 cap.md (700-711)，返回格式：{"Response_AlarmInfoPlate": {"manualTrigger": "ok"}}
                    statusLogger.LogInformation("Returning manual trigger message for device IP: {Ip}", deviceIp);
                    return Results.Json(new
                    {
                        Response_AlarmInfoPlate = new
                        {
                            manualTrigger = "ok"
                        }
                    });
                }

                // 不需要触发，返回正常响应
                return Results.Ok(new
                {
                    success = true,
                    msg = ""
                });
            }
            catch (Exception ex)
            {
                statusLogger.LogError(ex, "Error processing CallDeviceStatus request");
                return Results.Ok(new
                {
                    success = true,
                    msg = ""
                });
            }
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
