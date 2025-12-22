using FlashCap;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

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

    /// <summary>
    /// 捕获当前预览帧
    /// </summary>
    /// <returns>图像字节数组，如果失败则返回null</returns>
    Task<byte[]?> CaptureCurrentFrameAsync();
}

/// <summary>
/// USB 摄像头服务实现
/// 使用 FlashCap 库来检测和访问 USB 摄像头设备
/// </summary>
public class UsbCameraService : IUsbCameraService, ISingletonDependency,IAsyncDisposable
{
    private readonly ILogger<UsbCameraService>? _logger;
    private CaptureDevice? _currentDevice;
    private CaptureDeviceDescriptor? _currentDescriptor;
    private Action<byte[], int, int>? _frameCallback;
    private readonly object _lockObject = new();
    private bool? _lastAvailabilityStatus; // 缓存上次的可用性状态，用于避免重复日志
    private byte[]? _lastFrameData; // 保存最后一帧数据，供拍照使用
    private readonly object _frameLockObject = new(); // 用于保护最后一帧数据的锁

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
                    // 只在状态改变时记录 Information 级别日志，避免重复日志
                    if (_lastAvailabilityStatus != true)
                    {
                        _logger?.LogInformation("找到可用的 USB 摄像头设备: {DeviceInfo}", deviceInfo);
                        _lastAvailabilityStatus = true;
                    }
                    else
                    {
                        _logger?.LogDebug("找到可用的 USB 摄像头设备: {DeviceInfo}", deviceInfo);
                    }
                    return deviceInfo;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "无法打开设备: {DeviceName}", descriptor.Name);
                    // 继续尝试下一个设备
                    continue;
                }
            }

            // 只在状态改变时记录 Information 级别日志，避免重复日志
            if (_lastAvailabilityStatus != false)
            {
                _logger?.LogInformation("未找到可用的 USB 摄像头设备");
                _lastAvailabilityStatus = false;
            }
            else
            {
                _logger?.LogDebug("未找到可用的 USB 摄像头设备");
            }
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
                            // 保存最后一帧数据，供拍照使用
                            lock (_frameLockObject)
                            {
                                _lastFrameData = imageBytes;
                            }

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

            // 清空最后一帧数据
            lock (_frameLockObject)
            {
                _lastFrameData = null;
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

            // 清空最后一帧数据
            lock (_frameLockObject)
            {
                _lastFrameData = null;
            }
        }
    }

    /// <summary>
    /// 捕获当前预览帧
    /// </summary>
    public Task<byte[]?> CaptureCurrentFrameAsync()
    {
        lock (_frameLockObject)
        {
            if (_lastFrameData == null)
            {
                _logger?.LogWarning("没有可用的帧数据，预览可能未启动或未接收到帧");
                return Task.FromResult<byte[]?>(null);
            }

            // 创建副本，避免外部修改影响内部数据
            var frameCopy = new byte[_lastFrameData.Length];
            Array.Copy(_lastFrameData, frameCopy, _lastFrameData.Length);
            return Task.FromResult<byte[]?>(frameCopy);
        }
    }

    /// <summary>
    /// 释放资源（实现 IAsyncDisposable）
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // 先停止预览，确保资源正确释放
        try
        {
            await StopPreviewAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "在 DisposeAsync 中停止预览时发生错误");
        }

        // 额外检查并释放设备（防止 StopPreviewAsync 失败时仍有资源未释放）
        CaptureDevice? deviceToDispose = null;
        lock (_lockObject)
        {
            deviceToDispose = _currentDevice;
            _currentDevice = null;
            _currentDescriptor = null;
            _frameCallback = null;
            IsPreviewing = false;
            _lastFrameData = null;
        }

        if (deviceToDispose != null)
        {
            try
            {
                await deviceToDispose.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "释放 USB 摄像头设备时发生错误");
            }
        }

        _logger?.LogDebug("USB 摄像头服务资源已释放");
    }
}