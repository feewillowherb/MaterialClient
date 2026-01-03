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

            if (string.IsNullOrWhiteSpace(soundDeviceSettings.LocalIP) ||
                string.IsNullOrWhiteSpace(soundDeviceSettings.SoundIP) ||
                string.IsNullOrWhiteSpace(soundDeviceSettings.SoundSN))
            {
                _logger?.LogWarning(
                    "Sound device settings are incomplete: LocalIP={LocalIP}, SoundIP={SoundIP}, SoundSN={SoundSN}",
                    soundDeviceSettings.LocalIP, soundDeviceSettings.SoundIP, soundDeviceSettings.SoundSN);
                return;
            }

            // Parse volume (0 means 100)
            var volume = soundDeviceSettings.SoundVolume == "0" ? 100 : int.Parse(soundDeviceSettings.SoundVolume);

            // Build TTS URI
            var ttsUri = $"http://{soundDeviceSettings.LocalIP}:10008/tts_xf.single?text={Uri.EscapeDataString(text)}&voice_name=xiaoyan&speed=50&volume={volume}&origin=http://{soundDeviceSettings.LocalIP}:10008";

            // Create play API client
            var playBaseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
            var playHttpClient = _httpClientFactory.CreateClient();
            playHttpClient.BaseAddress = new Uri(playBaseUrl);
            playHttpClient.Timeout = TimeSpan.FromSeconds(30);
            var playApi = RestService.For<ISoundDeviceApi>(playHttpClient);

            // Create play request
            var playRequest = new SoundDevicePlayRequestDto
            {
                Name = "tts_play",
                SerialNumber = soundDeviceSettings.SoundSN,
                Type = "play",
                Params = new SoundDevicePlayParamsDto
                {
                    UserId = "system",
                    Volume = volume,
                    Urls =
                    [
                        new SoundDevicePlayUrlDto
                        {
                            Name = "tts_audio",
                            Udp = false,
                            Uri = ttsUri
                        }
                    ],
                    Level = 1,
                    Name = "tts_play_task",
                    Count = 1,
                    Length = 0,
                    Type = 0,
                    TaskId = Guid.NewGuid().ToString()
                }
            };

            // Play audio
            _logger?.LogInformation("Playing audio on sound device: {SoundIP}, TTS URI: {TtsUri}", soundDeviceSettings.SoundIP, ttsUri);
            var response = await playApi.PlayAudioAsync(playRequest, cancellationToken);

            // Parse response to check if successful
            try
            {
                var responseDoc = JsonDocument.Parse(response);
                if (responseDoc.RootElement.TryGetProperty("code", out var codeElement) &&
                    codeElement.GetInt32() == 0)
                {
                    _logger?.LogInformation("Audio playback started successfully");
                }
                else
                {
                    _logger?.LogWarning("Audio playback may have failed. Response: {Response}", response);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse play response: {Response}", response);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing text on sound device: {Text}", text);
            throw;
        }
    }
}

