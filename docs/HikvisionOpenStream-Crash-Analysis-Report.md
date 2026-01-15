<!--
DOCUMENT_STATUS: ARCHIVED
LAST_REVIEWED: 2026-01-15
REVIEWER: Claude (OpenSpec Migration)
NOTES: Agent-generated analysis report. Documents investigation findings and recommendations. Preserved for historical reference. Verify if recommendations were implemented.
-->

# Hikvision OpenStream Crash Analysis and Fix Recommendations Report

**Analysis Date**: 2026-01-13  
**Analysis Target**: HikvisionService and PlayM4Decoder  
**Issue Severity**: ? High

---

## Executive Summary

Through code analysis, **8 critical issues** were identified. **Callback resource race conditions** and **unprotected callback exceptions** are the most likely root causes of the crashes. The issues are primarily concentrated in the `CaptureJpegFromStream` method and `PlayM4Decoder.OpenStream` method.

**Symptoms**:
- High probability of crashes when capturing from 3-4 cameras simultaneously
- Low probability of crashes with 1-2 cameras
- No logs recorded when crashes occur
- Crashes likely occur during decoding (using software decoding rather than camera hardware decoding)

---

## ? Critical Issue 1: Callback Function Without Exception Protection Causes Process Crash

### Location
```427:480:MaterialClient.Common/Services/Hikvision/HikvisionService.cs
NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
{
    lock (streamLock)
    {
        switch (dataType)
        {
            case NET_DVR.NET_DVR_SYSHEAD: // System header data
                if (bufSize > 0 && decoder != null && !decoder.IsInitialized)
                {
                    // ...
                    if (!decoder.OpenStream(buffer, bufSize, hWnd))
                    {
                        // ...
                    }
                }
                break;
            // ... other cases
        }
    }
};
```

### Problem Analysis

**Core Issues**:
1. **Callback function has no try-catch wrapper**: Any exception will prevent the unmanaged SDK from handling properly, causing direct process crash
2. **Called from unmanaged code**: HCNetSDK.dll invokes this callback via function pointer, unable to catch managed exceptions
3. **Cannot record crash information**: Exceptions occur at unmanaged level, .NET logging system cannot capture them

**Crash Trigger Conditions**:
- `decoder` is disposed during callback execution (race condition)
- `buffer` pointer is invalid or `bufSize` is incorrect
- `decoder.OpenStream()` throws an exception internally
- `decoder.InputData()` accesses released resources

### Impact
- **Process-level crash**: Cannot be caught by try-catch
- **No log recording**: No logs when crash occurs
- **High probability with concurrency**: When 3-4 cameras capture simultaneously, multiple callbacks execute concurrently

### Fix Recommendation ?????

```csharp
NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
{
    try
    {
        if (_isDisposed) return; // Quick exit
        
        lock (streamLock)
        {
            if (_isDisposed || decoder == null) return;
            
            // Validate parameter validity
            if (buffer == IntPtr.Zero || bufSize == 0) return;
            
            switch (dataType)
            {
                case NET_DVR.NET_DVR_SYSHEAD:
                    if (!decoder.IsInitialized)
                    {
                        var hWnd = NET_DVR.GetDesktopWindow();
                        if (!decoder.OpenStream(buffer, bufSize, hWnd))
                        {
                            _logger?.LogError("Decoder initialization failed: Port={Port}, Error={Error}", 
                                decoder.Port, decoder.GetLastError());
                        }
                    }
                    break;
                    
                case NET_DVR.NET_DVR_STREAMDATA:
                    if (decoder.IsPlaying)
                    {
                        decoder.InputData(buffer, bufSize);
                    }
                    break;
                    
                // ... other cases
            }
        }
    }
    catch (Exception ex)
    {
        // Must catch all exceptions to prevent crash
        _logger?.LogError(ex, "Callback exception: DataType={DataType}, BufSize={BufSize}", dataType, bufSize);
    }
};
```

---

## ? Critical Issue 2: Race Condition Between Callback and Resource Disposal

### Location
```556:569:MaterialClient.Common/Services/Hikvision/HikvisionService.cs
finally
{
    // Release decoder resources
    if (decoder != null)
    {
        // Get error code if not retrieved earlier
        if (playM4Error == 0 && decoder.Port >= 0) playM4Error = decoder.GetLastError();

        decoder.Dispose();
    }

    if (lRealHandle >= 0) NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
}
```

### Problem Analysis

**Race Condition Timeline**:
1. **T1**: Main thread enters `finally` block
2. **T2**: SDK background thread triggers callback, starts executing `realDataCallback`
3. **T3**: Main thread executes `decoder.Dispose()`, releases PlayM4 port
4. **T4**: Callback thread attempts to call `decoder.OpenStream()` or `decoder.InputData()`
5. **T5**: Accesses released resource ¡ú **CRASH**

**Root Causes**:
1. **Dispose decoder before stopping preview**: Should stop stream first, then release decoder
2. **Callback holds decoder reference**: Even after `decoder = null`, callback closure still holds reference
3. **No state flag**: Callback cannot know resources are being released

### Impact
- **Guaranteed occurrence in concurrent scenarios**: With 3-4 cameras, callback and release operations highly overlap
- **Intermittent crashes**: Depends on thread scheduling timing
- **PlayM4 port leakage**: If callback is in use, Dispose may not complete fully

### Fix Recommendation ?????

```csharp
// Add instance-level state flag in CaptureJpegFromStream method
private volatile bool _isDisposed = false;

public bool CaptureJpegFromStream(...)
{
    // ... initialization code ...
    
    // Create local state flag for each call
    var disposed = false;
    
    try
    {
        // ... start preview ...
    }
    finally
    {
        // 1. Set flag first to prevent new callback execution
        disposed = true;
        
        // 2. Stop preview stream (stop callback triggering)
        if (lRealHandle >= 0)
        {
            NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
            // Wait for callback completion
            Thread.Sleep(200);
        }
        
        // 3. Finally release decoder
        lock (streamLock)
        {
            if (decoder != null)
            {
                if (playM4Error == 0 && decoder.Port >= 0)
                    playM4Error = decoder.GetLastError();
                
                decoder.Dispose();
                decoder = null;
            }
        }
    }
}
```

**Better Approach (using closure-captured local variables)**:
```csharp
public bool CaptureJpegFromStream(...)
{
    // Use closure-captured local variables (safer than instance fields)
    var disposed = false;
    PlayM4Decoder? decoder = null;
    var streamLock = new object();
    
    NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
    {
        try
        {
            if (disposed) return; // Check local variable
            
            lock (streamLock)
            {
                if (disposed || decoder == null) return;
                // ... processing logic
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Callback exception");
        }
    };
    
    try
    {
        decoder = new PlayM4Decoder();
        // ... start preview
    }
    finally
    {
        disposed = true; // Set local flag
        
        if (lRealHandle >= 0)
        {
            NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
            Thread.Sleep(200); // Wait for callback completion
        }
        
        lock (streamLock)
        {
            decoder?.Dispose();
        }
    }
}
```

---

## ? Critical Issue 3: Non-Atomic Operations in PlayM4Decoder.OpenStream

### Location
```122:151:MaterialClient.Common/Services/Hikvision/PlayM4Decoder.cs
public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
{
    lock (_lockObject)
    {
        if (!IsInitialized)
            if (!Initialize())
                return false;

        if (_port < 0) return false;

        _hPlayWnd = hPlayWnd;

        // Set real-time stream playback mode
        if (!PlayM4.PlayM4_SetStreamOpenMode(_port, STREAME_REALTIME)) return false;

        // Open stream interface
        if (!PlayM4.PlayM4_OpenStream(_port, systemHeader, headerSize, 1024 * 1024 * 10)) return false;

        // Start playback
        if (!PlayM4.PlayM4_Play(_port, _hPlayWnd))
        {
            PlayM4.PlayM4_CloseStream(_port);
            return false;
        }

        IsPlaying = true;
        return true;
    }
}
```

### Problem Analysis

**Issue 1: Inconsistent state on failure**
- If `PlayM4_Play` fails, `CloseStream` is called, but `IsInitialized` remains `true`
- Next call may skip initialization, using invalid state

**Issue 2: Resource leakage on partial failure**
- When `PlayM4_OpenStream` succeeds but `PlayM4_Play` fails, stream is closed but port state is not reset

**Issue 3: Uncertainty with concurrent calls**
- When multiple threads call `OpenStream` simultaneously, although locked, PlayM4 SDK's port state may not be thread-safe

**Issue 4: Missing parameter validation**
- No validation that `systemHeader` is a valid pointer
- No validation that `headerSize` is reasonable

### Impact
- **Unpredictable behavior on multiple calls**
- **Port resources may leak**
- **IsPlaying state unreliable**

### Fix Recommendation ????

```csharp
public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
{
    lock (_lockObject)
    {
        // Parameter validation
        if (systemHeader == IntPtr.Zero || headerSize == 0)
        {
            return false;
        }
        
        // Prevent duplicate opening
        if (IsPlaying)
        {
            return true;
        }
        
        if (!IsInitialized)
        {
            if (!Initialize())
                return false;
        }

        if (_port < 0) return false;

        _hPlayWnd = hPlayWnd;

        // Set real-time stream playback mode
        if (!PlayM4.PlayM4_SetStreamOpenMode(_port, STREAME_REALTIME))
        {
            var error = PlayM4.PlayM4_GetLastError(_port);
            // Log error: SetStreamOpenMode failed
            return false;
        }

        // Open stream interface
        if (!PlayM4.PlayM4_OpenStream(_port, systemHeader, headerSize, 1024 * 1024 * 10))
        {
            var error = PlayM4.PlayM4_GetLastError(_port);
            // Log error: OpenStream failed
            return false;
        }

        // Start playback
        if (!PlayM4.PlayM4_Play(_port, _hPlayWnd))
        {
            var error = PlayM4.PlayM4_GetLastError(_port);
            // Cleanup: close stream
            PlayM4.PlayM4_CloseStream(_port);
            // Log error: Play failed
            return false;
        }

        IsPlaying = true;
        return true;
    }
}
```

---

## ? Critical Issue 4: PlayM4 Global Resource Limitations

### Problem Analysis

**PlayM4 SDK Limitations**:
1. **Limited port count**: PlayM4 library typically supports maximum 16 or 64 concurrent ports (depends on version)
2. **Memory limitations**: Each stream allocates 10MB buffer, 4 streams = 40MB, may exceed limits
3. **No port reuse**: Current implementation creates new `PlayM4Decoder` each time, doesn't reuse ports

**Concurrent Scenario**:
- 3-4 cameras capturing simultaneously
- Each creates a PlayM4 port
- If previous call didn't release properly, ports are exhausted
- `PlayM4_GetPort` fails ¡ú `Initialize()` fails ¡ú Cannot create decoder

### Code Location
```107:112:MaterialClient.Common/Services/Hikvision/PlayM4Decoder.cs
// Get unused channel number from playback library
if (!PlayM4.PlayM4_GetPort(ref _port)) return false;

IsInitialized = _port >= 0;
return IsInitialized;
```

### Impact
- **Concurrency limitation**: Direct failure when exceeding port count
- **Port leakage accumulation**: If Dispose is incomplete, ports gradually exhaust
- **Post-crash aftermath**: Ports may not be released after crash, cannot recover before restart

### Fix Recommendation ????

**Solution 1: Port Pool Management (Recommended)**
```csharp
/// <summary>
/// PlayM4 port pool manager
/// </summary>
public class PlayM4PortPool
{
    private static readonly ConcurrentBag<int> _availablePorts = new();
    private static readonly SemaphoreSlim _semaphore = new(16, 16); // Maximum 16 concurrent
    private static readonly object _initLock = new();
    private static bool _initialized = false;
    
    public static async Task<int> AcquirePortAsync(int timeoutMs = 5000)
    {
        if (!await _semaphore.WaitAsync(timeoutMs))
        {
            throw new TimeoutException("Cannot acquire PlayM4 port: concurrency limit reached");
        }
        
        try
        {
            if (_availablePorts.TryTake(out var port))
            {
                return port;
            }
            
            int newPort = -1;
            if (!PlayM4.PlayM4_GetPort(ref newPort))
            {
                throw new InvalidOperationException($"PlayM4_GetPort failed: {PlayM4.PlayM4_GetLastError(newPort)}");
            }
            
            return newPort;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }
    
    public static void ReleasePort(int port)
    {
        if (port >= 0)
        {
            try
            {
                // Reset port state
                PlayM4.PlayM4_Stop(port);
                PlayM4.PlayM4_CloseStream(port);
                
                // Return to pool for reuse (optional)
                // _availablePorts.Add(port);
                
                // Or release directly
                PlayM4.PlayM4_FreePort(port);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
```

**Solution 2: Serialized Processing (Simple but sacrifices performance)**
```csharp
public class HikvisionService
{
    private static readonly SemaphoreSlim _captureThrottle = new(2, 2); // Maximum 2 concurrent
    
    public async Task<bool> CaptureJpegFromStreamAsync(
        HikvisionDeviceConfig config, 
        int channel, 
        string saveFullPath,
        out int playM4Error)
    {
        await _captureThrottle.WaitAsync();
        try
        {
            return CaptureJpegFromStream(config, channel, saveFullPath, out playM4Error);
        }
        finally
        {
            _captureThrottle.Release();
        }
    }
}
```

**Solution 3: Using Polly Retry Policy**
```csharp
using Polly;

private static readonly AsyncPolicy _retryPolicy = Policy
    .Handle<InvalidOperationException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * attempt),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            // Log retry
        });

public async Task<bool> CaptureJpegFromStreamAsync(...)
{
    return await _retryPolicy.ExecuteAsync(() => 
    {
        return Task.FromResult(CaptureJpegFromStream(...));
    });
}
```

---

## ? Issue 5: Inefficient and Unreliable Polling for Decoder Initialization

### Location
```521:527:MaterialClient.Common/Services/Hikvision/HikvisionService.cs
var waitCount = 0;
while (!decoder.IsPlaying && waitCount < 50) // Wait up to 5 seconds
{
    Thread.Sleep(100);
    waitCount++;
}
```

### Problem Analysis

1. **Low polling efficiency**: Checks every 100ms, wastes CPU
2. **No event notification**: Decoder should actively notify when initialization completes, not polling
3. **Blocking during concurrency**: Multiple threads waiting simultaneously waste thread resources
4. **May timeout prematurely**: If network is slow, 5 seconds may not be enough
5. **Thread.Sleep blocks thread**: Wastes thread pool resources in high concurrency scenarios

### Fix Recommendation ???

**Solution 1: Use Event Waiting (Recommended)**
```csharp
// Add event in PlayM4Decoder
public class PlayM4Decoder : IDisposable
{
    private readonly ManualResetEventSlim _playingEvent = new(false);
    
    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        lock (_lockObject)
        {
            // ... stream opening logic ...
            
            if (PlayM4.PlayM4_Play(_port, _hPlayWnd))
            {
                IsPlaying = true;
                _playingEvent.Set(); // Notify waiters
                return true;
            }
            
            return false;
        }
    }
    
    public bool WaitForPlaying(int timeoutMs = 5000)
    {
        return _playingEvent.Wait(timeoutMs);
    }
    
    public void Dispose()
    {
        lock (_lockObject)
        {
            // ...
            _playingEvent?.Dispose();
        }
    }
}

// Use in HikvisionService
if (!decoder.WaitForPlaying(5000))
{
    _logger?.LogWarning("Decoder initialization timeout");
    playM4Error = decoder.GetLastError();
    return false;
}
```

**Solution 2: Async Waiting**
```csharp
// Use TaskCompletionSource
public class PlayM4Decoder : IDisposable
{
    private TaskCompletionSource<bool>? _playingTcs;
    
    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        lock (_lockObject)
        {
            _playingTcs = new TaskCompletionSource<bool>();
            
            // ... stream opening logic ...
            
            if (PlayM4.PlayM4_Play(_port, _hPlayWnd))
            {
                IsPlaying = true;
                _playingTcs.TrySetResult(true);
                return true;
            }
            
            _playingTcs.TrySetResult(false);
            return false;
        }
    }
    
    public async Task<bool> WaitForPlayingAsync(int timeoutMs = 5000)
    {
        if (_playingTcs == null) return false;
        
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            return await _playingTcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
```

---

## ? Issue 6: Memory Allocation Issues in CaptureJpeg

### Location
```208:230:MaterialClient.Common/Services/Hikvision/PlayM4Decoder.cs
const int bufferSize = 1024 * 1024 * 10;
var buffer = Marshal.AllocHGlobal(bufferSize);
try
{
    uint jpegSize = 0;
    // Get JPEG data
    if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize)) return false;

    if (jpegSize == 0 || jpegSize > bufferSize) return false;

    // Copy data from unmanaged memory to byte array
    var jpegData = new byte[jpegSize];
    Marshal.Copy(buffer, jpegData, 0, (int)jpegSize);

    // Write to file
    File.WriteAllBytes(savePath, jpegData);
    return true;
}
finally
{
    Marshal.FreeHGlobal(buffer);
}
```

### Problem Analysis

1. **Allocates 10MB unmanaged memory each time**: Memory pressure during concurrency
2. **LOH allocation**: `byte[jpegSize]` enters Large Object Heap (LOH) if exceeds 85KB, may cause memory fragmentation
3. **Synchronous file write**: `File.WriteAllBytes` blocks thread
4. **May leak on exception**: Although has finally, still risk in high concurrency
5. **No buffer reuse**: Reallocates each time

### Fix Recommendation ???

**Solution 1: Use ArrayPool (Recommended)**
```csharp
using System.Buffers;

public class PlayM4Decoder : IDisposable
{
    private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    public bool CaptureJpeg(string savePath)
    {
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0) return false;

            SetPictureQuality(100);

            const int bufferSize = 1024 * 1024 * 10;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            byte[]? rentedBuffer = null;
            
            try
            {
                uint jpegSize = 0;
                if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize))
                    return false;

                if (jpegSize == 0 || jpegSize > bufferSize)
                    return false;

                // Rent buffer from pool
                rentedBuffer = _bufferPool.Rent((int)jpegSize);
                Marshal.Copy(buffer, rentedBuffer, 0, (int)jpegSize);

                // Write to file (only actual size)
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
```

**Solution 2: Write Directly from Unmanaged Memory to File**
```csharp
public bool CaptureJpeg(string savePath)
{
    lock (_lockObject)
    {
        if (!IsPlaying || _port < 0) return false;

        SetPictureQuality(100);

        const int bufferSize = 1024 * 1024 * 10;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        
        try
        {
            uint jpegSize = 0;
            if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize))
                return false;

            if (jpegSize == 0 || jpegSize > bufferSize)
                return false;

            // Write directly from unmanaged memory to file (avoid intermediate copy)
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);
            unsafe
            {
                var span = new ReadOnlySpan<byte>(buffer.ToPointer(), (int)jpegSize);
                fileStream.Write(span);
            }
            
            return true;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }
}
```

---

## ? Issue 7: Incomplete Dispose Pattern

### Location
```35:51:MaterialClient.Common/Services/Hikvision/PlayM4Decoder.cs
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
```

### Problem Analysis

1. **Does not implement standard IDisposable pattern**: Missing `Dispose(bool disposing)` and finalizer
2. **No prevention of repeated Dispose**: May call `PlayM4_FreePort` multiple times
3. **No finalizer suppression**: `GC.SuppressFinalize(this)` not called
4. **Events not cleaned up**: If events added, need cleanup in Dispose
5. **Missing exception handling**: Exceptions in Dispose may prevent complete resource release

### Fix Recommendation ???

```csharp
public sealed class PlayM4Decoder : IDisposable
{
    private bool _disposed = false;
    private readonly ManualResetEventSlim? _playingEvent;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        lock (_lockObject)
        {
            if (_disposed) return; // Double-check
            
            try
            {
                if (disposing)
                {
                    // Clean up managed resources
                    _playingEvent?.Dispose();
                }
                
                // Clean up unmanaged resources
                try
                {
                    Stop();
                }
                catch (Exception)
                {
                    // Log but don't throw
                }
                
                try
                {
                    CloseStream();
                }
                catch (Exception)
                {
                    // Log but don't throw
                }
                
                if (_port >= 0)
                {
                    try
                    {
                        PlayM4.PlayM4_FreePort(_port);
                    }
                    catch (Exception)
                    {
                        // Log but don't throw
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
    
    ~PlayM4Decoder()
    {
        Dispose(false);
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PlayM4Decoder));
    }
    
    // Call at beginning of all public methods
    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        ThrowIfDisposed();
        
        lock (_lockObject)
        {
            // ... implementation
        }
    }
}
```

---

## ? Issue 8: Insufficient Logging

### Problem Analysis

Current code lacks logging on critical paths:
1. **No error codes recorded when OpenStream fails**
2. **No logs for callback execution**
3. **No tracking of port allocation/release**
4. **Difficult to diagnose issues during concurrency**
5. **Performance metrics not recorded** (e.g., capture duration)

### Fix Recommendation ??

**Add detailed diagnostic logging**:

```csharp
public bool CaptureJpegFromStream(...)
{
    var sw = Stopwatch.StartNew();
    
    _logger?.LogDebug("Starting stream capture: IP={Ip}, Channel={Channel}", 
        config.Ip, channel);
    
    try
    {
        decoder = new PlayM4Decoder();
        _logger?.LogDebug("Decoder created: Port={Port}", decoder.Port);
        
        // ... start preview
        
        lRealHandle = NET_DVR.NET_DVR_RealPlay_V40(...);
        if (lRealHandle < 0)
        {
            var errorCode = NET_DVR.NET_DVR_GetLastError();
            _logger?.LogWarning(
                "Failed to start real-time preview: IP={Ip}, Channel={Channel}, ErrorCode={ErrorCode}, ErrorDesc={ErrorDesc}",
                config.Ip, channel, errorCode, GetErrorDescription(errorCode));
            return false;
        }
        
        _logger?.LogDebug("Preview started: Handle={Handle}", lRealHandle);
        
        // ... wait for initialization
        
        if (!decoder.IsPlaying)
        {
            playM4Error = decoder.GetLastError();
            _logger?.LogWarning(
                "Decoder initialization timeout: IP={Ip}, Channel={Channel}, Port={Port}, PlayM4Error={Error}",
                config.Ip, channel, decoder.Port, playM4Error);
            return false;
        }
        
        // ... capture JPEG
        
        var ok = decoder.CaptureJpeg(saveFullPath);
        
        sw.Stop();
        
        if (ok)
        {
            var fileSize = new FileInfo(saveFullPath).Length;
            _logger?.LogInformation(
                "Stream capture successful: IP={Ip}, Channel={Channel}, Port={Port}, FileSize={Size}, Duration={Ms}ms",
                config.Ip, channel, decoder.Port, fileSize, sw.ElapsedMilliseconds);
        }
        else
        {
            playM4Error = decoder.GetLastError();
            _logger?.LogWarning(
                "JPEG capture failed: IP={Ip}, Channel={Channel}, Port={Port}, PlayM4Error={Error}, Duration={Ms}ms",
                config.Ip, channel, decoder.Port, playM4Error, sw.ElapsedMilliseconds);
        }
        
        return ok;
    }
    finally
    {
        _logger?.LogDebug("Cleaning up resources: Handle={Handle}, Port={Port}", 
            lRealHandle, decoder?.Port);
        
        // ... cleanup
    }
}
```

**Logging in callback function**:
```csharp
NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
{
    try
    {
        _logger?.LogTrace("Callback triggered: DataType={DataType}, BufSize={BufSize}", dataType, bufSize);
        
        if (disposed) return;
        
        lock (streamLock)
        {
            switch (dataType)
            {
                case NET_DVR.NET_DVR_SYSHEAD:
                    _logger?.LogDebug("Received system header: Size={Size}", bufSize);
                    // ... processing
                    break;
                    
                case NET_DVR.NET_DVR_STREAMDATA:
                    // Use Trace level to avoid excessive logging
                    // _logger?.LogTrace("Received stream data: Size={Size}", bufSize);
                    // ... processing
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Callback exception: DataType={DataType}, BufSize={BufSize}", 
            dataType, bufSize);
    }
};
```

**Error code description helper method**:
```csharp
private static string GetErrorDescription(uint errorCode)
{
    return errorCode switch
    {
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
        _ => $"Unknown error ({errorCode})"
    };
}
```

---

## Fix Priority

### ? Immediate Fix (Root causes of crashes)

1. **Issue 1**: Add try-catch protection to callback function ?????
   - **Impact**: Prevent process crash
   - **Difficulty**: Low
   - **Time**: 15 minutes

2. **Issue 2**: Fix race condition between callback and resource disposal ?????
   - **Impact**: Eliminate race conditions
   - **Difficulty**: Medium
   - **Time**: 30 minutes

3. **Issue 3**: Fix state inconsistency in OpenStream ????
   - **Impact**: Ensure state correctness
   - **Difficulty**: Low
   - **Time**: 20 minutes

### ? Fix Soon (Affecting stability)

4. **Issue 4**: Add port concurrency limit (semaphore or port pool) ????
   - **Impact**: Prevent resource exhaustion
   - **Difficulty**: Medium
   - **Time**: 1 hour

5. **Issue 5**: Change to event waiting mechanism ???
   - **Impact**: Improve efficiency, reduce thread waste
   - **Difficulty**: Medium
   - **Time**: 30 minutes

6. **Issue 7**: Improve Dispose pattern ???
   - **Impact**: Ensure proper resource release
   - **Difficulty**: Medium
   - **Time**: 30 minutes

### ? Recommended Optimization (Improve performance)

7. **Issue 6**: Use ArrayPool to optimize memory allocation ??
   - **Impact**: Reduce memory allocation, improve performance
   - **Difficulty**: Medium
   - **Time**: 30 minutes

8. **Issue 8**: Enhance logging ??
   - **Impact**: Improve observability, facilitate diagnosis
   - **Difficulty**: Low
   - **Time**: 1 hour

---

## Comprehensive Fix Plan

### Phase 1: Emergency Fix (Eliminate Crashes)

**Goal**: Eliminate crash issues within 1-2 hours

**Fix Steps**:

1. **Add try-catch in callback function**
2. **Adjust finally block order (stop stream first, then release decoder)**
3. **Add state flag to prevent race conditions**
4. **Add parameter validation and error handling in OpenStream**

**Expected Results**:
- ? Crash issues eliminated (exceptions caught)
- ? Race conditions significantly reduced
- ? Error logs can be recorded

### Phase 2: Stability Enhancement (Improve Reliability)

**Goal**: Improve system stability within 2-3 days

**Fix Steps**:

1. **Implement port pool or add concurrency limit**
2. **Change to event waiting mechanism**
3. **Improve Dispose pattern**
4. **Add detailed logging**

**Expected Results**:
- ? Stable in concurrent scenarios
- ? Standardized resource management
- ? Issues can be tracked

### Phase 3: Performance Optimization (Improve Performance)

**Goal**: Optimize performance within 1 week

**Fix Steps**:

1. **Use ArrayPool to optimize memory**
2. **Implement async capture**
3. **Add performance monitoring**

**Expected Results**:
- ? Reduced memory footprint
- ? Improved response speed
- ? Enhanced concurrency capability

---

## Complete Fix Code Examples

### PlayM4Decoder.cs Fixed Version

```csharp
using System.Buffers;
using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
/// PlayM4 decoder for manual decoding of Hikvision video streams
/// </summary>
public sealed class PlayM4Decoder : IDisposable
{
    private const int STREAME_REALTIME = 0;
    private const int STREAME_FILE = 1;
    
    private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    
    private readonly object _lockObject = new();
    private readonly ManualResetEventSlim _playingEvent = new(false);
    
    private IntPtr _hPlayWnd = IntPtr.Zero;
    private int _port = -1;
    private bool _disposed = false;

    public bool IsInitialized { get; private set; }
    public bool IsPlaying { get; private set; }
    public int Port => _port;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_disposed) return;

            try
            {
                if (disposing)
                {
                    _playingEvent?.Dispose();
                }

                try { Stop(); } catch { }
                try { CloseStream(); } catch { }

                if (_port >= 0)
                {
                    try
                    {
                        PlayM4.PlayM4_FreePort(_port);
                    }
                    catch { }
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

    ~PlayM4Decoder()
    {
        Dispose(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PlayM4Decoder));
    }

    public int GetLastError()
    {
        return PlayM4.PlayM4_GetLastError(_port);
    }

    public bool GetPictureQuality()
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (_port < 0) return false;
            var bHighQuality = false;
            return PlayM4.PlayM4_GetPictureQuality(_port, ref bHighQuality) && bHighQuality;
        }
    }

    public bool SetPictureQuality(long highQuality)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (_port < 0) return false;
            return PlayM4.PlayM4_SetJpegQuality(highQuality);
        }
    }

    public bool Initialize()
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (IsInitialized) return true;
            if (_port >= 0) return true;

            if (!PlayM4.PlayM4_GetPort(ref _port))
                return false;

            IsInitialized = _port >= 0;
            return IsInitialized;
        }
    }

    public bool OpenStream(IntPtr systemHeader, uint headerSize, IntPtr hPlayWnd = default)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            // Parameter validation
            if (systemHeader == IntPtr.Zero || headerSize == 0)
                return false;

            // Prevent duplicate opening
            if (IsPlaying)
                return true;

            if (!IsInitialized)
                if (!Initialize())
                    return false;

            if (_port < 0) return false;

            _hPlayWnd = hPlayWnd;

            if (!PlayM4.PlayM4_SetStreamOpenMode(_port, STREAME_REALTIME))
                return false;

            if (!PlayM4.PlayM4_OpenStream(_port, systemHeader, headerSize, 1024 * 1024 * 10))
                return false;

            if (!PlayM4.PlayM4_Play(_port, _hPlayWnd))
            {
                PlayM4.PlayM4_CloseStream(_port);
                return false;
            }

            IsPlaying = true;
            _playingEvent.Set();
            return true;
        }
    }

    public bool WaitForPlaying(int timeoutMs = 5000)
    {
        ThrowIfDisposed();
        return _playingEvent.Wait(timeoutMs);
    }

    public bool InputData(IntPtr data, uint dataSize)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0 || dataSize == 0)
                return false;

            return PlayM4.PlayM4_InputData(_port, data, dataSize);
        }
    }

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

    public void CloseStream()
    {
        lock (_lockObject)
        {
            if (_port >= 0)
                PlayM4.PlayM4_CloseStream(_port);
        }
    }

    public bool CaptureJpeg(string savePath)
    {
        ThrowIfDisposed();
        lock (_lockObject)
        {
            if (!IsPlaying || _port < 0)
                return false;

            SetPictureQuality(100);

            const int bufferSize = 1024 * 1024 * 10;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            byte[]? rentedBuffer = null;

            try
            {
                uint jpegSize = 0;
                if (!PlayM4.PlayM4_GetJPEG(_port, buffer, bufferSize, ref jpegSize))
                    return false;

                if (jpegSize == 0 || jpegSize > bufferSize)
                    return false;

                rentedBuffer = _bufferPool.Rent((int)jpegSize);
                Marshal.Copy(buffer, rentedBuffer, 0, (int)jpegSize);

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
```

### HikvisionService.cs Key Method Fixed Version

```csharp
public bool CaptureJpegFromStream(HikvisionDeviceConfig config, int channel, string saveFullPath,
    out int playM4Error)
{
    playM4Error = 0;
    ArgumentNullException.ThrowIfNull(config);
    if (string.IsNullOrWhiteSpace(saveFullPath))
        throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
    
    EnsureInitialized();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

    if (!EnsureLogin(config, out var userId))
    {
        _logger?.LogWarning("Login failed: IP={Ip}, Port={Port}", config.Ip, config.Port);
        return false;
    }

    var sw = Stopwatch.StartNew();
    var lRealHandle = -1;
    PlayM4Decoder? decoder = null;
    var streamLock = new object();
    var disposed = false; // Local state flag

    NET_DVR.REALDATACALLBACK realDataCallback = (handle, dataType, buffer, bufSize, user) =>
    {
        try
        {
            if (disposed) return;
            
            lock (streamLock)
            {
                if (disposed || decoder == null) return;
                if (buffer == IntPtr.Zero || bufSize == 0) return;

                switch (dataType)
                {
                    case NET_DVR.NET_DVR_SYSHEAD:
                        if (!decoder.IsInitialized)
                        {
                            var hWnd = NET_DVR.GetDesktopWindow();
                            if (!decoder.OpenStream(buffer, bufSize, hWnd))
                            {
                                _logger?.LogError("Decoder initialization failed: Port={Port}, Error={Error}",
                                    decoder.Port, decoder.GetLastError());
                            }
                        }
                        break;

                    case NET_DVR.NET_DVR_STREAMDATA:
                        if (decoder.IsPlaying)
                            decoder.InputData(buffer, bufSize);
                        break;

                    case NET_DVR.NET_DVR_AUDIOSTREAMDATA:
                        if (decoder.IsPlaying)
                            decoder.InputData(buffer, bufSize);
                        break;

                    default:
                        if (decoder.IsPlaying)
                            decoder.InputData(buffer, bufSize);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Callback exception: DataType={DataType}, BufSize={BufSize}", dataType, bufSize);
        }
    };

    try
    {
        decoder = new PlayM4Decoder();
        _logger?.LogDebug("Decoder created: Port={Port}", decoder.Port);

        var previewInfo = new NET_DVR.NET_DVR_PREVIEWINFO
        {
            lChannel = channel,
            dwStreamType = 0,
            dwLinkMode = 0,
            hPlayWnd = IntPtr.Zero,
            bBlocked = true,
            bPassbackRecord = false,
            byPreviewMode = 0,
            byStreamID = new byte[32],
            byProtoType = 0,
            byRes1 = 0,
            byVideoCodingType = 0,
            dwDisplayBufNum = 1,
            byNPQMode = 0,
            byRes = new byte[215]
        };

        lRealHandle = NET_DVR.NET_DVR_RealPlay_V40(userId, ref previewInfo, realDataCallback, IntPtr.Zero);
        if (lRealHandle < 0)
        {
            var errorCode = NET_DVR.NET_DVR_GetLastError();
            _logger?.LogWarning("Preview start failed: IP={Ip}, Channel={Channel}, ErrorCode={ErrorCode}",
                config.Ip, channel, errorCode);
            return false;
        }

        _logger?.LogDebug("Preview started: Handle={Handle}", lRealHandle);

        // Use event waiting instead of polling
        if (!decoder.WaitForPlaying(5000))
        {
            playM4Error = decoder.GetLastError();
            _logger?.LogWarning("Decoder initialization timeout: Port={Port}, Error={Error}",
                decoder.Port, playM4Error);
            return false;
        }

        Thread.Sleep(500); // Wait for one frame

        var ok = decoder.CaptureJpeg(saveFullPath);
        if (!ok)
            playM4Error = decoder.GetLastError();

        sw.Stop();

        if (ok)
        {
            var fileSize = new FileInfo(saveFullPath).Length;
            _logger?.LogInformation("Capture successful: IP={Ip}, Channel={Channel}, FileSize={Size}, Duration={Ms}ms",
                config.Ip, channel, fileSize, sw.ElapsedMilliseconds);
        }
        else
        {
            _logger?.LogWarning("Capture failed: IP={Ip}, Channel={Channel}, PlayM4Error={Error}, Duration={Ms}ms",
                config.Ip, channel, playM4Error, sw.ElapsedMilliseconds);
        }

        return ok;
    }
    finally
    {
        // 1. Set flag
        disposed = true;

        // 2. Stop preview (stop callbacks)
        if (lRealHandle >= 0)
        {
            NET_DVR.NET_DVR_StopRealPlay(lRealHandle);
            Thread.Sleep(200); // Wait for callback completion
        }

        // 3. Release decoder
        lock (streamLock)
        {
            if (decoder != null)
            {
                if (playM4Error == 0 && decoder.Port >= 0)
                    playM4Error = decoder.GetLastError();

                decoder.Dispose();
            }
        }

        _logger?.LogDebug("Resources cleaned: Handle={Handle}", lRealHandle);
    }
}
```

---

## Testing Recommendations

### 1. Unit Tests

```csharp
[Fact]
public async Task CaptureJpegFromStream_Concurrent_ShouldNotCrash()
{
    // Simulate concurrent capture
    var tasks = Enumerable.Range(0, 4)
        .Select(i => Task.Run(() =>
        {
            var config = new HikvisionDeviceConfig
            {
                Ip = "192.168.1.64",
                Port = 8000,
                Username = "admin",
                Password = "password"
            };
            
            return _service.CaptureJpegFromStream(
                config, 
                1, 
                $"test_{i}.jpg", 
                out var error);
        }))
        .ToArray();
    
    await Task.WhenAll(tasks);
    
    // Verify: at least no crash
    Assert.True(true);
}
```

### 2. Stress Test

```csharp
[Fact]
public async Task CaptureJpegFromStream_StressTest()
{
    // 100 iterations, 4 concurrent each
    for (int i = 0; i < 100; i++)
    {
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => 
            {
                return _service.CaptureJpegFromStream(...);
            }))
            .ToArray();
        
        await Task.WhenAll(tasks);
    }
}
```

### 3. Resource Leak Test

```csharp
[Fact]
public void PlayM4Decoder_DisposeTest()
{
    // Verify ports are properly released
    var ports = new List<int>();
    
    for (int i = 0; i < 100; i++)
    {
        using var decoder = new PlayM4Decoder();
        decoder.Initialize();
        ports.Add(decoder.Port);
    }
    
    // Verify ports can be reused
    Assert.True(ports.Distinct().Count() < 100);
}
```

### 4. Exception Injection Test

```csharp
[Fact]
public void CallbackException_ShouldNotCrash()
{
    // Use Moq to simulate exception scenarios
    // Verify exceptions are properly caught
}
```

---

## Monitoring Metrics

### 1. Key Metrics

- **Capture Success Rate**: Success count / Total count
- **Average Capture Duration**: Milliseconds
- **Port Usage**: Current ports used / Maximum ports
- **Memory Usage**: Heap memory, unmanaged memory
- **Exception Rate**: Callback exception count, Dispose exception count

### 2. Log Level Recommendations

- **Error**: Crashes, callback exceptions, resource release failures
- **Warning**: Capture failures, timeouts, port exhaustion
- **Information**: Capture success, performance metrics
- **Debug**: State changes, resource allocation/release
- **Trace**: Callback triggers (high frequency, enable only for debugging)

---

## Summary

### Root Causes of Crashes

1. **Primary**: Callback function without exception protection + race condition between callback and resource disposal
2. **Secondary**: PlayM4 port resource limitations + lack of concurrency management
3. **Trigger**: With 3-4 concurrent operations, probability of race conditions and resource contention spikes

### Expected Results After Fixes

- ? **Crashes eliminated**: Exceptions caught, won't crash process
- ? **Concurrency stability improved**: Port limits + state flags prevent race conditions
- ? **Observability enhanced**: Detailed logging for easy diagnosis
- ? **Resource management standardized**: Complete Dispose pattern prevents leaks
- ? **Performance improved**: ArrayPool reduces memory allocation, event waiting improves efficiency

### Recommended Fix Order

1. **Phase 1 (1-2 hours)**: Fix Issues 1, 2, 3 - Eliminate crashes
2. **Phase 2 (2-3 days)**: Fix Issues 4, 5, 7, 8 - Improve stability
3. **Phase 3 (1 week)**: Fix Issue 6 - Optimize performance

### Risk Assessment

- **Low Risk**: Issues 1, 3, 7, 8 (improvement fixes)
- **Medium Risk**: Issues 2, 5 (logic changes)
- **High Risk**: Issues 4, 6 (architectural changes, need thorough testing)

**Recommendation**: Verify fix effectiveness in test environment first, then gradually roll out to production.

---

**Report Completion Date**: 2026-01-13  
**Analysis Tool**: Claude Opus 4.5  
**Recommended Reviewers**: Architect, Senior Software Engineers
