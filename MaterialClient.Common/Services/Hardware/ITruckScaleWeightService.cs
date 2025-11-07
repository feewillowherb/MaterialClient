namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Truck scale weight service interface
/// </summary>
public interface ITruckScaleWeightService
{
    /// <summary>
    /// Get current weight from truck scale
    /// </summary>
    /// <returns>Current weight in decimal (kg)</returns>
    Task<decimal> GetCurrentWeightAsync();
}

