using System.Buffers;
using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     PlayM4 decoder for manual decoding of Hikvision video streams.
///     Implements proper dispose pattern and thread-safe operations.
/// </summary>
public sealed class PlayM4Decoder : IDisposable
{
    // Stream mode definitions
    private const int STREAME_REALTIME = 0; // Real-time stream
    private const int STREAME_FILE = 1; // File stream

    private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    private readonly object _lockObject = new();
    private readonly ManualResetEventSlim _playingEvent = new(false);

    private IntPtr _hPlayWnd = IntPtr.Zero;
    private int _port = -1; // Playback library port number
    private bool _disposed;

    /// <summary>
    ///     Whether the decoder is initialized
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     Whether the decoder is playing
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    ///     Playback library port number
    /// </summary>
    public int Port => _port;

    /// <summary>
    ///     Dispose resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Finalizer
    /// </summary>
    ~PlayM4Decoder()
    {
        Dispose(false);
    }

    /// <summary>
    ///     Dispose implementation with proper pattern
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_disposed) return; // Double-check inside lock

            try
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _playingEvent?.Dispose();
                }

                // Dispose unmanaged resources - wrap each in try-catch
                try
                {
                    Stop();
                }
                catch
                {
                    // Ignore errors during cleanup
                }

                try
                {
                    CloseStreamInternal();
                }
                catch
                {
                    // Ignore errors during cleanup
                }

                if (_port >= 0)
                {
                    try
                    {
                        PlayM4.PlayM4_FreePort(_port);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                    finally
                    {
                        _port = -1;
                    }
                }

                IsInitialized = false;
                IsPlaying = false;
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    ///     Throws ObjectDisposedException if the decoder has been disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PlayM4Decoder));
    }

    /// <summary>
    ///     Gets the last error code
    /// </summary>
    /// <returns>Error code</returns>
    public int GetLastError()
    {
        return PlayM4.PlayM4_GetLastError(_port);
    }

    /// <summary>
    ///     Gets the picture quality setting
    /// </summary>
    /// <returns>True for high quality, false for normal quality</returns>
    public bool GetPictureQuality()
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (_port < 0) return false;

            var bHighQuality = false;
            if (PlayM4.PlayM4_GetPictureQuality(_port, ref bHighQuality)) return bHighQuality;

            return false;
        }
    }

    /// <summary>
    ///     Sets the picture quality
    /// </summary>
    /// <param name="highQuality">Quality value (0-100)</param>
    /// <returns>Whether successful</returns>
    public bool SetPictureQuality(long highQuality)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (_port < 0) return false;

            return PlayM4.PlayM4_SetJpegQuality(highQuality);
        }
    }


    /// <summary>
    ///     Initializes the playback library and acquires a port
    /// </summary>
    /// <returns>Whether successful</returns>
    public bool Initialize()
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (IsInitialized) return true;

            if (_port >= 0) return true; // Port already acquired

            // Get an unused channel number from the playback library
            if (!PlayM4.PlayM4_GetPort(ref _port)) return false;

            IsInitialized = _port >= 0;
            return IsInitialized;
        }
    }

    /// <summary>
    ///     Opens stream and starts playback
    /// </summary>
    /// <param name="systemHeader">System header data pointer</param>
    /// <param name="headerSize">System header size</param>
    /// <param name="hPlayWnd">Playback window handle, IntPtr.Zero for no display</param>
    /// <returns>Whether successful</returns>
    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            // Parameter validation
            if (systemHeader == IntPtr.Zero || headerSize == 0)
                return false;

            // Prevent duplicate opening - return true if already playing
            if (IsPlaying)
                return true;

            if (!IsInitialized)
                if (!Initialize())
                    return false;

            if (_port < 0) return false;

            _hPlayWnd = hPlayWnd;

            // Set real-time stream playback mode
            if (!PlayM4.PlayM4_SetStreamOpenMode(_port, STREAME_REALTIME))
            {
                // Log error if needed: SetStreamOpenMode failed
                return false;
            }

            // Open stream interface
            // Parameters: port, system header data, header size, buffer size (10MB)
            if (!PlayM4.PlayM4_OpenStream(_port, systemHeader, headerSize, 1024 * 1024 * 10))
            {
                // Log error if needed: OpenStream failed
                return false;
            }

            // Start playback
            if (!PlayM4.PlayM4_Play(_port, _hPlayWnd))
            {
                // Cleanup on failure
                PlayM4.PlayM4_CloseStream(_port);
                return false;
            }

            IsPlaying = true;
            _playingEvent.Set(); // Signal that playback has started
            return true;
        }
    }

    /// <summary>
    ///     Waits for the decoder to start playing
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>True if playing started within timeout, false otherwise</returns>
    public bool WaitForPlaying(int timeoutMs = 5000)
    {
        ThrowIfDisposed();
        return _playingEvent.Wait(timeoutMs);
    }

    /// <summary>
    ///     Inputs stream data for decoding
    /// </summary>
    /// <param name="data">Data pointer</param>
    /// <param name="dataSize">Data size</param>
    /// <returns>Whether successful</returns>
    public bool InputData(IntPtr data, uint dataSize)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0 || dataSize == 0) return false;

            return PlayM4.PlayM4_InputData(_port, data, dataSize);
        }
    }

    /// <summary>
    ///     Stops playback
    /// </summary>
    public void Stop()
    {
        lock (_lockObject)
        {
            if (IsPlaying && _port >= 0)
            {
                PlayM4.PlayM4_Stop(_port);
                IsPlaying = false;
                _playingEvent.Reset();
            }
        }
    }

    /// <summary>
    ///     Closes the stream
    /// </summary>
    public void CloseStream()
    {
        ThrowIfDisposed();
        CloseStreamInternal();
    }

    /// <summary>
    ///     Internal close stream - doesn't check disposal state
    /// </summary>
    private void CloseStreamInternal()
    {
        lock (_lockObject)
        {
            if (_port >= 0) PlayM4.PlayM4_CloseStream(_port);
        }
    }

    /// <summary>
    ///     Captures the current frame as a JPEG image
    /// </summary>
    /// <param name="savePath">Path to save the image</param>
    /// <returns>Whether successful</returns>
    public bool CaptureJpeg(string savePath)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0) return false;

            SetPictureQuality(100);

            // Allocate buffer for JPEG data (10MB should be sufficient)
            const int bufferSize = 1024 * 1024 * 10;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            byte[]? rentedBuffer = null;

            try
            {
                uint jpegSize = 0;
                // Get JPEG data
                if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize))
                    return false;

                if (jpegSize == 0 || jpegSize > bufferSize)
                    return false;

                // Rent buffer from ArrayPool to avoid LOH allocation
                rentedBuffer = _bufferPool.Rent((int)jpegSize);
                Marshal.Copy(buffer, rentedBuffer, 0, (int)jpegSize);

                // Write only the actual data size to file
                File.WriteAllBytes(savePath, rentedBuffer.AsSpan(0, (int)jpegSize).ToArray());
                return true;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);

                if (rentedBuffer != null)
                    _bufferPool.Return(rentedBuffer);
            }
        }
    }
}

/// <summary>
///     PlayM4 playback library P/Invoke declarations
/// </summary>
internal static class PlayM4
{
    private const string DllName = "PlayCtrl.dll";

    /// <summary>
    ///     Gets an unused channel number from the playback library
    /// </summary>
    /// <param name="nPort">Output parameter, returns port number</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetPort(ref int nPort);

    /// <summary>
    ///     Sets the stream open mode
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="nMode">Mode: 0-real-time stream, 1-file stream</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_SetStreamOpenMode(int nPort, int nMode);

    /// <summary>
    ///     Opens a stream
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="pFileHeadBuf">System header data pointer</param>
    /// <param name="nSize">System header size</param>
    /// <param name="nBufPoolSize">Buffer size</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_OpenStream(int nPort, IntPtr pFileHeadBuf, uint nSize, uint nBufPoolSize);

    /// <summary>
    ///     Starts playback
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="hWnd">Playback window handle</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_Play(int nPort, IntPtr hWnd);

    /// <summary>
    ///     Inputs data for decoding
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="pBuf">Data pointer</param>
    /// <param name="nSize">Data size</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_InputData(int nPort, IntPtr pBuf, uint nSize);

    /// <summary>
    ///     Stops playback
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_Stop(int nPort);

    /// <summary>
    ///     Closes a stream
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_CloseStream(int nPort);

    /// <summary>
    ///     Frees a port
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_FreePort(int nPort);

    /// <summary>
    ///     Gets the current frame as a JPEG image
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="pJpeg">JPEG data buffer</param>
    /// <param name="nBufSize">Buffer size</param>
    /// <param name="pJpegSize">Output parameter, returns actual JPEG data size</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetJPEG(int nPort, IntPtr pJpeg, uint nBufSize, ref uint pJpegSize);

    /// <summary>
    ///     Gets the last error code
    /// </summary>
    /// <returns>Error code</returns>
    [DllImport(DllName)]
    internal static extern int PlayM4_GetLastError(int nPort);

    /// <summary>
    ///     Gets the picture quality setting
    /// </summary>
    /// <param name="nPort">Port number</param>
    /// <param name="bHighQuality">Output parameter, true for high quality, false for normal</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_GetPictureQuality(int nPort, ref bool bHighQuality);


    /// <summary>
    ///     Sets the global JPEG quality (applies to all ports)
    /// </summary>
    /// <param name="nQuality">Quality value, typically 0-100, higher is better quality</param>
    /// <returns>Whether successful</returns>
    [DllImport(DllName)]
    internal static extern bool PlayM4_SetJpegQuality(long nQuality);
}
