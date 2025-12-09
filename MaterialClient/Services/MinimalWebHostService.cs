using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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


            _webApplication.MapGet("/", () => "Hello World!");

            // Configure URLs from appsettings
            var urls = builder.Configuration["Urls"] ?? "http://localhost:9960";
            _webApplication.Urls.Add(urls);

            System.Diagnostics.Debug.WriteLine($"启动 Web 服务于 {urls}");
            System.Diagnostics.Debug.WriteLine($"Swagger UI 访问地址: {urls}/swagger");

            // Start the web application
            await _webApplication.RunAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Web Host 启动失败: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine("正在停止 Web Host...");

            try
            {
                await _webApplication.StopAsync();
                await _webApplication.DisposeAsync();
                _webApplication = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止 Web Host 时出错: {ex.Message}");
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
}