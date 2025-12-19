using FlashCap;
using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;

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

    /// <summary>
    /// 启动摄像头预览
    /// </summary>
    /// <param name="frameCallback">帧数据回调函数，参数为图像字节数组、宽度、高度</param>
    /// <returns>是否成功启动</returns>
    Task<bool> StartPreviewAsync(Action<byte[], int, int> frameCallback);

    /// <summary>
    /// 停止摄像头预览
    /// </summary>
    Task StopPreviewAsync();

    /// <summary>
    /// 获取当前预览状态
    /// </summary>
    bool IsPreviewing { get; }
}

/// <summary>
/// USB 摄像头服务实现
/// 使用 FlashCap 库来检测和访问 USB 摄像头设备
/// </summary>
public class UsbCameraService : IUsbCameraService
{
    private readonly ILogger<UsbCameraService>? _logger;
    private CaptureDevice? _currentDevice;
    private CaptureDeviceDescriptor? _currentDescriptor;
    private Action<byte[], int, int>? _frameCallback;
    private readonly object _lockObject = new();

    public bool IsPreviewing { get; private set; }

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

    /// <summary>
    /// 启动摄像头预览
    /// </summary>
    public async Task<bool> StartPreviewAsync(Action<byte[], int, int> frameCallback)
    {
        lock (_lockObject)
        {
            if (IsPreviewing)
            {
                _logger?.LogWarning("摄像头预览已在运行中");
                return true;
            }

            _frameCallback = frameCallback;
        }

        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices
                .EnumerateDescriptors()
                .Where(x => x.DeviceType == DeviceTypes.DirectShow)
                .ToList();

            CaptureDeviceDescriptor? targetDescriptor = null;
            VideoCharacteristics? targetCharacteristics = null;

            // 查找第一个可用的设备
            foreach (var descriptor in descriptors)
            {
                try
                {
                    if (descriptor.Characteristics == null || descriptor.Characteristics.Length == 0)
                    {
                        continue;
                    }

                    // 选择第一个特性
                    targetDescriptor = descriptor;
                    targetCharacteristics = descriptor.Characteristics[0];
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "无法使用设备: {DeviceName}", descriptor.Name);
                    continue;
                }
            }

            if (targetDescriptor == null || targetCharacteristics == null)
            {
                _logger?.LogWarning("未找到可用的 USB 摄像头设备");
                return false;
            }

            _currentDescriptor = targetDescriptor;

            // 从 VideoCharacteristics 获取宽度和高度信息
            var width = targetCharacteristics.Width;
            var height = targetCharacteristics.Height;

            // 打开设备并启动预览
            _currentDevice = await targetDescriptor.OpenAsync(
                targetCharacteristics,
                async args =>
                {
                    try
                    {
                        // 将帧数据转换为字节数组
                        var imageBytes = args.Buffer.ExtractImage();
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            var callback = _frameCallback;
                            if (callback != null)
                            {
                                // 使用 VideoCharacteristics 中保存的宽度和高度
                                // ExtractImage() 只返回字节数组，不包含尺寸信息
                                callback(imageBytes, width, height);
                            }
                        }

                        await Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "处理摄像头帧数据时发生错误");
                    }
                });

            // 启动捕获
            await _currentDevice.StartAsync();

            lock (_lockObject)
            {
                IsPreviewing = true;
            }

            _logger?.LogInformation("USB 摄像头预览已启动: {DeviceName}", targetDescriptor.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "启动 USB 摄像头预览时发生错误");
            lock (_lockObject)
            {
                IsPreviewing = false;
                _currentDevice = null;
                _currentDescriptor = null;
            }

            return false;
        }
    }

    /// <summary>
    /// 停止摄像头预览
    /// </summary>
    public async Task StopPreviewAsync()
    {
        lock (_lockObject)
        {
            if (!IsPreviewing)
            {
                return;
            }
        }

        try
        {
            if (_currentDevice != null)
            {
                await _currentDevice.StopAsync();
                await _currentDevice.DisposeAsync();
                _currentDevice = null;
            }

            lock (_lockObject)
            {
                IsPreviewing = false;
                _currentDescriptor = null;
                _frameCallback = null;
            }

            _logger?.LogInformation("USB 摄像头预览已停止");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "停止 USB 摄像头预览时发生错误");
            lock (_lockObject)
            {
                IsPreviewing = false;
                _currentDevice = null;
                _currentDescriptor = null;
            }
        }
    }
}