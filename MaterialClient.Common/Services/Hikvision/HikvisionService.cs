using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MaterialClient.Common.Services.Hikvision;

public interface IHikvisionService
{
    void AddOrUpdateDevice(HikvisionDeviceConfig config);
    bool IsOnline(HikvisionDeviceConfig config);
    bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, int quality = 90);
    bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, out uint lastError, int quality = 1);
    bool TryOpenRealStream(HikvisionDeviceConfig config, int channel);
    bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath);
    Task<List<BatchCaptureResult>> CaptureJpegFromStreamBatchAsync(List<BatchCaptureRequest> requests);
}

public sealed class HikvisionService : IHikvisionService
{
    private readonly ConcurrentDictionary<string, int> deviceKeyToUserId = new();

    public static uint GetLastErrorCode() => NET_DVR.NET_DVR_GetLastError();

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
        var login = TryLogin(config, out var userId);
        if (login)
        {
            NET_DVR.NET_DVR_Logout(userId);
        }
        return login;
    }

    public bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath)) throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!TryLogin(config, out var userId))
        {
            return false;
        }

        try
        {
            NET_DVR.NET_DVR_JPEGPARA para = new NET_DVR.NET_DVR_JPEGPARA
            {
                wPicQuality = (ushort)Math.Clamp(quality, 0, 100),
                wPicSize = 0xFF, // use device default
            };
            var pathBytes = Encoding.ASCII.GetBytes(saveFullPath + "\0");
            bool ok = NET_DVR.NET_DVR_CaptureJPEGPicture(userId, channel, ref para, pathBytes);
            return ok;
        }
        finally
        {
            NET_DVR.NET_DVR_Logout(userId);
        }
    }

    public bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, out uint lastError, int quality = 1)
    {
        lastError = 0;
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath)) throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!TryLogin(config, out var userId))
        {
            lastError = NET_DVR.NET_DVR_GetLastError();
            return false;
        }

        try
        {
            NET_DVR.NET_DVR_JPEGPARA para = new NET_DVR.NET_DVR_JPEGPARA
            {
                wPicQuality = (ushort)Math.Clamp(quality, 1, 100),
                wPicSize = 0xFF,
            };
            var pathBytes = Encoding.ASCII.GetBytes(saveFullPath + "\0");
            bool ok = NET_DVR.NET_DVR_CaptureJPEGPicture(userId, channel, ref para, pathBytes);
            if (!ok)
            {
                lastError = NET_DVR.NET_DVR_GetLastError();
            }
            return ok;
        }
        finally
        {
            NET_DVR.NET_DVR_Logout(userId);
        }
    }

    // Placeholder for real-time stream obtaining. In many apps this returns a handle or starts a callback.
    public bool TryOpenRealStream(HikvisionDeviceConfig config, int channel)
    {
        ArgumentNullException.ThrowIfNull(config);
        EnsureInitialized();
        // Not implemented here to keep scope minimal for unit test; can be expanded later.
        return IsOnline(config);
    }

    public bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath)
    {
        return CaptureJpegFromStream(config, channel, saveFullPath, out _);
    }

    public bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath, out int playM4Error)
    {
        playM4Error = 0;
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(saveFullPath)) throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
        EnsureInitialized();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

        if (!TryLogin(config, out var userId))
        {
            return false;
        }

        int lRealHandle = -1;
        // 当 hPlayWnd 为 NULL 时，需要设置回调函数来处理码流数据
        // 参考文档：hPlayWnd 为 NULL 时仅取流不解码，需要手动解码（使用播放库 PlayM4）
        // 使用 PlayM4Decoder 进行手动解码
        IntPtr hPlayWnd = IntPtr.Zero; // NULL - 仅取流不解码，需要手动解码

        // 创建 PlayM4 解码器实例
        PlayM4Decoder? decoder = null;
        object streamLock = new object();

        // 回调函数用于接收码流数据（当 hPlayWnd 为 NULL 时）
        // 数据类型：NET_DVR_SYSHEAD(系统头), NET_DVR_STREAMDATA(码流数据) 等
        // 使用 PlayM4Decoder 进行手动解码
        NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
        {
            lock (streamLock)
            {
                switch (dataType)
                {
                    case NET_DVR.NET_DVR_SYSHEAD: // 系统头数据
                        if (bufSize > 0 && decoder != null && !decoder.IsInitialized)
                        {
                            // 使用系统头数据初始化播放库
                            // 获取桌面窗口句柄用于播放（即使不显示，也需要有效句柄）
                            IntPtr hWnd = NET_DVR.GetDesktopWindow();
                            if (!decoder.OpenStream(buffer, bufSize, hWnd))
                            {
                                // 初始化失败，记录错误
                                // 可以根据需要记录日志或抛出异常
                            }
                        }
                        break;

                    case NET_DVR.NET_DVR_STREAMDATA: // 码流数据
                        if (bufSize > 0 && decoder != null && decoder.IsPlaying)
                        {
                            // 将码流数据输入到播放库进行解码
                            decoder.InputData(buffer, bufSize);
                        }
                        break;

                    case NET_DVR.NET_DVR_AUDIOSTREAMDATA: // 音频数据
                        if (bufSize > 0)
                        {
                            // 音频数据处理（如果需要）
                            // PlayM4 也可以处理音频数据
                            if (decoder != null && decoder.IsPlaying)
                            {
                                decoder.InputData(buffer, bufSize);
                            }
                        }
                        break;

                    case NET_DVR.NET_DVR_PRIVATE_DATA: // 私有数据（包括智能信息）
                        if (bufSize > 0)
                        {
                            // 收到私有数据，可能包含智能分析信息
                            // 可以根据需要处理这些数据
                        }
                        break;

                    default:
                        // 其他类型的数据，也尝试输入到播放库
                        if (bufSize > 0 && decoder != null && decoder.IsPlaying)
                        {
                            decoder.InputData(buffer, bufSize);
                        }
                        break;
                }
            }
        };

        try
        {
            // 创建 PlayM4 解码器实例
            decoder = new PlayM4Decoder();

            // 启动实时预览
            // hPlayWnd 为 NULL 时仅取流不解码，需要手动解码；设为有效值则 SDK 自动解码
            NET_DVR.NET_DVR_PREVIEWINFO previewInfo = new NET_DVR.NET_DVR_PREVIEWINFO
            {
                lChannel = channel,
                dwStreamType = 0, // 主码流
                dwLinkMode = 0, // TCP模式
                hPlayWnd = hPlayWnd, // NULL - 仅取流不解码，需要手动解码
                bBlocked = true, // 阻塞取流
                bPassbackRecord = false, // 不启用录像回传
                byPreviewMode = 0, // 正常预览
                byStreamID = new byte[32],
                byProtoType = 0, // 私有协议
                byRes1 = 0,
                byVideoCodingType = 0, // 通用编码数据
                dwDisplayBufNum = 1, // 播放缓冲区最大缓冲帧数
                byNPQMode = 0, // 直连模式
                byRes = new byte[215]
            };

            // 当 hPlayWnd 为 NULL 时，必须设置回调函数来处理码流数据
            // 回调函数签名：void CALLBACK fRealDataCallBack(LONG lRealHandle, DWORD dwDataType, BYTE *pBuffer, DWORD dwBufSize, void* pUser)
            lRealHandle = NET_DVR.NET_DVR_RealPlay_V40(userId, ref previewInfo, realDataCallback, IntPtr.Zero);
            if (lRealHandle < 0)
            {
                return false;
            }

            // 等待解码器初始化和第一帧数据
            // 等待系统头数据被处理，解码器初始化完成
            int waitCount = 0;
            while (!decoder.IsPlaying && waitCount < 50) // 最多等待5秒
            {
                System.Threading.Thread.Sleep(100);
                waitCount++;
            }

            if (!decoder.IsPlaying)
            {
                // 解码器初始化失败
                if (decoder != null && decoder.Port >= 0)
                {
                    playM4Error = decoder.GetLastError();
                }
                return false;
            }

            // 等待一帧数据解码完成
            System.Threading.Thread.Sleep(500);

            // 使用 PlayM4_GetJPEG 捕获当前帧为 JPEG 图片
            bool ok = decoder.CaptureJpeg(saveFullPath);

            if (!ok && decoder != null && decoder.Port >= 0)
            {
                playM4Error = decoder.GetLastError();
            }

            return ok;
        }
        finally
        {
            // 释放解码器资源
            if (decoder != null)
            {
                // 如果之前没有获取错误码，现在尝试获取
                if (playM4Error == 0 && decoder.Port >= 0)
                {
                    playM4Error = decoder.GetLastError();
                }
                decoder.Dispose();
            }

            if (lRealHandle >= 0)
            {
                NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
            }
            NET_DVR.NET_DVR_Logout(userId);
        }
    }

    public async Task<List<BatchCaptureResult>> CaptureJpegFromStreamBatchAsync(List<BatchCaptureRequest> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return new List<BatchCaptureResult>();
        }

        // 使用并发处理多个设备
        var tasks = requests.Select(async request =>
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
                // 在后台线程中执行拍照操作（因为 CaptureJpegFromStream 是同步的阻塞操作）
                await Task.Run(() =>
                {
                    int playM4Error = 0;
                    result.Success = CaptureJpegFromStream(request.Config, request.Channel, request.SaveFullPath, out playM4Error);
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
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = "文件未创建";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private static string BuildDeviceKey(HikvisionDeviceConfig config)
        => $"{config.Ip}:{config.Port}:{config.Username}";

    private static void EnsureInitialized()
    {
        if (!NET_DVR._initialized)
        {
            if (!NET_DVR.NET_DVR_Init())
            {
                throw new InvalidOperationException("NET_DVR_Init failed.");
            }
            NET_DVR._initialized = true;
            AppDomain.CurrentDomain.ProcessExit += (_, __) => NET_DVR.NET_DVR_Cleanup();
        }
    }

    private static bool TryLogin(HikvisionDeviceConfig config, out int userId)
    {
        NET_DVR.NET_DVR_DEVICEINFO_V40 devInfo = new NET_DVR.NET_DVR_DEVICEINFO_V40();
        var loginInfo = new NET_DVR.NET_DVR_USER_LOGIN_INFO
        {
            sDeviceAddress = ToFixedBytes(config.Ip, 129),
            sUserName = ToFixedBytes(config.Username, 64),
            sPassword = ToFixedBytes(config.Password, 64),
            wPort = (ushort)config.Port,
            bUseAsynLogin = 0
        };
        userId = NET_DVR.NET_DVR_Login_V40(ref loginInfo, ref devInfo);
        return userId >= 0;
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
    internal static bool _initialized;
    private const int STREAM_ID_LEN = 32;

    // 码流数据类型定义
    internal const uint NET_DVR_SYSHEAD = 1; // 系统头数据
    internal const uint NET_DVR_STREAMDATA = 2; // 码流数据
    internal const uint NET_DVR_AUDIOSTREAMDATA = 3; // 音频数据
    internal const uint NET_DVR_PRIVATE_DATA = 112; // 私有数据，包括智能信息

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_DEVICEINFO_V30
    {
        public int dwSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] sSerialNumber;
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
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_USER_LOGIN_INFO
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)] public byte[] sDeviceAddress;
        public byte byUseTransport;
        public ushort wPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] sUserName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] sPassword;
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

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_Init();

    [DllImport("HCNetSDK.dll")]
    internal static extern void NET_DVR_Cleanup();

    [DllImport("HCNetSDK.dll")]
    internal static extern int NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO pLoginInfo, ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_Logout(int lUserID);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_CaptureJPEGPicture(int lUserID, int lChannel, ref NET_DVR_JPEGPARA lpJpegPara, byte[] sPicFileName);

    [DllImport("HCNetSDK.dll")]
    internal static extern uint NET_DVR_GetLastError();

    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_PREVIEWINFO
    {
        public Int32 lChannel; // 通道号
        public uint dwStreamType; // 码流类型，0-主码流，1-子码流，2-码流3，3-码流4 等以此类推
        public uint dwLinkMode; // 0：TCP方式,1：UDP方式,2：多播方式,3 - RTP方式，4-RTP/RTSP,5-RSTP/HTTP
        public IntPtr hPlayWnd; // 播放窗口的句柄,为NULL表示不播放图象
        [MarshalAs(UnmanagedType.Bool)]
        public bool bBlocked; // 0-非阻塞取流, 1-阻塞取流, 如果阻塞SDK内部connect失败将会有5s的超时才能够返回,不适合于轮询取流操作.
        [MarshalAs(UnmanagedType.Bool)]
        public bool bPassbackRecord; // 0-不启用录像回传,1启用录像回传
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

    [DllImport("HCNetSDK.dll")]
    internal static extern int NET_DVR_RealPlay_V40(int lUserID, ref NET_DVR_PREVIEWINFO lpPreviewInfo, NET_DVR.REALDATACALLBACK fRealDataCallBack, IntPtr pUser);

    [DllImport("HCNetSDK.dll")]
    internal static extern bool NET_DVR_StopRealPlay(int lRealHandle);

    [DllImport(@".\HCNetSDK.dll")]
    internal static extern bool NET_DVR_CapturePicture(Int32 lRealHandle, string sPicFileName);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    internal delegate void REALDATACALLBACK(int lRealHandle, uint dwDataType, IntPtr pBuffer, uint dwBufSize, IntPtr pUser);
}


