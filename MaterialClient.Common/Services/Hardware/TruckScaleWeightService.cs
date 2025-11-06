namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Truck scale weight service implementation
/// Returns fixed test value for hardware simulation
/// TODO: Replace with actual hardware integration when device is available
/// </summary>
public class TruckScaleWeightService : ITruckScaleWeightService
{
    private decimal _currentWeight = 0m; // Default weight (empty scale)

    /// <summary>
    /// Get current weight from truck scale
    /// </summary>
    /// <returns>Current weight in decimal (kg)</returns>
    public Task<decimal> GetCurrentWeightAsync()
    {
        // Return fixed test value
        // In production, this would read from actual hardware
        return Task.FromResult(_currentWeight);
    }

    /// <summary>
    /// Set weight for testing purposes (for hardware simulation API)
    /// </summary>
    /// <param name="weight">Weight value to set</param>
    public void SetWeight(decimal weight)
    {
        _currentWeight = weight;
    }

    /// <summary>
    /// Get current weight synchronously (for testing)
    /// </summary>
    public decimal GetCurrentWeight()
    {
        return _currentWeight;
    }
}

