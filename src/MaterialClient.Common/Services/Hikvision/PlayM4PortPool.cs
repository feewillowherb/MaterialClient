using System.Collections.Concurrent;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Manages PlayM4 port allocation to prevent resource exhaustion.
///     Implements port pooling with semaphore-based concurrency limiting.
/// </summary>
/// <remarks>
///     PlayM4 SDK typically supports a limited number of concurrent ports (usually 16-64 depending on version).
///     This pool manager:
///     - Limits concurrent port usage via semaphore
///     - Provides async acquisition with timeout
///     - Properly releases ports back to the system
/// </remarks>
public static class PlayM4PortPool
{
    /// <summary>
    ///     Maximum number of concurrent ports (PlayM4 SDK limitation)
    /// </summary>
    private const int MaxConcurrentPorts = 16;

    /// <summary>
    ///     Semaphore to limit concurrent port usage
    /// </summary>
    private static readonly SemaphoreSlim _semaphore = new(MaxConcurrentPorts, MaxConcurrentPorts);

    /// <summary>
    ///     Pool of available ports that have been returned
    /// </summary>
    private static readonly ConcurrentBag<int> _availablePorts = new();

    /// <summary>
    ///     Lock object for thread-safe port acquisition
    /// </summary>
    private static readonly object _acquireLock = new();

    /// <summary>
    ///     Acquires a port from the pool asynchronously.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000ms)</param>
    /// <returns>The acquired port number</returns>
    /// <exception cref="TimeoutException">Thrown when port cannot be acquired within timeout</exception>
    /// <exception cref="InvalidOperationException">Thrown when PlayM4_GetPort fails</exception>
    public static async Task<int> AcquirePortAsync(int timeoutMs = 5000)
    {
        if (!await _semaphore.WaitAsync(timeoutMs))
        {
            throw new TimeoutException($"Cannot acquire PlayM4 port: concurrency limit ({MaxConcurrentPorts}) reached");
        }

        try
        {
            // Try to get a recycled port first
            if (_availablePorts.TryTake(out var port))
            {
                return port;
            }

            // No recycled port available, allocate a new one
            int newPort = -1;
            lock (_acquireLock)
            {
                if (!PlayM4.PlayM4_GetPort(ref newPort))
                {
                    var error = PlayM4.PlayM4_GetLastError(newPort);
                    throw new InvalidOperationException($"PlayM4_GetPort failed with error: {error}");
                }
            }

            return newPort;
        }
        catch
        {
            // Release semaphore on failure to maintain correct count
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    ///     Acquires a port from the pool synchronously.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000ms)</param>
    /// <returns>The acquired port number</returns>
    /// <exception cref="TimeoutException">Thrown when port cannot be acquired within timeout</exception>
    /// <exception cref="InvalidOperationException">Thrown when PlayM4_GetPort fails</exception>
    public static int AcquirePort(int timeoutMs = 5000)
    {
        if (!_semaphore.Wait(timeoutMs))
        {
            throw new TimeoutException($"Cannot acquire PlayM4 port: concurrency limit ({MaxConcurrentPorts}) reached");
        }

        try
        {
            // Try to get a recycled port first
            if (_availablePorts.TryTake(out var port))
            {
                return port;
            }

            // No recycled port available, allocate a new one
            int newPort = -1;
            lock (_acquireLock)
            {
                if (!PlayM4.PlayM4_GetPort(ref newPort))
                {
                    var error = PlayM4.PlayM4_GetLastError(newPort);
                    throw new InvalidOperationException($"PlayM4_GetPort failed with error: {error}");
                }
            }

            return newPort;
        }
        catch
        {
            // Release semaphore on failure to maintain correct count
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    ///     Tries to acquire a port from the pool synchronously without throwing.
    /// </summary>
    /// <param name="port">The acquired port number if successful</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000ms)</param>
    /// <returns>True if port was acquired successfully, false otherwise</returns>
    public static bool TryAcquirePort(out int port, int timeoutMs = 5000)
    {
        port = -1;

        if (!_semaphore.Wait(timeoutMs))
        {
            return false;
        }

        try
        {
            // Try to get a recycled port first
            if (_availablePorts.TryTake(out port))
            {
                return true;
            }

            // No recycled port available, allocate a new one
            lock (_acquireLock)
            {
                if (!PlayM4.PlayM4_GetPort(ref port))
                {
                    _semaphore.Release();
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // Release semaphore on failure
            _semaphore.Release();
            return false;
        }
    }

    /// <summary>
    ///     Releases a port back to the pool.
    /// </summary>
    /// <param name="port">The port number to release</param>
    /// <param name="cleanupResources">Whether to stop and close stream before releasing (default true)</param>
    public static void ReleasePort(int port, bool cleanupResources = true)
    {
        if (port < 0) return;

        try
        {
            if (cleanupResources)
            {
                // Cleanup any resources associated with the port
                try
                {
                    PlayM4.PlayM4_Stop(port);
                }
                catch
                {
                    // Ignore errors during cleanup
                }

                try
                {
                    PlayM4.PlayM4_CloseStream(port);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            // Free the port back to PlayM4 SDK
            try
            {
                PlayM4.PlayM4_FreePort(port);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
        finally
        {
            // Always release the semaphore
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Gets the current number of available slots in the pool.
    /// </summary>
    public static int AvailableSlots => _semaphore.CurrentCount;

    /// <summary>
    ///     Gets the maximum number of concurrent ports allowed.
    /// </summary>
    public static int MaxPorts => MaxConcurrentPorts;
}
