using System;
using System.IO;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClient.Common.UnitTest;

public class UnitTest1
{
	[Fact]
	public void Capture_Jpeg_From_Hikvision()
	{
		var service = new HikvisionService();
		var config = new HikvisionDeviceConfig
		{
			Ip = "192.168.1.2",
			Username = "admin",
			Password = "admin",
			Port = 8050,
			StreamType = 0,
			Channels = new[] { 1 }
		};

		service.AddOrUpdateDevice(config);

		var captureDir = Path.Combine(AppContext.BaseDirectory, "captures");
		Directory.CreateDirectory(captureDir);
		var fileName = $"hik_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
		var fullPath = Path.Combine(captureDir, fileName);

		bool ok = service.CaptureJpeg(config, config.Channels[0], fullPath);
		Assert.True(ok, "CaptureJpeg failed.");
		Assert.True(File.Exists(fullPath), "Captured file not found.");
		var size = new FileInfo(fullPath).Length;
		Assert.True(size > 0, "Captured file size should be greater than 0.");
	}
}
