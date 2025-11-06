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

