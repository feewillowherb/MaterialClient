namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Plate number capture service interface
/// </summary>
public interface IPlateNumberCaptureService
{
    /// <summary>
    /// Capture plate number from vehicle
    /// </summary>
    /// <returns>Plate number string (can be empty if capture fails)</returns>
    Task<string?> CapturePlateNumberAsync();
}

/// <summary>
/// Plate number capture service implementation
/// Returns fixed test value for hardware simulation
/// TODO: Replace with actual hardware integration when device is available
/// </summary>
public class PlateNumberCaptureService : IPlateNumberCaptureService
{
    private string? _testPlateNumber = "京A12345"; // Default test plate number

    /// <summary>
    /// Capture plate number from vehicle
    /// </summary>
    /// <returns>Plate number string (can be empty if capture fails)</returns>
    public Task<string?> CapturePlateNumberAsync()
    {
        // Return fixed test value
        // In production, this would call actual plate recognition hardware
        return Task.FromResult(_testPlateNumber);
    }

    /// <summary>
    /// Set plate number for testing purposes (for hardware simulation API)
    /// </summary>
    /// <param name="plateNumber">Plate number to set</param>
    public void SetPlateNumber(string? plateNumber)
    {
        _testPlateNumber = plateNumber;
    }

    /// <summary>
    /// Get current test plate number
    /// </summary>
    public string? GetPlateNumber()
    {
        return _testPlateNumber;
    }
}

