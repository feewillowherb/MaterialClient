using MaterialClient.Common.Configuration;

namespace MaterialClient.Common.Services.TruckScale.Facade;

/// <summary>
///     Truck scale weight service interface
/// </summary>
public interface ITruckScaleWeightService : IAsyncDisposable
{
    /// <summary>
    ///     Observable stream of weight updates from truck scale
    /// </summary>
    IObservable<decimal> WeightUpdates { get; }

    /// <summary>
    ///     Check if truck scale is online (serial port is open and connected)
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    ///     Get current weight from truck scale
    /// </summary>
    Task<decimal> GetCurrentWeightAsync();

    /// <summary>
    ///     Initialize serial port connection with settings
    /// </summary>
    Task<bool> InitializeAsync(ScaleSettings settings);

    /// <summary>
    ///     Close serial port connection
    /// </summary>
    void Close();

    /// <summary>
    ///     Restart the truck scale service with current settings
    /// </summary>
    Task<bool> RestartAsync();

    /// <summary>
    ///     Set weight for testing purposes (for hardware simulation API)
    /// </summary>
    void SetWeight(decimal weight);

    /// <summary>
    ///     Get current weight synchronously (for testing)
    /// </summary>
    decimal GetCurrentWeight();
}
