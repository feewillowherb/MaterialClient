using System;
using System.Diagnostics;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Views;

public partial class AttendedWeighingDetailView : UserControl
{
    private static readonly Stopwatch CtorSw = new();
    private ILogger<AttendedWeighingDetailView>? _logger;
    
    public AttendedWeighingDetailView()
    {
        CtorSw.Restart();
        
        var sw = Stopwatch.StartNew();
        InitializeComponent();
        
        // InitializeComponent 完成后才能获取 logger
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        
        // 暂存初始化时间，等获取到 logger 后再输出
        _initComponentMs = sw.ElapsedMilliseconds;
        _ctorTotalMs = CtorSw.ElapsedMilliseconds;
    }

    private long _initComponentMs;
    private long _ctorTotalMs;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        
        // 从 ViewModel 的 ServiceProvider 获取 Logger
        // 由于 View 不能直接访问 ServiceProvider，使用 App 级别的方式
        try
        {
            var app = Avalonia.Application.Current as App;
            var serviceProvider = app?.GetType().GetField("_abpApplication", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(app);
            
            if (serviceProvider is Volo.Abp.IAbpApplicationWithInternalServiceProvider abpApp)
            {
                _logger = abpApp.ServiceProvider.GetService<ILogger<AttendedWeighingDetailView>>();
            }
        }
        catch
        {
            // 获取失败则不记录日志
        }
        
        // 输出之前暂存的初始化时间
        _logger?.LogInformation("[DetailView] InitializeComponent: {ElapsedMs}ms", _initComponentMs);
        _logger?.LogInformation("[DetailView] Constructor TOTAL: {ElapsedMs}ms", _ctorTotalMs);
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("[DetailView] OnLoaded START (since ctor: {ElapsedMs}ms)", CtorSw.ElapsedMilliseconds);
        
        // 只在首次加载时执行
        Loaded -= OnLoaded;

        if (DataContext is AttendedWeighingDetailViewModel vm)
        {
            await vm.LoadDataAsync();
        }
        
        _logger?.LogInformation("[DetailView] OnLoaded TOTAL: {ElapsedMs}ms", sw.ElapsedMilliseconds);
        _logger?.LogInformation("[DetailView] ===== FULL LOAD TIME: {ElapsedMs}ms =====", CtorSw.ElapsedMilliseconds);
    }
}
