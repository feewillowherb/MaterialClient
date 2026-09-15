using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services.TruckScale.Protocols;

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
    ///     Observable stream of instrument component weights (tare/gross/net).
    ///     Invalid / incomplete components publish <see cref="ScaleComponentWeights.Invalid"/>.
    /// </summary>
    IObservable<ScaleComponentWeights> ComponentWeightUpdates { get; }

    /// <summary>
    ///     Latest component weights (may be invalid when not all of tare/gross/net are present).
    /// </summary>
    ScaleComponentWeights LatestComponentWeights { get; }

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
