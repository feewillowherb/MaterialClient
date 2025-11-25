using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClientToolkit;

class Program
{
	static int Main(string[] args)
	{
		try
		{
			// 从 appsettings.json 加载配置
			var config = LoadConfiguration();
			
			// 命令行参数可以覆盖配置文件的值
			ApplyCommandLineArguments(config, args);
			
			// 创建服务实例
			var service = new HikvisionService();
			service.AddOrUpdateDevice(config);

			// 创建 captures 文件夹（与工具目录相同）
			// 对于单文件发布，使用 Environment.ProcessPath 获取可执行文件所在目录
			var toolDirectory = GetToolDirectory();
			var captureDir = Path.Combine(toolDirectory, "captures");
			Directory.CreateDirectory(captureDir);

			// 生成文件名
			var fileName = $"hik_stream_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
			var fullPath = Path.Combine(captureDir, fileName);

			Console.WriteLine($"开始捕获图片...");
			Console.WriteLine($"设备: {config.Ip}:{config.Port}");
			Console.WriteLine($"通道: {config.Channels[0]}");
			Console.WriteLine($"保存路径: {fullPath}");

			// 尝试捕获图片
			bool ok = false;
			foreach (var ch in config.Channels)
			{
				ok = service.CaptureJpegFromStream(config, ch, fullPath);
				if (ok)
				{
					break;
				}
				else
				{
					var hcError = HikvisionService.GetLastErrorCode();
					var playM4Error = PlayM4Decoder.GetLastError();
					Console.WriteLine($"通道 {ch} 捕获失败 - HCNetSDK错误: {hcError}, PlayM4错误: {playM4Error}");
				}
			}

			if (!ok)
			{
				var hcError = HikvisionService.GetLastErrorCode();
				var playM4Error = PlayM4Decoder.GetLastError();
				Console.Error.WriteLine($"捕获失败 - HCNetSDK错误: {hcError}, PlayM4错误: {playM4Error}");
				return 1;
			}

			// 验证文件
			if (!File.Exists(fullPath))
			{
				Console.Error.WriteLine("错误: 文件未创建");
				return 1;
			}

			var fileInfo = new FileInfo(fullPath);
			if (fileInfo.Length == 0)
			{
				Console.Error.WriteLine("错误: 文件大小为0");
				return 1;
			}

			Console.WriteLine($"捕获成功!");
			Console.WriteLine($"文件大小: {fileInfo.Length} 字节");
			Console.WriteLine($"文件路径: {fullPath}");
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"发生错误: {ex.Message}");
			Console.Error.WriteLine(ex.StackTrace);
			return 1;
		}
	}

	static HikvisionDeviceConfig LoadConfiguration()
	{
		// 使用可执行文件所在目录，而不是临时解压目录
		var basePath = GetToolDirectory();
		var builder = new ConfigurationBuilder()
			.SetBasePath(basePath)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

		var configuration = builder.Build();

		var config = new HikvisionDeviceConfig();
		var section = configuration.GetSection("HikvisionDevice");

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
