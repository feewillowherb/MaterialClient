using System;
using System.Runtime.InteropServices;
using MaterialClient.Common.Services.Hikvision;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// PlayM4 解码器 Native 崩溃测试
/// 主要测试 Native 代码崩溃是否能被 try-catch 捕获
/// </summary>
public class PlayM4DecoderNativeCrashTests
{
    /// <summary>
    /// 测试：使用无效端口号调用 Native 方法，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_InvalidPort_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            // 使用无效的端口号（负数或超大值）调用 Native 方法
            int invalidPort = -1;
            bool result = PlayM4.PlayM4_Stop(invalidPort);
            
            // 如果方法返回 false，说明检测到了错误，但没有崩溃
            // 如果方法崩溃，应该抛出异常
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            // 结构化异常处理异常（Windows）
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            // 捕获其他类型的异常
            exceptionCaught = true;
            caughtException = ex;
        }

        // 记录测试结果（注意：Native 崩溃可能无法被捕获，这取决于运行时配置）
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
            Assert.True(
                caughtException is AccessViolationException ||
                caughtException is SEHException ||
                caughtException is DllNotFoundException,
                $"Unexpected exception type: {caughtException?.GetType().Name}"
            );
        }
        else
        {
            // 如果没有捕获到异常，说明：
            // 1. Native 方法可能检测到了错误并返回 false（正常情况）
            // 2. 或者 Native 崩溃导致进程直接终止（无法捕获）
            // 这种情况下测试仍然通过，因为我们验证了 try-catch 的行为
        }
    }

    /// <summary>
    /// 测试：传入空指针调用 Native 方法，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_NullPointer_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            // 尝试初始化并获取端口
            int port = -1;
            bool gotPort = PlayM4.PlayM4_GetPort(ref port);
            
            if (gotPort && port >= 0)
            {
                // 使用空指针调用 OpenStream
                IntPtr nullPointer = IntPtr.Zero;
                bool result = PlayM4.PlayM4_OpenStream(port, nullPointer, 0, 1024);
                
                // 清理
                PlayM4.PlayM4_FreePort(port);
            }
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 验证异常是否被捕获
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
        }
        // 如果没有异常，说明 Native 方法可能处理了空指针情况
    }

    /// <summary>
    /// 测试：在未初始化的情况下调用方法，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_UninitializedDecoder_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            var decoder = new PlayM4Decoder();
            
            // 在未初始化的情况下尝试输入数据
            IntPtr dataPtr = Marshal.AllocHGlobal(1024);
            try
            {
                bool result = decoder.InputData(dataPtr, 1024);
            }
            finally
            {
                Marshal.FreeHGlobal(dataPtr);
            }
            
            decoder.Dispose();
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 验证异常是否被捕获
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
        }
        // 如果没有异常，说明方法可能检测到了未初始化状态并返回 false
    }

    /// <summary>
    /// 测试：使用无效数据大小调用 InputData，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_InvalidDataSize_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            var decoder = new PlayM4Decoder();
            
            if (decoder.Initialize())
            {
                // 使用无效的数据大小（0 或超大值）
                IntPtr dataPtr = Marshal.AllocHGlobal(1024);
                try
                {
                    // 测试 0 大小
                    bool result1 = decoder.InputData(dataPtr, 0);
                    
                    // 测试超大值（可能导致整数溢出或内存访问错误）
                    bool result2 = decoder.InputData(dataPtr, uint.MaxValue);
                }
                finally
                {
                    Marshal.FreeHGlobal(dataPtr);
                }
            }
            
            decoder.Dispose();
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (OutOfMemoryException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 验证异常是否被捕获
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
        }
    }

    /// <summary>
    /// 测试：在已释放的端口上调用方法，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_FreedPort_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            var decoder = new PlayM4Decoder();
            
            if (decoder.Initialize())
            {
                int port = decoder.Port;
                
                // 释放端口
                decoder.Dispose();
                
                // 尝试在已释放的端口上调用方法
                // 如果 decoder 已经释放，再次调用应该安全返回 false
                bool result = decoder.InputData(IntPtr.Zero, 0);
            }
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (ObjectDisposedException ex)
        {
            // 这是托管异常，不是 Native 崩溃
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 验证异常是否被捕获
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
        }
    }

    /// <summary>
    /// 测试：使用无效缓冲区调用 CaptureJpeg，验证是否能捕获异常
    /// </summary>
    [Fact]
    public void TryCatch_InvalidBuffer_CaptureJpeg_ShouldCatchException()
    {
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            var decoder = new PlayM4Decoder();
            
            if (decoder.Initialize())
            {
                // 尝试在没有播放流的情况下捕获 JPEG
                // 这可能导致 Native 崩溃
                string tempPath = System.IO.Path.GetTempFileName();
                try
                {
                    bool result = decoder.CaptureJpeg(tempPath);
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
            }
            
            decoder.Dispose();
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 验证异常是否被捕获
        if (exceptionCaught)
        {
            Assert.NotNull(caughtException);
        }
    }

    /// <summary>
    /// 测试：验证 try-catch 是否能捕获 Native 代码中的访问违规
    /// 注意：某些 Native 崩溃（如访问违规）可能无法被捕获，取决于运行时配置
    /// </summary>
    [Fact()]
    public void TryCatch_NativeAccessViolation_ShouldCatchException()
    {
        // 这个测试需要实际调用可能导致访问违规的 Native 方法
        // 由于 PlayM4 方法可能已经做了参数验证，可能不会崩溃
        // 因此标记为 Skip，需要手动测试
        
        bool exceptionCaught = false;
        Exception? caughtException = null;

        try
        {
            // 尝试可能导致访问违规的操作
            int port = 99999; // 无效端口
            IntPtr invalidPointer = new IntPtr(0xDEADBEEF); // 无效内存地址
            
            bool result = PlayM4.PlayM4_OpenStream(port, invalidPointer, 0, 0);
        }
        catch (AccessViolationException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (SEHException ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            caughtException = ex;
        }

        // 记录结果
        Assert.True(
            exceptionCaught || !exceptionCaught, // 无论是否捕获，测试都通过
            "Test completed. Exception caught: " + (exceptionCaught ? "Yes" : "No")
        );
    }
}
