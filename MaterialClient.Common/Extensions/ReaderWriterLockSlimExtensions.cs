namespace MaterialClient.Common.Extensions;

/// <summary>
///     ReaderWriterLockSlim 扩展方法，提供更简洁且低分配的锁使用方式
///     使用 struct 实现 IDisposable 避免每次锁操作的堆分配
/// </summary>
public static class ReaderWriterLockSlimExtensions
{
    extension(ReaderWriterLockSlim rwLock)
    {
        /// <summary>
        /// 获取读锁，返回 IDisposable，使用 using 语句自动释放（value-type，避免堆分配）
        /// </summary>
        public ReadLockDisposable ReadLock()
        {
            rwLock.EnterReadLock();
            return new ReadLockDisposable(rwLock);
        }

        /// <summary>
        /// 获取写锁，返回 IDisposable，使用 using 语句自动释放（value-type，避免堆分配）
        /// </summary>
        public WriteLockDisposable WriteLock()
        {
            rwLock.EnterWriteLock();
            return new WriteLockDisposable(rwLock);
        }

        /// <summary>
        /// 获取可升级读锁，返回 IDisposable，使用 using 语句自动释放（value-type，避免堆分配）
        /// 适用于读取后可能需要升级为写锁的场景
        /// </summary>
        public UpgradeableReadLockDisposable UpgradeableReadLock()
        {
            rwLock.EnterUpgradeableReadLock();
            return new UpgradeableReadLockDisposable(rwLock);
        }
    }

    /// <summary>
    ///     读锁释放器（结构体，避免每次锁时产生 GC 分配）
    /// </summary>
    public readonly struct ReadLockDisposable : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;

        internal ReadLockDisposable(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
        }

        public void Dispose()
        {
            _rwLock?.ExitReadLock();
        }
    }

    /// <summary>
    ///     写锁释放器（结构体，避免每次锁时产生 GC 分配）
    /// </summary>
    public readonly struct WriteLockDisposable : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;

        internal WriteLockDisposable(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
        }

        public void Dispose()
        {
            _rwLock?.ExitWriteLock();
        }
    }

    /// <summary>
    ///     可升级读锁释放器（结构体，避免每次锁时产生 GC 分配）
    /// </summary>
    public readonly struct UpgradeableReadLockDisposable : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;

        internal UpgradeableReadLockDisposable(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
        }

        public void Dispose()
        {
            _rwLock?.ExitUpgradeableReadLock();
        }
    }
}