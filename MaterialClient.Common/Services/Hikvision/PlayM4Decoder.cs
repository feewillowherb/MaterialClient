using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     PlayM4 解码器，用于手动解码海康威视视频流
/// </summary>
public sealed class PlayM4Decoder : IDisposable
{
    // 流模式定义
    private const int STREAME_REALTIME = 0; // 实时流
    private const int STREAME_FILE = 1; // 文件流
    private readonly object _lockObject = new();
    private IntPtr _hPlayWnd = IntPtr.Zero;
    private int _port = -1; // 播放库端口号

    /// <summary>
    ///     是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     是否正在播放
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    ///     播放库端口号
    /// </summary>
    public int Port => _port;

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        lock (_lockObject)
        {
            Stop();
            CloseStream();

            if (_port >= 0)
            {
                PlayM4.PlayM4_FreePort(_port);
                _port = -1;
            }

            IsInitialized = false;
            IsPlaying = false;
        }
    }

    /// <summary>
    ///     获取最后一个错误码
    /// </summary>
    /// <returns>错误码</returns>
    public int GetLastError()
    {
        return PlayM4.PlayM4_GetLastError(_port);
    }

    /// <summary>
    ///     获取图片质量
    /// </summary>
    /// <returns>true 表示高质量，false 表示普通质量</returns>
    public bool GetPictureQuality()
    {
        lock (_lockObject)
        {
            if (_port < 0) return false;

            var bHighQuality = false;
            if (PlayM4.PlayM4_GetPictureQuality(_port, ref bHighQuality)) return bHighQuality;

            return false;
        }
    }

    /// <summary>
    ///     设置图片质量
    /// </summary>
    /// <param name="highQuality">true 表示高质量，false 表示普通质量</param>
    /// <returns>是否成功</returns>
    public bool SetPictureQuality(long highQuality)
    {
        lock (_lockObject)
        {
            if (_port < 0) return false;

            return PlayM4.PlayM4_SetJpegQuality(highQuality);
        }
    }


    /// <summary>
    ///     初始化播放库并获取端口
    /// </summary>
    /// <returns>是否成功</returns>
    public bool Initialize()
    {
        lock (_lockObject)
        {
            if (IsInitialized) return true;

            if (_port >= 0) return true; // 已经获取过端口

            // 获取播放库未使用的通道号
            if (!PlayM4.PlayM4_GetPort(ref _port)) return false;

            IsInitialized = _port >= 0;
            return IsInitialized;
        }
    }

    /// <summary>
    ///     打开流并开始播放
    /// </summary>
    /// <param name="systemHeader">系统头数据</param>
    /// <param name="headerSize">系统头大小</param>
    /// <param name="hPlayWnd">播放窗口句柄，为 IntPtr.Zero 表示不显示</param>
    /// <returns>是否成功</returns>
    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        lock (_lockObject)
        {
            if (!IsInitialized)
                if (!Initialize())
                    return false;

            if (_port < 0) return false;

            _hPlayWnd = hPlayWnd;

            // 设置实时流播放模式
            if (!PlayM4.PlayM4_SetStreamOpenMode(_port, STREAME_REALTIME)) return false;

            // 打开流接口
            // 参数：端口号、系统头数据、系统头大小、缓冲区大小（1MB）
            if (!PlayM4.PlayM4_OpenStream(_port, systemHeader, headerSize, 1024 * 1024 * 10)) return false;

            // 开始播放
            if (!PlayM4.PlayM4_Play(_port, _hPlayWnd))
            {
                PlayM4.PlayM4_CloseStream(_port);
                return false;
            }

            IsPlaying = true;
            return true;
        }
    }

    /// <summary>
    ///     输入码流数据
    /// </summary>
    /// <param name="data">数据指针</param>
    /// <param name="dataSize">数据大小</param>
    /// <returns>是否成功</returns>
    public bool InputData(IntPtr data, uint dataSize)
    {
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0 || dataSize == 0) return false;

            return PlayM4.PlayM4_InputData(_port, data, dataSize);
        }
    }

    /// <summary>
    ///     停止播放
    /// </summary>
    public void Stop()
    {
        lock (_lockObject)
        {
            if (IsPlaying && _port >= 0)
            {
                PlayM4.PlayM4_Stop(_port);
                IsPlaying = false;
            }
        }
    }

    /// <summary>
    ///     关闭流
    /// </summary>
    public void CloseStream()
    {
        lock (_lockObject)
        {
            if (_port >= 0) PlayM4.PlayM4_CloseStream(_port);
        }
    }

    /// <summary>
    ///     捕获当前帧为 JPEG 图片
    /// </summary>
    /// <param name="savePath">保存路径</param>
    /// <returns>是否成功</returns>
    public bool CaptureJpeg(string savePath)
    {
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0) return false;

            SetPictureQuality(100);

            // 分配缓冲区用于存储 JPEG 数据（通常 1MB 足够）
            const int bufferSize = 1024 * 1024 * 10;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint jpegSize = 0;
                // 获取 JPEG 数据
                if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize)) return false;

                if (jpegSize == 0 || jpegSize > bufferSize) return false;

                // 将数据从非托管内存复制到字节数组
                var jpegData = new byte[jpegSize];
                Marshal.Copy(buffer, jpegData, 0, (int)jpegSize);

                // 写入文件
                File.WriteAllBytes(savePath, jpegData);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}

/// <summary>
///     PlayM4 播放库 P/Invoke 声明
/// </summary>
internal static class PlayM4
{
    private const string DllName = "PlayCtrl.dll";

    /// <summary>
    ///     获取播放库未使用的通道号
    /// </summary>
    /// <param name="nPort">输出参数，返回端口号</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetPort(ref int nPort);

    /// <summary>
    ///     设置流打开模式
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="nMode">模式：0-实时流，1-文件流</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_SetStreamOpenMode(int nPort, int nMode);

    /// <summary>
    ///     打开流
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="pFileHeadBuf">系统头数据指针</param>
    /// <param name="nSize">系统头大小</param>
    /// <param name="nBufPoolSize">缓冲区大小</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_OpenStream(int nPort, IntPtr pFileHeadBuf, uint nSize, uint nBufPoolSize);

    /// <summary>
    ///     开始播放
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="hWnd">播放窗口句柄</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_Play(int nPort, IntPtr hWnd);

    /// <summary>
    ///     输入数据
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="pBuf">数据指针</param>
    /// <param name="nSize">数据大小</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_InputData(int nPort, IntPtr pBuf, uint nSize);

    /// <summary>
    ///     停止播放
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_Stop(int nPort);

    /// <summary>
    ///     关闭流
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_CloseStream(int nPort);

    /// <summary>
    ///     释放端口
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_FreePort(int nPort);

    /// <summary>
    ///     获取当前帧的 JPEG 图片
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="pJpeg">JPEG 数据缓冲区</param>
    /// <param name="nBufSize">缓冲区大小</param>
    /// <param name="pJpegSize">输出参数，返回实际 JPEG 数据大小</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetJPEG(int nPort, IntPtr pJpeg, uint nBufSize, ref uint pJpegSize);

    /// <summary>
    ///     获取最后一个错误码
    /// </summary>
    /// <returns>错误码</returns>
    [DllImport(DllName)]
    internal static extern int PlayM4_GetLastError(int nPort);

    /// <summary>
    ///     获取图片质量
    /// </summary>
    /// <param name="nPort">端口号</param>
    /// <param name="bHighQuality">输出参数，true 表示高质量，false 表示普通质量</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetPictureQuality(int nPort, ref bool bHighQuality);


    /// <summary>
    ///     设置全局 JPEG 质量（适用于所有端口）
    /// </summary>
    /// <param name="nQuality">质量值，通常范围 0-100，值越大质量越高</param>
    /// <returns>是否成功</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_SetJpegQuality(long nQuality);
}