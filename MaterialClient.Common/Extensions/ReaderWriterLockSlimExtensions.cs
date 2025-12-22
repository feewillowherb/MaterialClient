using System;
using System.Threading;

namespace MaterialClient.Common.Extensions;

/// <summary>
/// ReaderWriterLockSlim 扩展方法，提供更简洁的锁使用方式
/// </summary>
public static class ReaderWriterLockSlimExtensions
{
    extension(ReaderWriterLockSlim rwLock)
    {
        /// <summary>
        /// 获取读锁，返回 IDisposable，使用 using 语句自动释放
        /// </summary>
        public IDisposable ReadLock()
        {
            rwLock.EnterReadLock();
            return new ReadLockDisposable(rwLock);
        }

        /// <summary>
        /// 获取写锁，返回 IDisposable，使用 using 语句自动释放
        /// </summary>
        public IDisposable WriteLock()
        {
            rwLock.EnterWriteLock();
            return new WriteLockDisposable(rwLock);
        }
    }

    /// <summary>
    /// 读锁释放器
    /// </summary>
    private class ReadLockDisposable(ReaderWriterLockSlim rwLock) : IDisposable
    {
        private bool _disposed = false;

        public void Dispose()
        {
            if (!_disposed)
            {
                rwLock.ExitReadLock();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 写锁释放器
    /// </summary>
    private class WriteLockDisposable(ReaderWriterLockSlim rwLock) : IDisposable
    {
        private bool _disposed = false;

        public void Dispose()
        {
            if (!_disposed)
            {
                rwLock.ExitWriteLock();
                _disposed = true;
            }
        }
    }
}

