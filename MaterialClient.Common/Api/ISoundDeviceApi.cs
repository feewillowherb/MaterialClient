using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

/// <summary>
///     Sound device and TTS API client interface
/// </summary>
public interface ISoundDeviceApi
{
    /// <summary>
    ///     Get TTS audio (returns audio stream from TTS service)
    /// </summary>
    /// <param name="text">Text to convert to speech</param>
    /// <param name="voiceName">Voice name (default: xiaoyan)</param>
    /// <param name="speed">Speech speed (default: 50)</param>
    /// <param name="volume">Volume (default: 100)</param>
    /// <param name="origin">Origin URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TTS audio stream</returns>
    /// <remarks>
    ///     Base URL should be set to http://{LocalIP}:10008 when creating the Refit client for TTS.
    ///     The sound device endpoint should use base URL http://{SoundIP}:8888.
    /// </remarks>
    [Get("/tts_xf.single")]
    Task<Stream> GetTtsAudioAsync(
        [Query] string text,
        [Query] [AliasAs("voice_name")] string voiceName = "xiaoyan",
        [Query] int speed = 50,
        [Query] int volume = 100,
        [Query] string? origin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Play audio on sound device
    /// </summary>
    /// <param name="request">Play request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Play response JSON string (can be deserialized to SoundDevicePlaySuccessResponseDto or SoundDevicePlayErrorResponseDto)</returns>
    /// <remarks>
    ///     Base URL should be set to http://{SoundIP}:8888 when creating the Refit client.
    ///     The response can be either a success response (code=0, type="resp") or an error response (result=-1).
    ///     Use JsonSerializer to deserialize to the appropriate type based on the response content.
    /// </remarks>
    [Post("")]
    Task<string> PlayAudioAsync(
        [Body] SoundDevicePlayRequestDto request,
        CancellationToken cancellationToken = default);
}