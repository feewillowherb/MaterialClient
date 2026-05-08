using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视设备服务接口
///     注意：此服务专门用于海康威视设备的车牌识别功能，与 SystemSettings.LprDeviceType 配置无关。
///     无论 LprDeviceType 设置为 Hikvision 还是 Vzvision，此服务都会正常工作。
/// </summary>
public interface IHikvisionService
{
    void AddOrUpdateDevice(HikvisionDeviceConfig config);
    bool IsOnline(HikvisionDeviceConfig config);
    bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, int quality = 90,
        int jpegQuality = 100);

    bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, out uint lastError,
        int quality = 90, int jpegQuality = 100);

    bool TryOpenRealStream(HikvisionDeviceConfig config, int channel);
    bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath,
        int jpegQuality = 100);
    Task<List<BatchCaptureResult>> CaptureJpegFromStreamBatchAsync(List<BatchCaptureRequest> requests);
    Task<List<BatchCaptureResult>> TestCaptureAsync();
    /// <summary>
    ///     Test capture for a single camera. Returns the result or null if config is invalid or capture fails.
    /// </summary>
    Task<BatchCaptureResult?> TestCaptureAsync(CameraConfig cameraConfig);
}

/// <summary>
///     海康威视设备服务实现
///     注意：此服务专门用于海康威视设备的车牌识别功能，与 SystemSettings.LprDeviceType 配置无关。
///     无论 LprDeviceType 设置为 Hikvision 还是 Vzvision，此服务都会正常工作。
///     调用方应根据 LprDeviceType 配置决定是否调用此服务的方法。
/// </summary>
public sealed class HikvisionService : IHikvisionService, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, int> deviceKeyToUserId = new();
    private readonly ISettingsService? _settingsService;
    private readonly ILogger<HikvisionService>? _logger;

    public HikvisionService(ISettingsService? settingsService = null, ILogger<HikvisionService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public void AddOrUpdateDevice(HikvisionDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var key = BuildDeviceKey(config);
        deviceKeyToUserId.AddOrUpdate(key, _ => -1, (_, __) => -1);
    }

    public bool IsOnline(HikvisionDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        EnsureInitialized();
        return EnsureLogin(config, out _);
    }

    public bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, int quality = 90,
        int jpegQuality = 100)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath))
            throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!EnsureLogin(config, out var userId))
        {
            _logger?.LogWarning(
                "海康威视抓拍失败（登录失败）: IP={Ip}, Port={Port}, Channel={Channel}, SavePath={SavePath}",
                config.Ip, config.Port, channel, saveFullPath);
            return false;
        }

        var para = new NET_DVR.NET_DVR_JPEGPARA
        {
            wPicQuality = (ushort)Math.Clamp(quality, 0, 100),
            wPicSize = 0xFF // use device default
        };

        // 分配 10MB 缓存区
        const int bufferSize = 10 * 1024 * 1024; // 10MB
        var buffer = new byte[bufferSize];
        uint returnedSize = 0;

        // 调用新 API
        var ok = NET_DVR.NET_DVR_CaptureJPEGPicture_NEW(
            userId, channel, ref para, buffer, (uint)bufferSize, out returnedSize);

        if (ok && returnedSize > 0)
        {
            // 将缓存区数据写入文件
            File.WriteAllBytes(saveFullPath, buffer.Take((int)returnedSize).ToArray());
            // Apply JPEG compression after successful capture (result always true regardless of compression outcome)
            JpegCompressionUtil.TryCompressJpeg(saveFullPath, jpegQuality, _logger);
            return true;
        }

        var errorCode = NET_DVR.NET_DVR_GetLastError();
        _logger?.LogWarning(
            "海康威视抓拍失败: IP={Ip}, Port={Port}, Channel={Channel}, SavePath={SavePath}, ErrorCode={ErrorCode}, ReturnedSize={ReturnedSize}",
            config.Ip, config.Port, channel, saveFullPath, errorCode, returnedSize);
        return false;
    }

    public bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, out uint lastError,
        int quality = 90, int jpegQuality = 100)
    {
        lastError = 0;
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath))
            throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!EnsureLogin(config, out var userId))
        {
            lastError = NET_DVR.NET_DVR_GetLastError();
            _logger?.LogWarning(
                "海康威视抓拍失败（登录失败）: IP={Ip}, Port={Port}, Channel={Channel}, SavePath={SavePath}, ErrorCode={ErrorCode}",
                config.Ip, config.Port, channel, saveFullPath, lastError);
            return false;
        }

        var para = new NET_DVR.NET_DVR_JPEGPARA
        {
            wPicQuality = (ushort)Math.Clamp(quality, 1, 100),
            wPicSize = 0xFF
        };

        // 分配 10MB 缓存区
        const int bufferSize = 10 * 1024 * 1024; // 10MB
        var buffer = new byte[bufferSize];
        uint returnedSize = 0;

        // 调用新 API
        var ok = NET_DVR.NET_DVR_CaptureJPEGPicture_NEW(
            userId, channel, ref para, buffer, (uint)bufferSize, out returnedSize);

        if (ok && returnedSize > 0)
        {
            // 将缓存区数据写入文件
            File.WriteAllBytes(saveFullPath, buffer.Take((int)returnedSize).ToArray());
            // Apply JPEG compression after successful capture (result always true regardless of compression outcome)
            JpegCompressionUtil.TryCompressJpeg(saveFullPath, jpegQuality, _logger);
            return true;
        }

        if (!ok)
        {
            lastError = NET_DVR.NET_DVR_GetLastError();
        }

        _logger?.LogWarning(
            "海康威视抓拍失败: IP={Ip}, Port={Port}, Channel={Channel}, SavePath={SavePath}, ErrorCode={ErrorCode}, ReturnedSize={ReturnedSize}",
            config.Ip, config.Port, channel, saveFullPath, lastError, returnedSize);
        return false;
    }

    // Placeholder for real-time stream obtaining. In many apps this returns a handle or starts a callback.
    public bool TryOpenRealStream(HikvisionDeviceConfig config, int channel)
    {
        ArgumentNullException.ThrowIfNull(config);
        EnsureInitialized();
        // Not implemented here to keep scope minimal for unit test; can be expanded later.
        return IsOnline(config);
    }

    public bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath,
        int jpegQuality = 100)
    {
        return CaptureJpegFromStream(config, channel, saveFullPath, out _, jpegQuality);
    }

    public async Task<List<BatchCaptureResult>> CaptureJpegFromStreamBatchAsync(List<BatchCaptureRequest> requests)
    {
        if (requests == null || requests.Count == 0) return new List<BatchCaptureResult>();

        // Get settings to determine which capture method to use
        // Default to Substream if settings service is not available
        var streamType = StreamType.Substream;
        var jpegQuality = 100; // default: no compression
        if (_settingsService != null)
        {
            var settings = await _settingsService.GetSettingsAsync();
            streamType = settings.SystemSettings.CaptureStreamType;
            jpegQuality = settings.SystemSettings.JpegQuality;
        }

        // Route to appropriate method based on stream type
        if (streamType == StreamType.Substream)
        {
            return await CaptureJpegBatchInternalAsync(requests, jpegQuality);
        }

        // Mainstream capture (existing implementation)
        // 同步处理多个设备
        var results = requests.Select(request =>
        {
            var result = new BatchCaptureResult
            {
                Request = request,
                Success = false,
                HcNetSdkError = 0,
                PlayM4Error = 0,
                ErrorMessage = null,
                FileSize = 0
            };

            try
            {
                // 同步调用拍照方法
                var playM4Error = 0;
                result.Success = CaptureJpegFromStream(request.Config, request.Channel, request.SaveFullPath,
                    out playM4Error);
                result.PlayM4Error = playM4Error;

                if (!result.Success)
                {
                    result.HcNetSdkError = GetLastErrorCode();
                    result.ErrorMessage = $"HCNetSDK错误: {result.HcNetSdkError}, PlayM4错误: {result.PlayM4Error}";
                }
                else
                {
                    // 验证文件
                    if (File.Exists(request.SaveFullPath))
                    {
                        var fileInfo = new FileInfo(request.SaveFullPath);
                        result.FileSize = fileInfo.Length;
                        if (fileInfo.Length == 0)
                        {
                            result.Success = false;
                            result.ErrorMessage = "文件大小为0";
                        }
                        else
                        {
                            // Apply JPEG compression after successful capture
                            JpegCompressionUtil.TryCompressJpeg(request.SaveFullPath, jpegQuality, _logger);
                            result.FileSize = new FileInfo(request.SaveFullPath).Length;
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = "文件未创建";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }).ToList();

        return results;
    }

    private Task<List<BatchCaptureResult>> CaptureJpegBatchInternalAsync(List<BatchCaptureRequest> requests, int jpegQuality = 100)
    {
        if (requests == null || requests.Count == 0) return Task.FromResult(new List<BatchCaptureResult>());

        // 同步处理多个设备（子码流直接拍照）
        var results = requests.Select(request =>
        {
            var result = new BatchCaptureResult
            {
                Request = request,
                Success = false,
                HcNetSdkError = 0,
                PlayM4Error = 0,
                ErrorMessage = null,
                FileSize = 0
            };

            try
            {
                // 同步调用拍照方法
                uint lastError = 0;
                result.Success = CaptureJpeg(request.Config, request.Channel, request.SaveFullPath, out lastError);
                result.HcNetSdkError = lastError;

                if (!result.Success)
                {
                    result.ErrorMessage = $"HCNetSDK错误: {result.HcNetSdkError}";
                }
                else
                {
                    // 验证文件
                    if (File.Exists(request.SaveFullPath))
                    {
                        var fileInfo = new FileInfo(request.SaveFullPath);
                        result.FileSize = fileInfo.Length;
                        if (fileInfo.Length == 0)
                        {
                            result.Success = false;
                            result.ErrorMessage = "文件大小为0";
                        }
                        else
                        {
                            // Apply JPEG compression after successful capture
                            JpegCompressionUtil.TryCompressJpeg(request.SaveFullPath, jpegQuality, _logger);
                            result.FileSize = new FileInfo(request.SaveFullPath).Length;
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = "文件未创建";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }).ToList();

        return Task.FromResult(results);
    }

    public async Task<List<BatchCaptureResult>> TestCaptureAsync()
    {
        if (_settingsService == null)
        {
            return new List<BatchCaptureResult>();
        }

        // Get all camera configurations (excluding USB cameras)
        var settings = await _settingsService.GetSettingsAsync();
        var cameraConfigs = settings.CameraConfigs;

        if (cameraConfigs.Count == 0)
        {
            return new List<BatchCaptureResult>();
        }

        // Get stream type for filename suffix
        var streamType = settings.SystemSettings.CaptureStreamType;
        var streamTypeSuffix = streamType == StreamType.Substream ? "sub" : "main";

        // Create test image directory
        var appDirectory = AppContext.BaseDirectory;
        var testImageDir = Path.Combine(appDirectory, "TestImage");
        Directory.CreateDirectory(testImageDir);

        // Create batch requests for all cameras
        var requests = new List<BatchCaptureRequest>();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

        foreach (var cameraConfig in cameraConfigs)
        {
            if (!cameraConfig.IsValid())
            {
                continue;
            }

            if (!int.TryParse(cameraConfig.Port, out var port) ||
                !int.TryParse(cameraConfig.Channel, out var channel))
            {
                continue;
            }

            var hikvisionConfig = new HikvisionDeviceConfig
            {
                Ip = cameraConfig.Ip,
                Port = port,
                Username = cameraConfig.UserName,
                Password = cameraConfig.Password,
                Channels = new[] { channel }
            };

            var fileName = $"test_{cameraConfig.Name}_ch{channel}_{streamTypeSuffix}_{timestamp}.jpg";
            var savePath = Path.Combine(testImageDir, fileName);

            requests.Add(new BatchCaptureRequest
            {
                Config = hikvisionConfig,
                Channel = channel,
                SaveFullPath = savePath,
                DeviceKey = $"{cameraConfig.Ip}:{port}"
            });
        }

        if (requests.Count == 0)
        {
            return new List<BatchCaptureResult>();
        }

        // Use unified interface - it will automatically route based on settings
        var results = await CaptureJpegFromStreamBatchAsync(requests);

        return results;
    }

    public async Task<BatchCaptureResult?> TestCaptureAsync(CameraConfig cameraConfig)
    {
        if (cameraConfig == null || !cameraConfig.IsValid())
            return null;

        if (!int.TryParse(cameraConfig.Port, out var port) ||
            !int.TryParse(cameraConfig.Channel, out var channel))
            return null;

        var streamTypeSuffix = "sub";
        if (_settingsService != null)
        {
            var settings = await _settingsService.GetSettingsAsync();
            var streamType = settings.SystemSettings.CaptureStreamType;
            streamTypeSuffix = streamType == StreamType.Substream ? "sub" : "main";
        }

        var appDirectory = AppContext.BaseDirectory;
        var testImageDir = Path.Combine(appDirectory, "TestImage");
        Directory.CreateDirectory(testImageDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var fileName = $"test_{cameraConfig.Name}_ch{channel}_{streamTypeSuffix}_{timestamp}.jpg";
        var savePath = Path.Combine(testImageDir, fileName);

        var hikvisionConfig = new HikvisionDeviceConfig
        {
            Ip = cameraConfig.Ip,
            Port = port,
            Username = cameraConfig.UserName,
            Password = cameraConfig.Password,
            Channels = new[] { channel }
        };

        var request = new BatchCaptureRequest
        {
            Config = hikvisionConfig,
            Channel = channel,
            SaveFullPath = savePath,
            DeviceKey = $"{cameraConfig.Ip}:{port}"
        };

        var results = await CaptureJpegFromStreamBatchAsync(new List<BatchCaptureRequest> { request });
        return results.Count > 0 ? results[0] : null;
    }

    public static uint GetLastErrorCode()
    {
        return NET_DVR.NET_DVR_GetLastError();
    }

    public bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath,
        out int playM4Error, int jpegQuality = 100)
    {
        playM4Error = 0;
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath))
            throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!EnsureLogin(config, out var userId))
        {
            _logger?.LogWarning(
                "Stream capture failed (login failed): IP={Ip}, Port={Port}, Channel={Channel}, SavePath={SavePath}",
                config.Ip, config.Port, channel, saveFullPath);
            return false;
        }

        // Performance tracking
        var sw = Stopwatch.StartNew();
        
        var lRealHandle = -1;
        // When hPlayWnd is NULL, callback function is needed to process stream data
        // Reference: hPlayWnd as NULL means only fetch stream without decoding, manual decoding required (using PlayM4)
        var hPlayWnd = IntPtr.Zero; // NULL - fetch stream only, manual decoding required

        // Create PlayM4 decoder instance
        PlayM4Decoder? decoder = null;
        var streamLock = new object();
        
        // Local state flag captured by closure - prevents race condition
        var disposed = false;
        
        // CRITICAL: Use GCHandle to prevent delegate from being garbage collected
        // The unmanaged SDK only stores function pointer, GC doesn't know it's still in use
        GCHandle? callbackHandle = null;
        NET_DVR.REALDATACALLBACK? realDataCallback = null;

        _logger?.LogDebug("Starting stream capture: IP={Ip}, Channel={Channel}", config.Ip, channel);

        // Callback function for receiving stream data (when hPlayWnd is NULL)
        // Data types: NET_DVR_SYSHEAD (system header), NET_DVR_STREAMDATA (stream data), etc.
        // Use PlayM4Decoder for manual decoding
        realDataCallback = (handle, dataType, buffer, bufSize, user) =>
        {
            // CRITICAL: Wrap entire callback in try-catch to prevent process crash
            // Unmanaged SDK cannot handle managed exceptions
            try
            {
                // Quick exit if disposed - check before acquiring lock
                if (disposed) return;

                lock (streamLock)
                {
                    // Double-check after acquiring lock
                    if (disposed || decoder == null) return;

                    // Validate buffer parameters
                    if (buffer == IntPtr.Zero || bufSize == 0) return;

                    switch (dataType)
                    {
                        case NET_DVR.NET_DVR_SYSHEAD: // System header data
                            if (!decoder.IsInitialized)
                            {
                                _logger?.LogDebug("Received system header: Size={Size}", bufSize);
                                // Use system header data to initialize playback library
                                // Get desktop window handle for playback (even if not displaying, valid handle needed)
                                var hWnd = NET_DVR.GetDesktopWindow();
                                if (!decoder.OpenStream(buffer, bufSize, hWnd))
                                {
                                    _logger?.LogError("Decoder initialization failed: Port={Port}, Error={Error}",
                                        decoder.Port, decoder.GetLastError());
                                }
                            }
                            break;

                        case NET_DVR.NET_DVR_STREAMDATA: // Stream data
                            if (decoder.IsPlaying)
                            {
                                // Input stream data to playback library for decoding
                                decoder.InputData(buffer, bufSize);
                            }
                            break;

                        case NET_DVR.NET_DVR_AUDIOSTREAMDATA: // Audio data
                            // Audio data processing (if needed)
                            // PlayM4 can also process audio data
                            if (decoder.IsPlaying)
                            {
                                decoder.InputData(buffer, bufSize);
                            }
                            break;

                        case NET_DVR.NET_DVR_PRIVATE_DATA: // Private data (including smart info)
                            // Received private data, may contain smart analysis information
                            // Process as needed
                            break;

                        default:
                            // Other types of data, also try to input to playback library
                            if (decoder.IsPlaying)
                            {
                                decoder.InputData(buffer, bufSize);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // MUST catch all exceptions to prevent crash from unmanaged callback
                _logger?.LogError(ex, "Callback exception: DataType={DataType}, BufSize={BufSize}", dataType, bufSize);
            }
        };

        try
        {
            // Create PlayM4 decoder instance
            decoder = new PlayM4Decoder();
            _logger?.LogDebug("Decoder created: Port={Port}", decoder.Port);

            // Pin the callback delegate to prevent GC from collecting it
            // This is CRITICAL because the unmanaged SDK only stores the function pointer
            callbackHandle = GCHandle.Alloc(realDataCallback);

            // Start real-time preview
            // hPlayWnd as NULL means only fetch stream without decoding; valid value means SDK auto-decodes
            var previewInfo = new NET_DVR.NET_DVR_PREVIEWINFO
            {
                lChannel = channel,
                dwStreamType = 0, // Main stream
                dwLinkMode = 0, // TCP mode
                hPlayWnd = hPlayWnd, // NULL - fetch stream only, manual decoding required
                bBlocked = true, // Blocking stream fetch
                bPassbackRecord = false, // Don't enable recording passback
                byPreviewMode = 0, // Normal preview
                byStreamID = new byte[32],
                byProtoType = 0, // Private protocol
                byRes1 = 0,
                byVideoCodingType = 0, // General encoding data
                dwDisplayBufNum = 1, // Max buffer frames for playback buffer
                byNPQMode = 0, // Direct connection mode
                byRes = new byte[215]
            };

            // When hPlayWnd is NULL, callback function must be set to process stream data
            lRealHandle = NET_DVR.NET_DVR_RealPlay_V40(userId, ref previewInfo, realDataCallback, IntPtr.Zero);
            if (lRealHandle < 0)
            {
                var errorCode = NET_DVR.NET_DVR_GetLastError();
                _logger?.LogWarning(
                    "Stream capture failed (preview start failed): IP={Ip}, Port={Port}, Channel={Channel}, ErrorCode={ErrorCode}, ErrorDesc={ErrorDesc}",
                    config.Ip, config.Port, channel, errorCode, GetErrorDescription(errorCode));
                return false;
            }

            _logger?.LogDebug("Preview started: Handle={Handle}", lRealHandle);

            // Wait for decoder initialization using event-based waiting instead of polling
            // This is more efficient and reliable than Thread.Sleep polling
            if (!decoder.WaitForPlaying(5000))
            {
                // Decoder initialization failed
                playM4Error = decoder.GetLastError();

                _logger?.LogWarning(
                    "Stream capture failed (decoder init timeout): IP={Ip}, Port={Port}, Channel={Channel}, PlayM4Error={PlayM4Error}",
                    config.Ip, config.Port, channel, playM4Error);
                return false;
            }

            // Wait for one frame to be decoded
            Thread.Sleep(500);

            // Use PlayM4_GetJPEG to capture current frame as JPEG image
            var ok = decoder.CaptureJpeg(saveFullPath);

            if (!ok)
            {
                playM4Error = decoder.GetLastError();
                _logger?.LogWarning(
                    "Stream capture failed (JPEG capture failed): IP={Ip}, Port={Port}, Channel={Channel}, PlayM4Error={PlayM4Error}",
                    config.Ip, config.Port, channel, playM4Error);
            }
            else
            {
                sw.Stop();

                // Apply JPEG compression after successful capture
                JpegCompressionUtil.TryCompressJpeg(saveFullPath, jpegQuality, _logger);

                var fileSize = File.Exists(saveFullPath) ? new FileInfo(saveFullPath).Length : 0;
                _logger?.LogInformation(
                    "Stream capture successful: IP={Ip}, Channel={Channel}, FileSize={Size}, Duration={Ms}ms",
                    config.Ip, channel, fileSize, sw.ElapsedMilliseconds);
            }

            return ok;
        }
        finally
        {
            // CRITICAL: Fix race condition by following correct order:
            // 1. Set disposed flag first - prevents new callback executions
            disposed = true;

            // 2. Stop preview stream - stops SDK from calling more callbacks
            if (lRealHandle >= 0)
            {
                NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
                // Wait for any in-flight callbacks to complete
                Thread.Sleep(200);
            }

            // 3. Finally release decoder within lock - ensures no callback is using it
            lock (streamLock)
            {
                if (decoder != null)
                {
                    // Get error code if not retrieved earlier
                    if (playM4Error == 0 && decoder.Port >= 0)
                        playM4Error = decoder.GetLastError();

                    decoder.Dispose();
                }
            }

            // 4. Release the GCHandle to allow delegate to be garbage collected
            // MUST be done after SDK has stopped calling the callback
            if (callbackHandle.HasValue && callbackHandle.Value.IsAllocated)
            {
                callbackHandle.Value.Free();
            }

            _logger?.LogDebug("Resources cleaned: Handle={Handle}", lRealHandle);
        }
    }

    private static string BuildDeviceKey(HikvisionDeviceConfig config)
    {
        return $"{config.Ip}:{config.Port}:{config.Username}";
    }

    /// <summary>
    ///     Gets a human-readable description for HCNetSDK error codes.
    /// </summary>
    /// <param name="errorCode">The HCNetSDK error code</param>
    /// <returns>A description of the error</returns>
    private static string GetErrorDescription(uint errorCode)
    {
        return errorCode switch
        {
            0 => "No error",
            1 => "Username or password error",
            2 => "No permission",
            3 => "SDK not initialized",
            4 => "Channel number error",
            5 => "Max client connections to device exceeded",
            6 => "Version mismatch",
            7 => "Failed to connect to device",
            8 => "Send failed",
            9 => "Receive failed",
            10 => "Timeout",
            11 => "Data transfer failed",
            12 => "Port incorrect",
            13 => "Password error",
            14 => "Get DVR work state failed",
            15 => "Get DVR system info failed",
            16 => "DVR does not support this function",
            17 => "DVR is offline",
            18 => "User is locked",
            19 => "Failed to allocate resources",
            20 => "DVR is being operated",
            21 => "DVR resource is being used",
            22 => "No more connections from the DVR are allowed",
            23 => "DVR command execution failed",
            24 => "DVR preview failed",
            25 => "DVR parameter format error",
            26 => "DVR invalid file or file error",
            27 => "Start preview failed",
            28 => "Open file failed",
            29 => "Read file failed",
            30 => "Write file failed",
            31 => "Close file failed",
            32 => "Create file failed",
            33 => "Delete file failed",
            34 => "Seek file failed",
            35 => "Get file size failed",
            36 => "Open stream failed",
            37 => "Close stream failed",
            38 => "Get stream failed",
            39 => "Start record failed",
            40 => "Stop record failed",
            41 => "Start capture failed",
            42 => "Stop capture failed",
            43 => "No picture",
            44 => "Capture timeout",
            45 => "Get stream timeout",
            _ => $"Unknown error ({errorCode})"
        };
    }

    private static void EnsureInitialized()
    {
        if (!NET_DVR._initialized)
        {
            if (!NET_DVR.NET_DVR_Init()) throw new InvalidOperationException("NET_DVR_Init failed.");

            NET_DVR._initialized = true;
            AppDomain.CurrentDomain.ProcessExit += (_, __) => NET_DVR.NET_DVR_Cleanup();
        }
    }

    private bool EnsureLogin(HikvisionDeviceConfig config, out int userId)
    {
        var key = BuildDeviceKey(config);

        userId = deviceKeyToUserId.AddOrUpdate(
            key,
            _ => Login(config),
            (_, existingUserId) => existingUserId >= 0 ? existingUserId : Login(config));

        return userId >= 0;
    }

    private int Login(HikvisionDeviceConfig config)
    {
        var devInfo = new NET_DVR.NET_DVR_DEVICEINFO_V40();
        var loginInfo = new NET_DVR.NET_DVR_USER_LOGIN_INFO
        {
            sDeviceAddress = ToFixedBytes(config.Ip, 129),
            sUserName = ToFixedBytes(config.Username, 64),
            sPassword = ToFixedBytes(config.Password, 64),
            wPort = (ushort)config.Port,
            bUseAsynLogin = 0
        };
        var userId = NET_DVR.NET_DVR_Login_V40(ref loginInfo, ref devInfo);
        
        if (userId < 0)
        {
            var errorCode = NET_DVR.NET_DVR_GetLastError();
            _logger?.LogWarning(
                "海康威视设备登录失败: IP={Ip}, Port={Port}, Username={Username}, ErrorCode={ErrorCode}",
                config.Ip, config.Port, config.Username, errorCode);
        }
        
        return userId >= 0 ? userId : -1;
    }

    private static byte[] ToFixedBytes(string text, int fixedLen)
    {
        var bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
        Array.Resize(ref bytes, fixedLen);
        return bytes;
    }
}

public sealed class HikvisionDeviceConfig
{
    public string Ip { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; }
    public int StreamType { get; set; }
    public int[] Channels { get; set; } = Array.Empty<int>();
}

public sealed class BatchCaptureRequest
{
    public HikvisionDeviceConfig Config { get; set; } = null!;
    public int Channel { get; set; }
    public string SaveFullPath { get; set; } = string.Empty;
    public string DeviceKey { get; set; } = string.Empty; // 用于标识设备，如 "192.168.1.100:8000"

    /// <summary>
    ///     从 CameraConfig 创建 BatchCaptureRequest
    /// </summary>
    /// <param name="cameraConfig">摄像头配置</param>
    /// <param name="basePath">保存路径的基础目录</param>
    /// <param name="logger">可选的日志记录器，用于记录警告信息</param>
    /// <returns>如果配置有效则返回 BatchCaptureRequest，否则返回 null</returns>
    public static BatchCaptureRequest? FromCameraConfig(CameraConfig cameraConfig, string basePath, ILogger? logger = null)
    {
        if (!cameraConfig.IsValid())
        {
            return null;
        }

        if (!int.TryParse(cameraConfig.Port, out var port) ||
            !int.TryParse(cameraConfig.Channel, out var channel))
        {
            logger?.LogWarning($"Invalid camera configuration: {cameraConfig.Name}");
            return null;
        }

        var hikvisionConfig = new HikvisionDeviceConfig
        {
            Ip = cameraConfig.Ip,
            Port = port,
            Username = cameraConfig.UserName,
            Password = cameraConfig.Password,
            Channels = new[] { channel }
        };

        var fileName = AttachmentPathUtils.GenerateMonitoringPhotoFileName(cameraConfig.Name, channel);
        var savePath = Path.Combine(basePath, fileName);

        return new BatchCaptureRequest
        {
            Config = hikvisionConfig,
            Channel = channel,
            SaveFullPath = savePath,
            DeviceKey = $"{cameraConfig.Ip}:{port}"
        };
    }
}

public sealed class BatchCaptureResult
{
    public BatchCaptureRequest Request { get; set; } = null!;
    public bool Success { get; set; }
    public uint HcNetSdkError { get; set; }
    public int PlayM4Error { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSize { get; set; }
}

internal static class NET_DVR
{
    private const int STREAM_ID_LEN = 32;

    // 码流数据类型定义
    internal const uint NET_DVR_SYSHEAD = 1; // 系统头数据
    internal const uint NET_DVR_STREAMDATA = 2; // 码流数据
    internal const uint NET_DVR_AUDIOSTREAMDATA = 3; // 音频数据
    internal const uint NET_DVR_PRIVATE_DATA = 112; // 私有数据，包括智能信息
    internal static bool _initialized;

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_Init();

    [DllImport("HCNetSDK.dll")]
    internal static extern void NET_DVR_Cleanup();

    [DllImport("HCNetSDK.dll")]
    internal static extern int NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO pLoginInfo,
        ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_Logout(int lUserID);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_CaptureJPEGPicture(int lUserID, int lChannel, ref NET_DVR_JPEGPARA lpJpegPara,
        byte[] sPicFileName);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_CaptureJPEGPicture_NEW(
        int lUserID,
        int lChannel,
        ref NET_DVR_JPEGPARA lpJpegPara,
        byte[] pJpegPicBuffer,      // 输出缓存区
        uint dwPicSize,              // 缓存区大小
        out uint lpSizeReturned);    // 返回的实际数据大小

    [DllImport("HCNetSDK.dll")]
    internal static extern uint NET_DVR_GetLastError();

    [DllImport("HCNetSDK.dll")]
    internal static extern int NET_DVR_RealPlay_V40(int lUserID, ref NET_DVR_PREVIEWINFO lpPreviewInfo,
        REALDATACALLBACK fRealDataCallBack, IntPtr pUser);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_StopRealPlay(int lRealHandle);

    [DllImport(@".\HCNetSDK.dll")]
    internal static extern bool NET_DVR_CapturePicture(int lRealHandle, string sPicFileName);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_DEVICEINFO_V30
    {
        public int dwSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] sSerialNumber;

        public int byAlarmInPortNum;
        public int byAlarmOutPortNum;
        public int byDiskNum;
        public int byDVRType;
        public int byChanNum;
        public int byStartChan;
        public int byAudioChanNum;
        public int byIPChanNum;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_DEVICEINFO_V40
    {
        public NET_DVR_DEVICEINFO_V30 struDeviceV30;
        public int bySupportLock;
        public int byRetryLoginTime;
        public int byPasswordLevel;
        public int byProxyType;
        public int dwSurplusLockTime;
        public int byCharEncodeType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_USER_LOGIN_INFO
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)]
        public byte[] sDeviceAddress;

        public byte byUseTransport;
        public ushort wPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] sUserName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] sPassword;

        public IntPtr cbLoginResult;
        public IntPtr pUser;
        public int bUseAsynLogin;
        public byte byProxyType;
        public byte byUseUTCTime;
        public byte byLoginMode;
        public byte byHttps;
        public int iProxyID;
        public byte byVerifyMode;
        public byte byRes3;
        public ushort wTaskNo;
        public int byRes4;
        public int byRes5;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_JPEGPARA
    {
        public ushort wPicSize;
        public ushort wPicQuality;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_PREVIEWINFO
    {
        public Int32 lChannel; // 通道号
        public uint dwStreamType; // 码流类型，0-主码流，1-子码流，2-码流3，3-码流4 等以此类推
        public uint dwLinkMode; // 0：TCP方式,1：UDP方式,2：多播方式,3 - RTP方式，4-RTP/RTSP,5-RSTP/HTTP
        public IntPtr hPlayWnd; // 播放窗口的句柄,为NULL表示不播放图象

        [MarshalAs(UnmanagedType.Bool)]
        public bool bBlocked; // 0-非阻塞取流, 1-阻塞取流, 如果阻塞SDK内部connect失败将会有5s的超时才能够返回,不适合于轮询取流操作.

        [MarshalAs(UnmanagedType.Bool)] public bool bPassbackRecord; // 0-不启用录像回传,1启用录像回传
        public byte byPreviewMode; // 预览模式，0-正常预览，1-延迟预览

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = STREAM_ID_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] byStreamID; // 流ID，lChannel为0xffffffff时启用此参数

        public byte byProtoType; // 应用层取流协议，0-私有协议，1-RTSP协议
        public byte byRes1;
        public byte byVideoCodingType; // 码流数据编解码类型 0-通用编码数据 1-热成像探测器产生的原始数据（温度数据的加密信息，通过去加密运算，将原始数据算出真实的温度值）
        public uint dwDisplayBufNum; // 播放库播放缓冲区最大缓冲帧数，范围1-50，置0时默认为1
        public byte byNPQMode; // NPQ是直连模式，还是过流媒体 0-直连 1-过流媒体

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 215, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes;
    }

    internal delegate void REALDATACALLBACK(int lRealHandle, uint dwDataType, IntPtr pBuffer, uint dwBufSize,
        IntPtr pUser);
}