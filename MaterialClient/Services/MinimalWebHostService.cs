using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.EventBus.Local;
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
    private readonly ILocalEventBus _localEventBus;
    private bool _isRunning;
    private WebApplication? _webApplication;

    private static string CallDeviceMessageHuaXiaZhiXingApiPath = "/api/CarLicense/CallDeviceMessageHuaXiaZhiXing";
    private static string SetScaleWeightApiPath = "/api/scale/weight";
    private static string SetTestPlateApiPath = "/api/lpr/test-plate";

    /// <summary>
    ///     构造函数，注入共享的服务提供者
    /// </summary>
    /// <param name="serviceProvider">来自桌面应用的共享服务提供者</param>
    /// <param name="configuration">应用配置</param>
    public MinimalWebHostService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _sharedServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _localEventBus = serviceProvider.GetRequiredService<ILocalEventBus>();
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
    /// <remarks>
    ///     当 LPR 设备类型为海康威视时不再启动 Web Host，避免与海康监听端口（SystemSettings.Urls）冲突。
    /// </remarks>
    public async Task StartAsync()
    {
        var settingsService = _sharedServiceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();
        if (settings.SystemSettings.LprDeviceType == LprDeviceType.Hikvision)
        {
            var logger = _sharedServiceProvider.GetService<ILogger<MinimalWebHostService>>();
            logger?.LogInformation("Web Host 未启动：当前为海康威视 LPR，与监听端口冲突");
            return;
        }

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

            // Configure URLs from SystemSettings (settings already loaded above)
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
            logger?.LogInformation("API 端点: {Urls}{ApiPath} 等（华夏智信回调、地磅测试）", urls,
                CallDeviceMessageHuaXiaZhiXingApiPath);

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
                CallDeviceMessageHuaXiaZhiXingApiPath,
                SetScaleWeightApiPath,
                SetTestPlateApiPath
            }
        }));

        // 地磅测试模式 - 设置重量
        app.MapPost(SetScaleWeightApiPath, async (SetWeightRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "请求体为空"
                });
            }

            if (request.Weight < 0)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "重量必须为非负数（单位：吨）"
                });
            }

            try
            {
                var settingsService = _sharedServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();

                if (settings.ScaleSettings.ScaleType != ScaleType.TestMode)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "当前不是地磅测试模式"
                    });
                }

                var preprocessor = _sharedServiceProvider.GetRequiredService<IScaleTestWeightPreprocessorService>();
                preprocessor.Enqueue(request.Weight);

                logger.LogInformation("地磅测试模式设置重量: {Weight} t", request.Weight);

                return Results.Ok(new
                {
                    success = true,
                    message = "完成",
                    weight = request.Weight
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "地磅测试模式设置重量失败");
                return Results.InternalServerError(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        });

        // 车牌测试模式 - 注入测试车牌识别结果
        app.MapPost(SetTestPlateApiPath, (SetTestPlateRequest? request) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "请求体为空"
                });
            }

            var plateNumber = request.PlateNumber?.Trim();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "车牌号不能为空"
                });
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
                logger.LogInformation("测试车牌注入成功：Plate={Plate}, DeviceType={DeviceType}, DeviceName={DeviceName}",
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
                logger.LogError(ex, "测试车牌注入失败");
                return Results.InternalServerError(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        });

        // 车牌识别 - 设备回调接口（华夏智信）- 支持 GET 和 POST
        app.MapMethods(CallDeviceMessageHuaXiaZhiXingApiPath, new[] { "GET", "POST" }, async (HttpContext context) =>
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
                if ((form == null || form.Count == 0) &&
                    context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
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
                                    using var reader = new System.IO.StreamReader(context.Request.Body,
                                        System.Text.Encoding.UTF8, leaveOpen: true);
                                    var raw = await reader.ReadToEndAsync();
                                    context.Request.Body.Position = 0;

                                    if (!string.IsNullOrWhiteSpace(raw))
                                    {
                                        // 使用 QueryHelpers.ParseQuery 解析（自动URL解码，与 HttpUtility.ParseQueryString 行为一致）
                                        var queryParams = QueryHelpers.ParseQuery(raw);
                                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        foreach (var kvp in queryParams)
                                        {
                                            var value = kvp.Value.Count > 0
                                                ? kvp.Value[0] ?? string.Empty
                                                : string.Empty;
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
                    result.error_num = 0;
                    result.error_str = string.Empty;
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
                        // 发布 ILocalEventBus 事件（统一事件传递）
                        var eventData = new LicensePlateRecognizedEventData
                        {
                            PlateNumber = plateNum,
                            ColorType = null, // 华夏智信回调中不包含颜色信息
                            DeviceType = LprDeviceType.Huaxiazhixin,
                            DeviceName = "Huaxiazhixin", // 可以从配置获取设备名称
                            Timestamp = DateTime.Now
                        };
                        _ = _localEventBus.PublishAsync(eventData);

                        logger.LogInformation($"华夏智信识别车牌号：{plateNum}");

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
                        var huaxiazhixinOnlineState = _sharedServiceProvider
                            .GetService<IHuaxiazhixinLprService>();
                        huaxiazhixinOnlineState?.RecordLastSeen(camIp);
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
            return;
        });
    }

    private record SetWeightRequest(
        [property: JsonPropertyName("weight")] decimal Weight
    );

    private record SetTestPlateRequest(
        [property: JsonPropertyName("plateNumber")]
        string? PlateNumber,
        [property: JsonPropertyName("deviceType")]
        LprDeviceType? DeviceType,
        [property: JsonPropertyName("deviceName")]
        string? DeviceName,
        [property: JsonPropertyName("colorType")]
        VzvisionColorType? ColorType,
        [property: JsonPropertyName("timestamp")]
        DateTime? Timestamp
    );


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


    private static async Task WriteCompressedJsonResponse(HttpContext context, ResultInfoHuaXiaZhiXing result,
        ILogger logger)
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

        logger?.LogDebug(
            $"HuaXiaZhiXing 响应: {System.Text.Encoding.UTF8.GetString(jsonBytes)}, Content-Length: {jsonBytes.Length}");
    }

    #endregion

}