using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services
{
    /// <summary>
    /// 客户端日志拉取服务 - 通过 SignalR 响应服务端的日志列表请求
    /// </summary>
    public class ClientLogPullService : ITransientDependency, IAsyncDisposable
    {
        private readonly ILogger<ClientLogPullService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDeviceStatusSignalRClient? _signalRClient;
        private readonly string _logBaseDirectory;

        public ClientLogPullService(
            ILogger<ClientLogPullService> logger,
            IConfiguration configuration,
            IDeviceStatusSignalRClient? signalRClient)
        {
            _logger = logger;
            _configuration = configuration;
            _signalRClient = signalRClient;

            var appDirectory = AppContext.BaseDirectory;
            var logDirectory = configuration.GetValue<string>("Log:Directory", "Logs");
            _logBaseDirectory = Path.IsPathRooted(logDirectory)
                ? logDirectory
                : Path.Combine(appDirectory, logDirectory);
        }

        /// <summary>
        /// 初始化服务 - 注册 SignalR 回调并连接
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_signalRClient == null)
            {
                _logger.LogWarning("SignalR client not available. Client log pull service cannot initialize.");
                return;
            }

            // Get client ID from configuration or use machine name
            var clientId = _configuration.GetValue<string>("Client:Id") ?? Environment.MachineName;
            if (clientId.Length > 100)
            {
                clientId = clientId.Substring(0, 100);
                _logger.LogWarning("Client ID truncated to 100 characters: {ClientId}", clientId);
            }

            // Register callback for log list requests
            _signalRClient.OnReceiveLogListRequest(async (requestId, targetClientId, dateFolder) =>
            {
                await HandleLogListRequestAsync(requestId, targetClientId, dateFolder, clientId);
            });

            // Register callback for file content requests
            _signalRClient.OnReceiveFileContentRequest(async (requestId, targetClientId, filePath, fileName) =>
            {
                await HandleFileContentRequestAsync(requestId, targetClientId, filePath, fileName, clientId);
            });

            _logger.LogInformation("ClientLogPullService initialized. ClientId: {ClientId}", clientId);

            // Wait for connection to be established before registering capability
            await RegisterCapabilityAsync(clientId);
        }

        /// <summary>
        /// 注册日志拉取能力到服务端
        /// </summary>
        private async Task RegisterCapabilityAsync(string clientId)
        {
            if (_signalRClient == null) return;

            var retries = 0;
            const int maxRetries = 3;

            while (retries < maxRetries)
            {
                try
                {
                    // Wait for connection to be established
                    await Task.Delay(1000 * (retries + 1));

                    if (_signalRClient.IsConnected)
                    {
                        var capabilityInfo = new LogCapabilityInfo
                        {
                            SupportsLogPull = true,
                            LogDirectory = _logBaseDirectory,
                            MaxConcurrentDownloads = 3,
                            ApiPort = _configuration.GetValue<int>("LocalLogApi:Port", 5900),
                            LogFormatVersion = "1.0"
                        };

                        await _signalRClient.RegisterLogCapability(clientId, capabilityInfo);
                        _logger.LogInformation("Log capability registered successfully: ClientId={ClientId}", clientId);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to register log capability after {Retries} retries", maxRetries);
                        return;
                    }
                    _logger.LogWarning(ex, "Failed to register log capability (attempt {Attempt}/{MaxAttempts})", retries, maxRetries);
                    await Task.Delay(5000);
                }
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

                // Build target directory path
                var targetDirectory = string.IsNullOrEmpty(dateFolder)
                    ? _logBaseDirectory
                    : Path.Combine(_logBaseDirectory, dateFolder);

                // Verify the directory is within the log base directory
                if (!targetDirectory.StartsWith(_logBaseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Log list request rejected: directory outside log directory: {TargetDirectory}",
                        targetDirectory);
                    return;
                }

                // Scan directory for log files
                var logFiles = new List<LogFileDto>();

                if (Directory.Exists(targetDirectory))
                {
                    var files = Directory.GetFiles(targetDirectory, "*.log");
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var relativePath = Path.GetRelativePath(_logBaseDirectory, Path.GetDirectoryName(file) ?? _logBaseDirectory);

                        logFiles.Add(new LogFileDto
                        {
                            FileName = fileInfo.Name,
                            FilePath = relativePath.Replace("\\", "/") + "/",
                            FileSize = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTimeUtc
                        });
                    }
                }

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
                var fullPath = string.IsNullOrEmpty(filePath)
                    ? Path.Combine(_logBaseDirectory, fileName)
                    : Path.Combine(_logBaseDirectory, filePath, fileName);

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

                using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("ClientLogPullService disposing");
            await Task.CompletedTask;
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
