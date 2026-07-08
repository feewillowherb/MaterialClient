using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Logging;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.Services
{
    /// <summary>
    /// 客户端日志拉取服务 - 通过 SignalR 响应服务端的日志列表请求
    /// </summary>
    public class ClientLogPullService : ISingletonDependency, IAsyncDisposable
    {
        private readonly ILogger<ClientLogPullService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDeviceStatusSignalRClient? _signalRClient;
        private readonly ILocalEventBus _localEventBus;
        private readonly string _logBaseDirectory;
        private readonly SemaphoreSlim _registerCapabilityGate = new(1, 1);

        private IDisposable? _connectionRestoredSubscription;
        private string? _clientId;
        private volatile bool _capabilityRegistered;

        public ClientLogPullService(
            ILogger<ClientLogPullService> logger,
            IConfiguration configuration,
            IDeviceStatusSignalRClient? signalRClient,
            ILocalEventBus localEventBus)
        {
            _logger = logger;
            _configuration = configuration;
            _signalRClient = signalRClient;
            _localEventBus = localEventBus;

            var appDirectory = AppContext.BaseDirectory;
            var logDirectory = configuration.GetValue<string>("Log:Directory", "Logs");
            _logBaseDirectory = Path.IsPathRooted(logDirectory)
                ? logDirectory
                : Path.Combine(appDirectory, logDirectory);
        }

        /// <summary>
        /// 初始化服务 - 注册 SignalR 回调（不阻塞启动；能力注册在后台重试）
        /// </summary>
        public Task InitializeAsync()
        {
            if (_signalRClient == null)
            {
                _logger.LogWarning("SignalR client not available. Client log pull service cannot initialize.");
                return Task.CompletedTask;
            }

            var clientId = _configuration.GetValue<string>("Client:Id") ?? Environment.MachineName;
            if (clientId.Length > 100)
            {
                clientId = clientId.Substring(0, 100);
                _logger.LogWarning("Client ID truncated to 100 characters: {ClientId}", clientId);
            }

            _clientId = clientId;
            RegisterSignalRCallbacks(clientId);

            _connectionRestoredSubscription?.Dispose();
            _connectionRestoredSubscription = _localEventBus.Subscribe<SignalRConnectionRestoredEventData>(_ =>
            {
                OnSignalRConnectionRestored();
                return Task.CompletedTask;
            });

            _logger.LogInformation("ClientLogPullService initialized. ClientId: {ClientId}", clientId);

            TryRegisterCapabilityInBackground();
            return Task.CompletedTask;
        }

        private void OnSignalRConnectionRestored()
        {
            if (string.IsNullOrEmpty(_clientId))
            {
                return;
            }

            _capabilityRegistered = false;
            RegisterSignalRCallbacks(_clientId);
            TryRegisterCapabilityInBackground();
        }

        private void RegisterSignalRCallbacks(string clientId)
        {
            if (_signalRClient == null)
            {
                return;
            }

            _signalRClient.OnReceiveLogListRequest(async (requestId, targetClientId, dateFolder) =>
            {
                await HandleLogListRequestAsync(requestId, targetClientId, dateFolder, clientId);
            });

            _signalRClient.OnReceiveFileContentRequest(async (requestId, targetClientId, filePath, fileName) =>
            {
                await HandleFileContentRequestAsync(requestId, targetClientId, filePath, fileName, clientId);
            });
        }

        private void TryRegisterCapabilityInBackground()
        {
            if (string.IsNullOrEmpty(_clientId))
            {
                return;
            }

            _ = RegisterCapabilityAsync(_clientId);
        }

        /// <summary>
        /// 注册日志拉取能力到服务端（后台重试，失败不阻塞应用启动）
        /// </summary>
        private async Task RegisterCapabilityAsync(string clientId)
        {
            if (_signalRClient == null || _capabilityRegistered)
            {
                return;
            }

            if (!await _registerCapabilityGate.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (_capabilityRegistered)
                {
                    return;
                }

                const int maxAttempts = 5;

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)));

                        if (!_signalRClient.IsConnected)
                        {
                            _logger.LogDebug(
                                "SignalR not connected; deferring log capability registration (attempt {Attempt}/{MaxAttempts})",
                                attempt, maxAttempts);
                            continue;
                        }

                        var capabilityInfo = new LogCapabilityInfo
                        {
                            SupportsLogPull = true,
                            LogDirectory = _logBaseDirectory,
                            MaxConcurrentDownloads = 3,
                            ApiPort = _configuration.GetValue<int>("LocalLogApi:Port", 5900),
                            LogFormatVersion = "1.0"
                        };

                        await _signalRClient.RegisterLogCapability(clientId, capabilityInfo);
                        _capabilityRegistered = true;
                        _logger.LogInformation("Log capability registered successfully: ClientId={ClientId}", clientId);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to register log capability (attempt {Attempt}/{MaxAttempts})",
                            attempt, maxAttempts);
                    }
                }

                _logger.LogWarning(
                    "Log capability registration skipped after {MaxAttempts} attempts; app continues without remote log pull",
                    maxAttempts);
            }
            finally
            {
                _registerCapabilityGate.Release();
            }
        }

        /// <summary>
        /// 处理日志列表请求
        /// </summary>
        private async Task HandleLogListRequestAsync(string requestId, string targetClientId, string dateFolder, string currentClientId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Verify the request is for this client
                if (targetClientId != currentClientId)
                {
                    _logger.LogDebug("Ignoring log list request for different client: {TargetClientId}", targetClientId);
                    return;
                }

                _logger.LogDebug("Processing log list request: RequestId={RequestId}, DateFolder={DateFolder}",
                    requestId, dateFolder);

                // Security check: prevent directory traversal
                if (dateFolder.Contains("..") || Path.IsPathRooted(dateFolder))
                {
                    _logger.LogWarning("Log list request rejected due to path traversal attempt: DateFolder={DateFolder}",
                        dateFolder);
                    return;
                }

                // Scan directory for log files (standardized and legacy layouts)
                var logFiles = ClientLogScanner.Scan(_logBaseDirectory, dateFolder)
                    .Select(entry => new LogFileDto
                    {
                        FileName = entry.FileName,
                        FilePath = entry.FilePath,
                        FileSize = entry.FileSize,
                        LastModified = entry.LastModifiedUtc
                    })
                    .ToList();

                // Build result
                var result = new ClientLogListResultDto
                {
                    RequestId = requestId,
                    ClientId = currentClientId,
                    DateFolder = dateFolder ?? string.Empty,
                    Files = logFiles.ToArray(),
                    TotalSize = logFiles.Sum(f => f.FileSize),
                    ScannedAt = DateTime.UtcNow
                };

                // Return result to server
                if (_signalRClient != null)
                {
                    await _signalRClient.ReturnLogList(result);
                    _logger.LogInformation("Log list returned: RequestId={RequestId}, FileCount={Count}, TotalSize={Size} bytes, Duration={Duration}ms",
                        requestId, logFiles.Count, result.TotalSize, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling log list request: RequestId={RequestId}, DateFolder={DateFolder}",
                    requestId, dateFolder);
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning("Log list request processing took {Duration}ms: RequestId={RequestId}",
                        stopwatch.ElapsedMilliseconds, requestId);
                }
            }
        }

        /// <summary>
        /// 处理文件内容请求 - 通过 SignalR 返回文件分片
        /// </summary>
        private async Task HandleFileContentRequestAsync(string requestId, string targetClientId, string filePath, string fileName, string currentClientId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Verify the request is for this client
                if (targetClientId != currentClientId)
                {
                    _logger.LogDebug("Ignoring file content request for different client: {TargetClientId}", targetClientId);
                    return;
                }

                _logger.LogDebug("Processing file content request: RequestId={RequestId}, FilePath={FilePath}, FileName={FileName}",
                    requestId, filePath, fileName);

                // Security check: prevent directory traversal
                if (filePath.Contains("..") || fileName.Contains("..") ||
                    Path.IsPathRooted(filePath) || Path.IsPathRooted(fileName))
                {
                    _logger.LogWarning("File content request rejected due to path traversal attempt: FilePath={FilePath}, FileName={FileName}",
                        filePath, fileName);
                    await _signalRClient?.ReturnFileErrorAsync(requestId, "路径包含非法字符");
                    return;
                }

                // Build full file path
                var normalizedFilePath = filePath.Replace('\\', '/').Trim('/');
                var fullPath = string.IsNullOrEmpty(normalizedFilePath)
                    ? Path.Combine(_logBaseDirectory, fileName)
                    : Path.Combine(_logBaseDirectory, normalizedFilePath, fileName);

                // Verify the file is within the log base directory
                if (!fullPath.StartsWith(_logBaseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("File content request rejected: file outside log directory: {FullPath}", fullPath);
                    await _signalRClient?.ReturnFileErrorAsync(requestId, "文件路径非法");
                    return;
                }

                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("File content request failed: file not found: {FullPath}", fullPath);
                    await _signalRClient?.ReturnFileErrorAsync(requestId, "文件不存在");
                    return;
                }

                // Read file and send in chunks
                const int chunkSize = 64 * 1024; // 64KB chunks
                var fileInfo = new FileInfo(fullPath);
                var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);

                using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var buffer = new byte[chunkSize];
                int chunkIndex = 0;
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize)) > 0)
                {
                    var chunkData = new byte[bytesRead];
                    Array.Copy(buffer, chunkData, bytesRead);

                    await _signalRClient?.ReturnFileChunkAsync(requestId, chunkIndex, totalChunks, chunkData, fileInfo.Length);
                    chunkIndex++;

                    // Small delay to avoid overwhelming SignalR
                    if (chunkIndex % 10 == 0)
                    {
                        await Task.Delay(10);
                    }
                }

                _logger.LogInformation("File content sent: RequestId={RequestId}, FileName={FileName}, Size={Size} bytes, Chunks={Chunks}, Duration={Duration}ms",
                    requestId, fileName, fileInfo.Length, chunkIndex, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file content request: RequestId={RequestId}, FilePath={FilePath}, FileName={FileName}",
                    requestId, filePath, fileName);
                await _signalRClient?.ReturnFileErrorAsync(requestId, ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 5000)
                {
                    _logger.LogWarning("File content request processing took {Duration}ms: RequestId={RequestId}",
                        stopwatch.ElapsedMilliseconds, requestId);
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _connectionRestoredSubscription?.Dispose();
            _connectionRestoredSubscription = null;
            _registerCapabilityGate.Dispose();
            _logger.LogInformation("ClientLogPullService disposing");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 客户端日志列表结果 DTO
    /// </summary>
    public class ClientLogListResultDto
    {
        public string RequestId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string DateFolder { get; set; } = string.Empty;
        public LogFileDto[] Files { get; set; } = Array.Empty<LogFileDto>();
        public long TotalSize { get; set; }
        public DateTime ScannedAt { get; set; }
    }
}
