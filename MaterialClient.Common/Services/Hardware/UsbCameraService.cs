using FlashCap;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// USB 摄像头服务接口
/// </summary>
public interface IUsbCameraService
{
    /// <summary>
    /// 检查是否有可用的 USB 摄像头设备
    /// </summary>
    /// <returns>如果存在可用的 USB 摄像头设备返回 true，否则返回 false</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// 获取第一个有效的 USB 摄像头设备信息
    /// </summary>
    /// <returns>摄像头设备描述信息，如果没有可用设备则返回 null</returns>
    Task<string?> GetFirstAvailableDeviceAsync();
}

/// <summary>
/// USB 摄像头服务实现
/// 使用 FlashCap 库来检测和访问 USB 摄像头设备
/// </summary>
public class UsbCameraService : IUsbCameraService
{
    private readonly ILogger<UsbCameraService>? _logger;

    public UsbCameraService(ILogger<UsbCameraService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查是否有可用的 USB 摄像头设备
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var device = await GetFirstAvailableDeviceAsync();
            return !string.IsNullOrEmpty(device);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "检查 USB 摄像头设备时发生错误");
            return false;
        }
    }

    /// <summary>
    /// 获取第一个有效的 USB 摄像头设备信息
    /// </summary>
    public async Task<string?> GetFirstAvailableDeviceAsync()
    {
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices
                .EnumerateDescriptors()
                .Where(x => x.DeviceType == DeviceTypes.DirectShow)
                .ToList();

            foreach (var descriptor in descriptors)
            {
                try
                {
                    // 检查设备是否有可用的特性
                    if (descriptor.Characteristics == null || descriptor.Characteristics.Length == 0)
                    {
                        _logger?.LogDebug("设备 {DeviceName} 没有可用的特性", descriptor.Name);
                        continue;
                    }

                    // 尝试打开设备以验证其是否可用
                    using var device = await descriptor.OpenAsync(
                        descriptor.Characteristics[0],
                        async args =>
                        {
                            // 空回调，仅用于验证设备是否可用
                            await Task.CompletedTask;
                        });

                    // 如果成功打开，返回设备描述
                    var deviceInfo = $"{descriptor.Name} ({descriptor.Characteristics[0].PixelFormat})";
                    _logger?.LogInformation("找到可用的 USB 摄像头设备: {DeviceInfo}", deviceInfo);
                    return deviceInfo;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "无法打开设备: {DeviceName}", descriptor.Name);
                    // 继续尝试下一个设备
                    continue;
                }
            }

            _logger?.LogInformation("未找到可用的 USB 摄像头设备");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "枚举 USB 摄像头设备时发生错误");
            return null;
        }
    }
}