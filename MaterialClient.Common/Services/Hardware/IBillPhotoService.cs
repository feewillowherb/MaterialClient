namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Bill photo service interface
/// </summary>
public interface IBillPhotoService
{
    /// <summary>
    /// Capture bill photo
    /// </summary>
    /// <returns>Photo file path (can be empty if capture fails)</returns>
    Task<string?> CaptureBillPhotoAsync();
}

