using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;

namespace MaterialClient;

internal sealed class Program
{
    private const string MutexName = "MaterialClient_SingleInstance_Mutex";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 使用 Mutex 确保只有一个实例运行
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        
        if (!createdNew)
        {
            // 如果 Mutex 已存在，说明程序已经在运行，直接退出
            return;
        }

        // 设置应用程序语言环境为中文
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("zh-CN");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("zh-CN");

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        
        // 程序退出时，using 语句会自动释放 Mutex
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
    }
}