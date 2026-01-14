using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            // Configure immediate shutdown - 立即停止接受新请求，丢弃正在处理的请求
            builder.Host.ConfigureHostOptions(options =>
            {
                options.ShutdownTimeout = TimeSpan.Zero; // 立即关闭，不等待正在处理的请求
            });

            // 移除响应压缩中间件
            // 原因：华夏智信相机不支持 gzip 压缩和分块传输，要求响应包含 Content-Length 头
            // 压缩中间件会自动移除 Content-Length 并使用 Transfer-Encoding: chunked
            // builder.Services.AddResponseCompression(options =>
            // {
            //     options.EnableForHttps = true;
            //     options.Providers.Add<GzipCompressionProvider>();
            //     options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            //     {
            //         "application/json"
            //     });
            // });
            //
            // builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            // {
            //     options.Level = CompressionLevel.Fastest;
            // });

            _webApplication = builder.Build();

            // 不使用响应压缩中间件（华夏智信相机不支持）
            // _webApplication.UseResponseCompression();

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
    ///     立即停止接受新请求，丢弃所有正在处理的请求
    /// </summary>
    public async Task StopAsync()
    {
        if (_webApplication != null)
        {
            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogInformation("正在立即停止 Web Host，丢弃所有正在处理的请求...");

            try
            {
                // 获取 IHostApplicationLifetime 来立即触发停止
                var lifetime = _webApplication.Services.GetService<IHostApplicationLifetime>();
                
                // 立即停止接受新请求
                lifetime?.StopApplication();

                // 立即停止 Web 应用程序，不等待正在处理的请求
                // 使用超时机制确保不会阻塞
                var stopTask = _webApplication.StopAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500)); // 最多等待 500ms
                
                var completedTask = await Task.WhenAny(stopTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    logger?.LogWarning("Web Host 停止超时，强制释放资源");
                }
                else
                {
                    await stopTask;
                }

                // 立即释放资源
                await _webApplication.DisposeAsync();
                _webApplication = null;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "停止 Web Host 时出错，强制释放资源");
                // 即使出错也强制释放资源
                try
                {
                    if (_webApplication != null)
                    {
                        await _webApplication.DisposeAsync();
                        _webApplication = null;
                    }
                }
                catch (Exception disposeEx)
                {
                    logger?.LogError(disposeEx, "强制释放 Web Application 时出错");
                }
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

        // 车牌识别 - 设备回调接口（华夏智信）- 支持 GET 和 POST
        app.MapMethods(HuaXiaZhiXingApiPath, new[] { "GET", "POST" }, async (HttpContext context) =>
        {
            var logger = _sharedServiceProvider.GetRequiredService<ILogger<MinimalWebHostService>>();
            var result = new ResultInfoHuaXiaZhiXing();

            try
            {
                FormNameValueCollection? form = null;

                // 首先尝试从查询字符串获取参数（GET 请求或 POST 请求的查询参数）
                if (context.Request.Query.Count > 0)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in context.Request.Query)
                    {
                        var value = kvp.Value.Count > 0 ? kvp.Value[0] ?? string.Empty : string.Empty;
                        dict[kvp.Key] = value;
                    }
                    form = new FormNameValueCollection(dict);
                }

                // 如果是 POST 请求，尝试从请求体读取（完全还原旧代码逻辑）
                // 对于 GET 请求，如果查询字符串已有参数，跳过请求体读取
                if ((form == null || form.Count == 0) && context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    // Prefer standard form collection if populated（完全还原旧代码逻辑）
                    try
                    {
                        context.Request.EnableBuffering();
                        IFormCollection? ctxForm = null;
                        try
                        {
                            ctxForm = await context.Request.ReadFormAsync();
                        }
                        catch (BadHttpRequestException)
                        {
                            // 请求体不完整，忽略并继续尝试从原始请求体读取
                        }
                        catch
                        {
                            // 忽略其他读取失败，继续尝试从原始请求体读取
                        }

                        if (ctxForm != null && ctxForm.Count > 0)
                        {
                            // 将 IFormCollection 转换为 Dictionary，然后包装为 FormNameValueCollection
                            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kvp in ctxForm)
                            {
                                dict[kvp.Key] = kvp.Value.ToString();
                            }
                            form = new FormNameValueCollection(dict);
                        }
                        else
                        {
                            // Fallback: read raw body and parse（完全还原旧代码逻辑）
                            try
                            {
                                if (context.Request.Body.CanSeek)
                                {
                                    context.Request.Body.Position = 0;
                                    using var reader = new System.IO.StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                                    var raw = await reader.ReadToEndAsync();
                                    context.Request.Body.Position = 0;

                                    if (!string.IsNullOrWhiteSpace(raw))
                                    {
                                        // 使用 QueryHelpers.ParseQuery 解析（自动URL解码，与 HttpUtility.ParseQueryString 行为一致）
                                        var queryParams = QueryHelpers.ParseQuery(raw);
                                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        foreach (var kvp in queryParams)
                                        {
                                            var value = kvp.Value.Count > 0 ? kvp.Value[0] ?? string.Empty : string.Empty;
                                            dict[kvp.Key] = value;
                                        }
                                        form = new FormNameValueCollection(dict);
                                    }
                                }
                            }
                            catch (BadHttpRequestException)
                            {
                                // 请求体不完整，忽略错误，继续处理
                            }
                            catch
                            {
                                // 忽略其他读取错误，继续处理
                            }
                        }
                    }
                    catch (BadHttpRequestException)
                    {
                        // 请求体不完整，忽略错误，继续处理（可能查询字符串有参数）
                    }
                }

                if (form == null || form.Count == 0)
                {
                    result.error_num = -1;
                    result.error_str = "无效的请求";
                    await WriteCompressedJsonResponse(context, result, logger);
                    return;
                }

                var type = form["type"] ?? string.Empty;
                logger.LogInformation($"form:{form}");
                
                if (type.Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    var plateNum = form["plate_num"]; // Already decoded (京A12345)
                    if (!string.IsNullOrWhiteSpace(plateNum))
                    {
                        var weighingService = _sharedServiceProvider.GetRequiredService<IAttendedWeighingService>();
                        weighingService.OnPlateNumberRecognized(plateNum);
                        
                        logger.LogInformation($"华夏智信抓拍车牌号：{plateNum}");
                        
                        // Optional: access other fields if needed
                        // var plateColor = form["plate_color"];
                        // var pictureBase64 = form["picture"];
                        // var closeupBase64 = form["closeup_pic"];
                    }
                }
                else if (type.Equals("heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    var camIp = form["cam_ip"];
                    if (!string.IsNullOrEmpty(camIp))
                    {
                        logger.LogInformation($"华夏智信设备心跳：{camIp}");
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
                logger.LogError(ex, "处理华夏智信设备回调失败");
            }

            await WriteCompressedJsonResponse(context, result, logger);
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

    /// <summary>
    ///     写入符合相机要求的 JSON 响应
    ///     要求：1. 包含 Content-Length 头（冒号前后无空格）
    ///           2. 不使用压缩（Transfer-Encoding: chunked 和 Content-Encoding: gzip）
    ///           3. JSON 格式为紧凑模式（不带换行）
    ///           4. Content-Type 包含 charset=utf-8
    /// </summary>
    private static async Task WriteCompressedJsonResponse(HttpContext context, ResultInfoHuaXiaZhiXing result, ILogger logger)
    {
        // 序列化 JSON（使用默认选项，保持属性名不变，紧凑格式）
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // 保持原始属性名（error_num, error_str）
            WriteIndented = false, // 不格式化，保持紧凑格式
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 避免中文被转义
        };
        
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(result, jsonOptions);

        // 必须在写入任何响应内容之前设置所有响应头
        // 设置响应状态码
        context.Response.StatusCode = 200;
        
        // 设置 Content-Type 头，包含 charset=utf-8（冒号前后无空格）
        context.Response.ContentType = "application/json;charset=utf-8";
        
        // 手动设置 Content-Length 头（冒号前后无空格）
        // 格式：Content-Length:30
        context.Response.ContentLength = jsonBytes.Length;
        
        // 直接写入响应体（因为已移除压缩中间件，Content-Length 会被保留）
        await context.Response.Body.WriteAsync(jsonBytes.AsMemory(0, jsonBytes.Length));
        await context.Response.Body.FlushAsync();
        
        logger?.LogDebug($"HuaXiaZhiXing 响应: {System.Text.Encoding.UTF8.GetString(jsonBytes)}, Content-Length: {jsonBytes.Length}");
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

    /// <summary>
    ///     模拟 NameValueCollection 行为的简单包装类（完全还原旧代码逻辑）
    /// </summary>
    private class FormNameValueCollection
    {
        private readonly Dictionary<string, string> _dict;

        public FormNameValueCollection(Dictionary<string, string> dict)
        {
            _dict = dict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int Count => _dict.Count;

        public string? this[string key]
        {
            get
            {
                _dict.TryGetValue(key, out var value);
                return value;
            }
        }

        public override string ToString()
        {
            return string.Join(", ", _dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
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
