using System;
using System.IO;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClient.Common.UnitTest;

public class UnitTest1
{
    [Fact(Skip = "Manual-only: Requires physical Hikvision device and SDK runtime.")]
    public void Capture_Jpeg_From_Hikvision()
    {
        var service = new HikvisionService();
        var config = new HikvisionDeviceConfig
        {
            Ip = "192.168.3.3",
            Username = "admin",
            Password = "fdkj123456",
            Port = 8000,
            StreamType = 0,
            Channels = [1]
        };

        service.AddOrUpdateDevice(config);

        var captureDir = Path.Combine(AppContext.BaseDirectory, "captures");
        Directory.CreateDirectory(captureDir);
        var fileName = $"hik_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
        var fullPath = Path.Combine(captureDir, fileName);

        // 先断言在线，避免后续抓拍因登录失败而混淆
        var online = service.IsOnline(config);
        if (!online)
        {
            var err = HikvisionService.GetLastErrorCode();
            Assert.True(online, $"Device is not online or login failed. HCNetSDK error={err}");
        }

        // 尝试常见通道号：配置中的第一个、1、33、101
        var candidates = new int[] { config.Channels[0], 1, 33, 101 };
        bool ok = false;
        uint lastErr = 0;
        foreach (var ch in candidates)
        {
            ok = service.CaptureJpeg(config, ch, fullPath, out lastErr);
            if (ok) break;
        }
        if (!ok)
        {
            Assert.True(ok, $"CaptureJpeg failed. HCNetSDK error={lastErr}");
        }
        Assert.True(File.Exists(fullPath), "Captured file not found.");
        var size = new FileInfo(fullPath).Length;
        Assert.True(size > 0, "Captured file size should be greater than 0.");
    }
}