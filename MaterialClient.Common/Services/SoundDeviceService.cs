using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Refit;
using System.Net.Http.Json;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Sound device service interface
/// </summary>
public interface ISoundDeviceService
{
    /// <summary>
    ///     Play text as speech on sound device
    /// </summary>
    /// <param name="text">Text to convert to speech and play</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PlayTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Play text as speech on sound device (V2 - using HttpClient directly without IHttpClientFactory and Refit)
    /// </summary>
    /// <param name="text">Text to convert to speech and play</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PlayTextV2Async(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if sound column device is online
    ///     Retrieves device serial number from ISettingsService, no parameters needed
    /// </summary>
    /// <returns>
    ///     Returns true if device is online (status code 1 or 2), otherwise false
    ///     Returns false if device is disabled or configuration is invalid
    /// </returns>
    Task<bool> IsOnlineAsync();
}

/// <summary>
///     Sound device service implementation
/// </summary>
[AutoConstructor]
public partial class SoundDeviceService : ISoundDeviceService, ISingletonDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SoundDeviceService>? _logger;
    private readonly ISettingsService _settingsService;

    /// <inheritdoc />
    public async Task PlayTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger?.LogWarning("Text is null or empty, skipping playback");
            return;
        }

        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var soundDeviceSettings = settings.SoundDeviceSettings;

            if (!soundDeviceSettings.Enabled)
            {
                _logger?.LogInformation("Sound device is disabled, skipping playback");
                return;
            }

            if (!soundDeviceSettings.IsValid())
            {
                _logger?.LogWarning(
                    "Sound device settings are incomplete: LocalIP={LocalIP}, SoundIP={SoundIP}, SoundSN={SoundSN}",
                    soundDeviceSettings.LocalIP, soundDeviceSettings.SoundIP, soundDeviceSettings.SoundSN);
                return;
            }

            // Parse volume (0 means 100)
            var volume = soundDeviceSettings.SoundVolume == "0" ? 100 : int.Parse(soundDeviceSettings.SoundVolume);

            // Build TTS URI
            var ttsUri =
                $"http://{soundDeviceSettings.LocalIP}:10008/tts_xf.single?text={Uri.EscapeDataString(text)}&voice_name=xiaoyan&speed=50&volume={volume}&origin=http://{soundDeviceSettings.LocalIP}:10008";
    
            // Create play API client
            var playBaseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
            var playHttpClient = _httpClientFactory.CreateClient();
            playHttpClient.BaseAddress = new Uri(playBaseUrl);
            playHttpClient.Timeout = TimeSpan.FromSeconds(30);
            var playApi = RestService.For<ISoundDeviceApi>(playHttpClient);

            // Create play request
            var playRequest = new SoundDevicePlayRequestDto
            {
                Name = "priority_task_play",
                SerialNumber = soundDeviceSettings.SoundSN,
                Type = "req",
                Params = new SoundDevicePlayParamsDto
                {
                    UserId = "0",
                    Volume = volume,
                    Urls =
                    [
                        new SoundDevicePlayUrlDto
                        {
                            Name = "speakText",
                            Udp = true,
                            Uri = ttsUri
                        }
                    ],
                    Level = 10000,
                    Name = $"task_{DateTime.Now.ToString("yyyyMMddHHmmss")}",
                    Count = 1,
                    Length = 0,
                    Type = 0,
                    TaskId = Guid.NewGuid().ToString()
                }
            };

            // Play audio with retry mechanism (8 attempts)
            const int maxRetries = 8;
            bool success = false;
            string? lastResponse = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger?.LogInformation("Playing audio on sound device (attempt {Attempt}/{MaxRetries}): {SoundIP}, TTS URI: {TtsUri}",
                        attempt, maxRetries, soundDeviceSettings.SoundIP, ttsUri);

                    var response = await playApi.PlayAudioAsync(playRequest, cancellationToken);
                    lastResponse = response;

                    // Parse response to check if successful
                    try
                    {
                        var responseDoc = JsonDocument.Parse(response);
                        if (responseDoc.RootElement.TryGetProperty("code", out var codeElement))
                        {
                            var code = codeElement.GetInt32();
                            if (code == 0)
                            {
                                _logger?.LogInformation("Audio playback started successfully (attempt {Attempt})", attempt);
                                success = true;
                                break;
                            }
                            
                            // code != 0, retry if not the last attempt
                            _logger?.LogWarning("Audio playback failed with code {Code} (attempt {Attempt}/{MaxRetries}). Response: {Response}",
                                code, attempt, maxRetries, response);
                        }
                        else
                        {
                            // No 'code' property, retry if not the last attempt
                            _logger?.LogWarning("Response does not contain 'code' property (attempt {Attempt}/{MaxRetries}). Response: {Response}",
                                attempt, maxRetries, response);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger?.LogError(jsonEx, "Failed to parse play response (attempt {Attempt}/{MaxRetries}): {Response}",
                            attempt, maxRetries, response);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger?.LogWarning("Audio playback request canceled (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error playing audio on sound device (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                }
            }

            if (!success)
            {
                _logger?.LogError("Audio playback ultimately failed after {MaxRetries} attempts. Text: {Text}, Last Response: {Response}",
                    maxRetries, text, lastResponse);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing text on sound device: {Text}", text);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PlayTextV2Async(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger?.LogWarning("Text is null or empty, skipping playback");
            return;
        }

        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var soundDeviceSettings = settings.SoundDeviceSettings;

            if (!soundDeviceSettings.Enabled)
            {
                _logger?.LogInformation("Sound device is disabled, skipping playback");
                return;
            }

            if (!soundDeviceSettings.IsValid())
            {
                _logger?.LogWarning(
                    "Sound device settings are incomplete: LocalIP={LocalIP}, SoundIP={SoundIP}, SoundSN={SoundSN}",
                    soundDeviceSettings.LocalIP, soundDeviceSettings.SoundIP, soundDeviceSettings.SoundSN);
                return;
            }

            // Parse volume (0 means 100)
            var volume = soundDeviceSettings.SoundVolume == "0" ? 100 : int.Parse(soundDeviceSettings.SoundVolume);

            // Build TTS URI (note: legacy code adds "。" at the end)
            var ttsUri =
                $"http://{soundDeviceSettings.LocalIP}:10008/tts_xf.single?text={Uri.EscapeDataString(text)}。&voice_name=xiaoyan&speed=50&volume={volume}&origin=http://{soundDeviceSettings.LocalIP}:10008";

            // Create play request
            var playRequest = new SoundDevicePlayRequestDto
            {
                Name = "priority_task_play",
                SerialNumber = soundDeviceSettings.SoundSN,
                Type = "req",
                Params = new SoundDevicePlayParamsDto
                {
                    UserId = "0",
                    Volume = volume,
                    Urls =
                    [
                        new SoundDevicePlayUrlDto
                        {
                            Name = "speakText",
                            Udp = true,
                            Uri = ttsUri
                        }
                    ],
                    Level = 10000,
                    Name = $"task_{DateTime.Now.ToString("yyyyMMddHHmmss")}",
                    Count = 1,
                    Length = 0,
                    Type = 0,
                    TaskId = Guid.NewGuid().ToString()
                }
            };

            // Play audio with retry mechanism (8 attempts)
            const int maxRetries = 8;
            bool success = false;
            string? lastResponse = null;

            // Create HttpClient directly (not using IHttpClientFactory)
            var playBaseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(playBaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.2; .NET CLR 1.1.4322; .NET CLR 2.0.50727; InfoPath.1) Web-Sniffer/1.0.24");
            
            try
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                try
                {
                    _logger?.LogInformation("Playing audio on sound device (attempt {Attempt}/{MaxRetries}): {SoundIP}, TTS URI: {TtsUri}",
                        attempt, maxRetries, soundDeviceSettings.SoundIP, ttsUri);

                    // Manually serialize JSON to ensure correct property names (matching RestSharp format)
                    // Use default options to respect JsonPropertyName attributes
                    var jsonOptions = new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                        WriteIndented = false
                    };
                    var jsonContent = JsonSerializer.Serialize(playRequest, jsonOptions);
                    
                    // Log the JSON content for debugging
                    _logger?.LogInformation("Sending JSON request (attempt {Attempt}): {JsonContent}", attempt, jsonContent);
                    
                    // Use StringContent with application/json content type
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("", content, cancellationToken);
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    lastResponse = responseContent;

                    // Check HTTP status
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogWarning("Audio playback HTTP request failed with status {StatusCode} (attempt {Attempt}/{MaxRetries}). Response: {Response}",
                            response.StatusCode, attempt, maxRetries, responseContent);
                        if (attempt < maxRetries)
                        {
                            continue;
                        }
                    }

                    // Parse response to check if successful
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        try
                        {
                            // Try to parse as success response
                            var successResult = JsonSerializer.Deserialize<SoundDevicePlaySuccessResponseDto>(responseContent);
                            if (successResult != null && successResult.Code == 0 && successResult.Type == "resp")
                            {
                                // Success: {"code":0,"type":"resp","msg":"","name":"priority_task_play"}
                                _logger?.LogInformation("Audio playback started successfully (attempt {Attempt}). Response: {Response}",
                                    attempt, responseContent);
                                success = true;
                                break;
                            }

                            // Try to parse as error response
                            var errorResult = JsonSerializer.Deserialize<SoundDevicePlayErrorResponseDto>(responseContent);
                            if (errorResult != null && errorResult.Result == -1)
                            {
                                // Failure: { "result": -1, "msg": "没有body信息" }
                                _logger?.LogWarning("Audio playback failed with result {Result} (attempt {Attempt}/{MaxRetries}). Response: {Response}",
                                    errorResult.Result, attempt, maxRetries, responseContent);
                            }
                            else
                            {
                                // Unknown response format
                                _logger?.LogWarning("Unknown response format (attempt {Attempt}/{MaxRetries}). Response: {Response}",
                                    attempt, maxRetries, responseContent);
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger?.LogWarning(jsonEx, "Failed to parse play response (attempt {Attempt}/{MaxRetries}): {Response}",
                                attempt, maxRetries, responseContent);
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Audio playback response is empty (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger?.LogWarning("Audio playback request canceled (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error playing audio on sound device (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                }
            }

                if (!success)
                {
                    _logger?.LogError("Audio playback ultimately failed after {MaxRetries} attempts. Text: {Text}, Last Response: {Response}",
                        maxRetries, text, lastResponse);
                }
            }
            finally
            {
                httpClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing text on sound device: {Text}", text);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            // 1. Get configuration
            var settings = await _settingsService.GetSettingsAsync();
            var soundDeviceSettings = settings.SoundDeviceSettings;

            // 2. Check if device is enabled
            if (!soundDeviceSettings.Enabled)
            {
                _logger?.LogDebug("Sound device is disabled, treating as offline");
                return false;
            }

            // 3. Check if configuration is valid
            if (!soundDeviceSettings.IsValid())
            {
                _logger?.LogWarning(
                    "Sound device settings are incomplete: LocalIP={LocalIP}, SoundIP={SoundIP}, SoundSN={SoundSN}",
                    soundDeviceSettings.LocalIP, soundDeviceSettings.SoundIP, soundDeviceSettings.SoundSN);
                return false;
            }

            // 4. Build device serial number (add prefix)
            var deviceSn = $"ls20://{soundDeviceSettings.SoundSN}";

            // 5. Create HTTP client and API instance
            var baseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(baseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var api = RestService.For<ISoundDeviceApi>(httpClient);

            // 6. Call remote API
            var statusResponse = await api.GetDeviceStatusAsync(
                type: "req",
                app: "ls20",
                sn: deviceSn);

            // 7. Parse status code
            var isOnline = statusResponse.Status == 1 || statusResponse.Status == 2;

            _logger?.LogDebug(
                "Sound device status check completed: DeviceSN={DeviceSN}, Status={Status}, IsOnline={IsOnline}",
                soundDeviceSettings.SoundSN, statusResponse.Status, isOnline);

            return isOnline;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error while checking sound device status");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogWarning(ex, "Timeout while checking sound device status");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while checking sound device status");
            return false;
        }
    }
}