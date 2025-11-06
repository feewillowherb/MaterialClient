namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Vehicle photo service implementation
/// Returns fixed test file paths for hardware simulation
/// TODO: Replace with actual hardware integration when device is available
/// </summary>
public class VehiclePhotoService : IVehiclePhotoService
{
    private readonly List<string> _testPhotoPaths = new()
    {
        "assets/test_vehicle_1.jpg",
        "assets/test_vehicle_2.jpg",
        "assets/test_vehicle_3.jpg",
        "assets/test_vehicle_4.jpg"
    };

    /// <summary>
    /// Capture vehicle photos
    /// </summary>
    /// <returns>List of photo file paths (typically 4 photos)</returns>
    public Task<List<string>> CaptureVehiclePhotosAsync()
    {
        // Return fixed test file paths
        // In production, this would capture photos from actual camera hardware
        return Task.FromResult(new List<string>(_testPhotoPaths));
    }

    /// <summary>
    /// Set test photo paths for testing purposes (for hardware simulation API)
    /// </summary>
    /// <param name="photoPaths">List of photo file paths</param>
    public void SetPhotoPaths(List<string> photoPaths)
    {
        _testPhotoPaths.Clear();
        _testPhotoPaths.AddRange(photoPaths);
    }

    /// <summary>
    /// Get current test photo paths
    /// </summary>
    public List<string> GetPhotoPaths()
    {
        return new List<string>(_testPhotoPaths);
    }
}

