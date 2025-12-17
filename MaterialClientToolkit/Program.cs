using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClientToolkit;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // 从 appsettings.json 加载配置
            var deviceConfigs = LoadConfigurations();

            // 命令行参数可以覆盖配置文件的值（如果只有一个设备）
            if (deviceConfigs.Count == 1)
            {
                ApplyCommandLineArguments(deviceConfigs[0], args);
            }

            // 创建服务实例
            var service = new HikvisionService();
            foreach (var config in deviceConfigs)
            {
                service.AddOrUpdateDevice(config);
            }

            // 创建 captures 文件夹（与工具目录相同）
            var toolDirectory = GetToolDirectory();
            var captureDir = Path.Combine(toolDirectory, "captures");
            Directory.CreateDirectory(captureDir);

            // 为每个设备的每个通道创建拍照请求
            var requests = new List<BatchCaptureRequest>();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

            foreach (var config in deviceConfigs)
            {
                var deviceKey = $"{config.Ip}:{config.Port}";
                foreach (var channel in config.Channels)
                {
                    var fileName = $"hik_{config.Ip.Replace(".", "_")}_ch{channel}_{timestamp}.jpg";
                    var fullPath = Path.Combine(captureDir, fileName);

                    requests.Add(new BatchCaptureRequest
                    {
                        Config = config,
                        Channel = channel,
                        SaveFullPath = fullPath,
                        DeviceKey = deviceKey
                    });
                }
            }

            Console.WriteLine($"开始批量捕获图片...");
            Console.WriteLine($"设备数量: {deviceConfigs.Count}");
            Console.WriteLine($"总任务数: {requests.Count}");
            Console.WriteLine($"保存目录: {captureDir}");
            Console.WriteLine();

            // 执行批量拍照
            var startTime = DateTime.Now;
            var results = await service.CaptureJpegFromStreamBatchAsync(requests);
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalSeconds;

            // 显示结果
            Console.WriteLine($"批量捕获完成，耗时: {duration:F2} 秒");
            Console.WriteLine();

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            Console.WriteLine($"成功: {successCount}, 失败: {failCount}");
            Console.WriteLine();

            // 显示详细信息
            foreach (var result in results)
            {
                var deviceInfo = $"{result.Request.Config.Ip}:{result.Request.Config.Port}";
                var channelInfo = $"通道 {result.Request.Channel}";

                if (result.Success)
                {
                    Console.WriteLine($"✓ {deviceInfo} {channelInfo} - 成功");
                    Console.WriteLine($"  文件: {Path.GetFileName(result.Request.SaveFullPath)}");
                    Console.WriteLine($"  大小: {result.FileSize} 字节");
                }
                else
                {
                    Console.WriteLine($"✗ {deviceInfo} {channelInfo} - 失败");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        Console.WriteLine($"  错误: {result.ErrorMessage}");
                    }
                    else
                    {
                        Console.WriteLine($"  HCNetSDK错误: {result.HcNetSdkError}, PlayM4错误: {result.PlayM4Error}");
                    }
                }
                Console.WriteLine();
            }

            // 返回状态码：如果有失败则返回1，全部成功返回0
            return failCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"发生错误: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static List<HikvisionDeviceConfig> LoadConfigurations()
    {
        // 使用可执行文件所在目录，而不是临时解压目录
        var basePath = GetToolDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        var configuration = builder.Build();

        var configs = new List<HikvisionDeviceConfig>();

        // 首先尝试读取 HikvisionDeviceList（多个设备）
        var deviceListSection = configuration.GetSection("HikvisionDeviceList");
        if (deviceListSection.Exists())
        {
            foreach (var deviceSection in deviceListSection.GetChildren())
            {
                var config = ParseDeviceConfig(deviceSection);
                if (config != null)
                {
                    configs.Add(config);
                }
            }
        }

        // 如果没有找到设备列表，尝试读取单个设备配置（向后兼容）
        if (configs.Count == 0)
        {
            var section = configuration.GetSection("HikvisionDevice");
            if (section.Exists())
            {
                var config = ParseDeviceConfig(section);
                if (config != null)
                {
                    configs.Add(config);
                }
            }
        }

        // 如果还是没有配置，使用默认值
        if (configs.Count == 0)
        {
            configs.Add(new HikvisionDeviceConfig
            {
                Ip = "192.168.3.245",
                Username = "admin",
                Password = "fdkj112233",
                Port = 8000,
                StreamType = 0,
                Channels = [1]
            });
        }

        return configs;
    }

    static HikvisionDeviceConfig? ParseDeviceConfig(IConfigurationSection section)
    {
        var config = new HikvisionDeviceConfig();

        config.Ip = section["Ip"] ?? "192.168.3.245";
        config.Username = section["Username"] ?? "admin";
        config.Password = section["Password"] ?? "fdkj112233";

        if (int.TryParse(section["Port"], out int port))
        {
            config.Port = port;
        }
        else
        {
            config.Port = 8000;
        }

        if (int.TryParse(section["StreamType"], out int streamType))
        {
            config.StreamType = streamType;
        }
        else
        {
            config.StreamType = 0;
        }

        // 解析 Channels 数组
        var channelsSection = section.GetSection("Channels");
        if (channelsSection.Exists())
        {
            var channels = new List<int>();
            foreach (var channelValue in channelsSection.GetChildren())
            {
                if (int.TryParse(channelValue.Value, out int channel))
                {
                    channels.Add(channel);
                }
            }
            config.Channels = channels.Count > 0 ? channels.ToArray() : [1];
        }
        else
        {
            config.Channels = [1];
        }

        return config;
    }

    static void ApplyCommandLineArguments(HikvisionDeviceConfig config, string[] args)
    {
        // 解析命令行参数，覆盖配置文件的值
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-ip" or "--ip":
                    if (i + 1 < args.Length)
                    {
                        config.Ip = args[++i];
                    }
                    break;
                case "-u" or "--username" or "-user":
                    if (i + 1 < args.Length)
                    {
                        config.Username = args[++i];
                    }
                    break;
                case "-p" or "--password":
                    if (i + 1 < args.Length)
                    {
                        config.Password = args[++i];
                    }
                    break;
                case "-port" or "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                    {
                        config.Port = port;
                    }
                    break;
                case "-c" or "--channel" or "-ch":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int channel))
                    {
                        config.Channels = [channel];
                    }
                    break;
                case "-h" or "--help":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }
    }

    static string GetToolDirectory()
    {
        // 对于单文件发布，Environment.ProcessPath 返回可执行文件的完整路径
        // 获取其所在目录，而不是临时解压目录
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
        {
            var exePath = Environment.ProcessPath;
            var directory = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }

        // 回退方案：使用 AppContext.BaseDirectory
        return AppContext.BaseDirectory;
    }

    static void PrintHelp()
    {
        Console.WriteLine("海康威视设备拍照工具");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  MaterialClientToolkit [选项]");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  -ip, --ip <IP地址>          设备IP地址 (默认: 192.168.3.245)");
        Console.WriteLine("  -u, --username, -user <用户名>  登录用户名 (默认: admin)");
        Console.WriteLine("  -p, --password <密码>       登录密码 (默认: fdkj112233)");
        Console.WriteLine("  -port, --port <端口>        设备端口 (默认: 8000)");
        Console.WriteLine("  -c, --channel, -ch <通道号>  通道号 (默认: 1)");
        Console.WriteLine("  -h, --help                  显示帮助信息");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  MaterialClientToolkit -ip 192.168.1.100 -u admin -p password123 -c 1");
        Console.WriteLine("  MaterialClientToolkit --ip 192.168.1.100 --channel 2");
    }
}
