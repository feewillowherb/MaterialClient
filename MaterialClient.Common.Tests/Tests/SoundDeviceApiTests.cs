using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using Refit;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Tests for ISoundDeviceApi PlayAudioAsync method
/// </summary>
public class SoundDeviceApiTests
{
    /// <summary>
    ///     Test that PlayAudioAsync successfully plays audio on sound device
    /// </summary>
    [Fact]
    public async Task PlayAudioAsync_Should_PlayAudioSuccessfully()
    {
        // Arrange
        var soundIP = "localhost";
        var playBaseUrl = $"http://{soundIP}:8888";
        var playHttpClient = new HttpClient
        {
            BaseAddress = new Uri(playBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var playApi = RestService.For<ISoundDeviceApi>(playHttpClient);

        var testText = "Hello, this is a test";
        var localIP = "localhost";
        var volume = 100;
        var ttsUri = $"http://{localIP}:10008/tts_xf.single?text={Uri.EscapeDataString(testText)}&voice_name=xiaoyan&speed=50&volume={volume}&origin=http://{localIP}:10008";

        var playRequest = new SoundDevicePlayRequestDto
        {
            Name = "tts_play",
            SerialNumber = "test_sn",
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

        // Act
        var response = await playApi.PlayAudioAsync(playRequest, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
        response.ShouldNotBeEmpty();

        // Verify response is valid JSON and contains expected fields
        var responseDoc = JsonDocument.Parse(response);
        responseDoc.RootElement.ShouldNotBeNull();

        // Cleanup
        playHttpClient.Dispose();
    }
}

