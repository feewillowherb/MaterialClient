using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Refit;
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
                            Name = "tts_audio",
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
}