using System;
using System.Globalization;
using Avalonia;
using Avalonia.ReactiveUI;

namespace MaterialClient;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 设置应用程序语言环境为中文
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("zh-CN");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("zh-CN");
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}