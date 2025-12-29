using MaterialClient.Common.Api;
using Refit;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Tests for ISoundDeviceApi GetTtsAudioAsync method
/// </summary>
public class SoundDeviceApiTests
{
    /// <summary>
    ///     Test that GetTtsAudioAsync returns a valid audio stream from actual API
    /// </summary>
    [Fact]
    public async Task GetTtsAudioAsync_Should_ReturnAudioStream()
    {
        // Arrange
        var localIP = "localhost";
        var ttsBaseUrl = $"http://{localIP}:10008";
        var ttsHttpClient = new HttpClient
        {
            BaseAddress = new Uri(ttsBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var ttsApi = RestService.For<ISoundDeviceApi>(ttsHttpClient);
        var testText = "Hello, this is a test";

        // Act
        await using var result = await ttsApi.GetTtsAudioAsync(
            testText,
            "xiaoyan",
            50,
            100,
            null,
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.CanRead.ShouldBeTrue();
        
        // Verify the stream contains audio data (at least some bytes)
        var buffer = new byte[1024];
        var bytesRead = await result.ReadAsync(buffer, 0, buffer.Length);
        bytesRead.ShouldBeGreaterThan(0, "Audio stream should contain data");

        // Cleanup
        ttsHttpClient.Dispose();
    }
}

