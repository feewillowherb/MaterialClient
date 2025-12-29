using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Refit;
using System.Text.Json;
using Volo.Abp.Domain.Services;

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
public partial class SoundDeviceService : DomainService, ISoundDeviceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SoundDeviceService>? _logger;
    private readonly ISettingsService _settingsService;

    /// <inheritdoc />
    public async Task PlayTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger?.LogWarning("PlayTextAsync: Text is null or empty, skipping playback");
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

            // Create TTS API client
            var ttsBaseUrl = $"http://{soundDeviceSettings.LocalIP}:10008";
            var ttsHttpClient = _httpClientFactory.CreateClient();
            ttsHttpClient.BaseAddress = new Uri(ttsBaseUrl);
            ttsHttpClient.Timeout = TimeSpan.FromSeconds(30);
            var ttsApi = RestService.For<ISoundDeviceApi>(ttsHttpClient);

            // Get TTS audio stream
            _logger?.LogInformation("Getting TTS audio for text: {Text}", text);
            await using var audioStream = await ttsApi.GetTtsAudioAsync(
                text,
                cancellationToken: cancellationToken);

            // Save audio stream to temporary file
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.wav");
            await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                await audioStream.CopyToAsync(fileStream, cancellationToken);
            }

            _logger?.LogInformation("TTS audio saved to temporary file: {TempFilePath}", tempFilePath);

            // Create audio URL (using local file path or upload to OSS if needed)
            // For now, we'll use a local HTTP server URL or file:// URL
            // In production, you might want to upload to OSS and use the OSS URL
            var audioUrl = $"file://{tempFilePath}";

            // Create play API client
            var playBaseUrl = $"http://{soundDeviceSettings.SoundIP}:8888";
            var playHttpClient = _httpClientFactory.CreateClient();
            playHttpClient.BaseAddress = new Uri(playBaseUrl);
            playHttpClient.Timeout = TimeSpan.FromSeconds(30);
            var playApi = RestService.For<ISoundDeviceApi>(playHttpClient);

            // Parse volume (0 means 100)
            var volume = soundDeviceSettings.SoundVolume == "0" ? 100 : int.Parse(soundDeviceSettings.SoundVolume);

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
                            Uri = audioUrl
                        }
                    ],
                    Level = 1,
                    Name = "tts_play_task",
                    Count = 1,
                    Length = 0, // Will be calculated by device
                    Type = 0,
                    TaskId = Guid.NewGuid().ToString()
                }
            };

            // Play audio
            _logger?.LogInformation("Playing audio on sound device: {SoundIP}", soundDeviceSettings.SoundIP);
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

            // Clean up temporary file after a delay (to allow device to read it)
            // In production, you might want to keep the file until playback completes
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                try
                {
                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                    _logger?.LogInformation("Temporary audio file deleted: {TempFilePath}", tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete temporary file: {TempFilePath}", tempFilePath);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing text on sound device: {Text}", text);
            throw;
        }
    }
}

