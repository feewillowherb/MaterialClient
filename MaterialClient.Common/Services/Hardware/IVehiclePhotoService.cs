namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Vehicle photo service interface
/// </summary>
public interface IVehiclePhotoService
{
    /// <summary>
    /// Capture vehicle photos
    /// </summary>
    /// <returns>List of photo file paths (typically 4 photos)</returns>
    Task<List<string>> CaptureVehiclePhotosAsync();
}

