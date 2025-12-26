using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hardware;

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
    /// <returns>Current weight in decimal (kg)</returns>
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

/// <summary>
///     Truck scale weight service implementation
///     Uses serial port communication to read weight from truck scale
/// </summary>
[AutoConstructor]
public partial class TruckScaleWeightService : ITruckScaleWeightService, ISingletonDependency
{
    private const decimal TonDecimal = 100m;
    private readonly ILogger<TruckScaleWeightService>? _logger;

    private readonly ReaderWriterLockSlim _rwLock =
        new(LockRecursionPolicy.NoRecursion);

    private readonly ISettingsService _settingsService;

    // Rx Subject for weight updates
    private readonly Subject<decimal> _weightSubject = new();
    private int _byteCount = 10;

    private ScaleSettings? _currentSettings;
    private decimal _currentWeight;
    private string _endChar = "=";
    private bool _isClosing;
    private bool _isListening;

    private ReceType _receType = ReceType.String;

    private SerialPort? _serialPort;

    /// <summary>
    ///     Observable stream of weight updates from truck scale
    /// </summary>
    public IObservable<decimal> WeightUpdates => _weightSubject.AsObservable();

    /// <summary>
    ///     Check if truck scale is online (serial port is open and connected)
    /// </summary>
    public bool IsOnline
    {
        get
        {
            using var _ = _rwLock.ReadLock();
            return _serialPort != null && _serialPort.IsOpen && !_isClosing;
        }
    }


    /// <summary>
    ///     Initialize serial port connection with settings
    /// </summary>
    public Task<bool> InitializeAsync(ScaleSettings settings)
    {
        return Task.Run(() =>
        {
            try
            {
                using var _ = _rwLock.WriteLock();
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    if (_currentSettings != null &&
                        _currentSettings.SerialPort == settings.SerialPort &&
                        _currentSettings.BaudRate == settings.BaudRate &&
                        _currentSettings.CommunicationMethod == settings.CommunicationMethod)
                        // Settings haven't changed, keep existing connection
                        return true;

                    // Settings changed, close and reopen
                    CloseInternal();
                }

                _currentSettings = settings;

                // Determine receiving type based on communication method
                if (settings.CommunicationMethod == "TF0")
                {
                    _receType = ReceType.Hex;
                    _byteCount = 12;
                }
                else
                {
                    _receType = ReceType.String;
                    _endChar = "=";
                }

                // Create and configure serial port
                _serialPort = new SerialPort
                {
                    PortName = settings.SerialPort,
                    BaudRate = int.Parse(settings.BaudRate),
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    WriteBufferSize = 1048576,
                    ReadBufferSize = 2097152,
                    Encoding = Encoding.GetEncoding("UTF-8"),
                    Handshake = Handshake.None,
                    RtsEnable = true
                };

                // Subscribe to data received event
                _serialPort.DataReceived += SerialPort_DataReceived;

                // Open serial port
                _serialPort.Open();
                _isClosing = false;
                _logger?.LogInformation(
                    $"Truck scale serial port opened: {settings.SerialPort} at {settings.BaudRate} baud");

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize truck scale serial port: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    ///     Get current weight from truck scale
    /// </summary>
    public async Task<decimal> GetCurrentWeightAsync()
    {
        try
        {
            // Ensure serial port is initialized
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                var settings = await _settingsService.GetSettingsAsync();
                await InitializeAsync(settings.ScaleSettings);
            }

            // Return the last received weight
            using var _ = _rwLock.ReadLock();
            return _currentWeight;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error getting current weight: {ex.Message}");
            return 0m;
        }
    }

    /// <summary>
    ///     Close serial port connection
    /// </summary>
    public void Close()
    {
        CloseInternal();
    }

    /// <summary>
    ///     Restart the truck scale service with current settings
    /// </summary>
    public async Task<bool> RestartAsync()
    {
        try
        {
            // Close existing connection
            CloseInternal();

            // Get current settings and reinitialize
            var settings = await _settingsService.GetSettingsAsync();
            return await InitializeAsync(settings.ScaleSettings);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error restarting truck scale service: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    ///     Set weight for testing purposes (for hardware simulation API)
    /// </summary>
    public void SetWeight(decimal weight)
    {
        using var _ = _rwLock.WriteLock();
        _currentWeight = weight;

        // Push weight update to Rx stream
        _weightSubject.OnNext(weight);
    }

    /// <summary>
    ///     Get current weight synchronously (for testing)
    /// </summary>
    public decimal GetCurrentWeight()
    {
        using var _ = _rwLock.ReadLock();
        return _currentWeight;
    }

    public async ValueTask DisposeAsync()
    {
        Close();
        _weightSubject?.Dispose();
        _rwLock?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Serial port data received event handler
    /// </summary>
    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_isClosing) return;

            // Use read lock to check state (allows concurrent access)
            using (_rwLock.ReadLock())
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
            }

            // I/O and parsing completely outside of lock
            _isListening = true;

            try
            {
                switch (_receType)
                {
                    case ReceType.Hex:
                        ReceiveHex(); // Internal lock management
                        break;
                    case ReceType.String:
                        ReceiveString(); // Internal lock management
                        break;
                }
            }
            finally
            {
                _isListening = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error receiving data from truck scale: {ex.Message}");
            _isListening = false;
        }
    }

    /// <summary>
    ///     Receive HEX format data
    /// </summary>
    private void ReceiveHex()
    {
        try
        {
            // Use read lock to get serial port reference (allows concurrent access)
            SerialPort? port;
            using (_rwLock.ReadLock())
            {
                port = _serialPort;
                if (port == null) return;
            }

            // I/O operation outside of lock (non-blocking for other threads)
            var receivedCount = 0;
            var readBuffer = new byte[_byteCount];

            while (receivedCount < _byteCount)
            {
                var bytesRead = port.Read(readBuffer, receivedCount, _byteCount - receivedCount);
                receivedCount += bytesRead;
            }

            // Check frame format: 0x02 at start, 0x03 at end
            if (readBuffer[0] == 0x02 && readBuffer[_byteCount - 1] == 0x03)
            {
                // Parse data outside of lock
                var parsedWeight = ParseHexWeight(readBuffer);

                // Only use write lock to update state (hold time < 50ns)
                if (parsedWeight.HasValue)
                {
                    // Convert weight based on scale unit
                    var convertedWeight = ConvertWeight(parsedWeight.Value);
                    
                    using var _ = _rwLock.WriteLock();
                    _currentWeight = convertedWeight;
                    _weightSubject.OnNext(convertedWeight);
                }
            }
            else
            {
                // Discard buffer also needs read lock
                using var _ = _rwLock.ReadLock();
                _serialPort?.DiscardInBuffer();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error receiving HEX data from truck scale");
        }
    }

    /// <summary>
    ///     Receive String format data
    /// </summary>
    private void ReceiveString()
    {
        try
        {
            // Use read lock to get serial port reference (allows concurrent access)
            SerialPort? port;
            using (_rwLock.ReadLock())
            {
                port = _serialPort;
                if (port == null) return;
            }

            // I/O operation outside of lock (non-blocking for other threads)
            var receivedData = port.ReadTo(_endChar);

            // Reverse the string as per reference implementation (outside of lock)
            var reversed = string.Empty;
            for (var i = receivedData.Length - 1; i >= 0; i--) reversed += receivedData[i];

            // Parse data outside of lock
            var parsedWeight = ParseStringWeight(reversed);

            // Only use write lock to update state (hold time < 50ns)
            if (parsedWeight.HasValue)
            {
                // Convert weight based on scale unit
                var convertedWeight = ConvertWeight(parsedWeight.Value);
                
                using var _ = _rwLock.WriteLock();
                _currentWeight = convertedWeight;
                _weightSubject.OnNext(convertedWeight);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error receiving String data from truck scale");
        }
    }

    /// <summary>
    ///     Parse weight from HEX data
    ///     Format: 0x02 [sign] [weight bytes as ASCII] [other] 0x03
    ///     Example: 02 2B 30 30 30 32 30 35 32 31 45 03
    ///     STX '+' "0002" "05" "21" 'E' ETX = 2.05
    /// </summary>
    /// <returns>Parsed weight in decimal (kg) or null if parsing failed</returns>
    private decimal? ParseHexWeight(byte[] buffer)
    {
        try
        {
            if (buffer.Length < 12) return null;

            // Check frame format: 0x02 at start, 0x03 at end
            if (buffer[0] != 0x02 || buffer[buffer.Length - 1] != 0x03)
            {
                _logger?.LogWarning($"Invalid frame format: STX={buffer[0]:X2}, ETX={buffer[buffer.Length - 1]:X2}");
                return null;
            }

            // Parse sign byte (byte 1): 0x2B = '+', 0x2D = '-'
            var isNegative = buffer[1] == 0x2D;

            // Extract ASCII weight digits (bytes 2 onwards until we find 'E')
            // Format: 4 digits (integer part) + 2 digits (decimal part) = 6 digits total
            // Example: "000205" -> 2.05
            var weightString = string.Empty;
            var startIndex = 2; // Skip STX and sign

            // Read ASCII digits until we encounter 'E' (0x45) or reach 6 digits
            for (var i = startIndex; i < buffer.Length - 1; i++) // -1 to skip ETX
            {
                var b = buffer[i];

                // Stop at 'E' marker (0x45)
                if (b == 0x45) break;

                // Convert ASCII to character
                var c = (char)b;

                // Only include digits, and limit to 6 digits (4 integer + 2 decimal)
                if (char.IsDigit(c) && weightString.Length < 6) weightString += c;
            }

            if (!string.IsNullOrEmpty(weightString) && weightString.Length >= 1)
            {
                // Parse the weight string
                // Format: "000205" -> 2.05 (assuming 2 decimal places)
                // The string contains integer part + decimal part without decimal point
                if (decimal.TryParse(weightString, out var weightInt))
                {
                    // Parse as kg (raw value divided by 100 to get decimal kg)
                    // Example: "000205" -> 205 / 100 = 2.05 kg
                    var parsedWeight = weightInt / TonDecimal;

                    // Apply sign
                    if (isNegative) parsedWeight = -parsedWeight;

                    _logger?.LogDebug(
                        $"Parsed HEX weight: {parsedWeight} (raw: {weightString}, sign: {(isNegative ? "-" : "+")})");

                    return parsedWeight;
                }

                _logger?.LogWarning($"Failed to parse weight string: {weightString}");
            }
            else
            {
                _logger?.LogWarning("No weight digits found in buffer");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing HEX weight data");
        }

        return null;
    }

    /// <summary>
    ///     Parse weight from String data
    ///     Format: reversed string ending with "="
    ///     Example: "=76.54321" reversed = "12345.67="
    ///     Note: The unit of returned value depends on ScaleUnit setting (kg or ton)
    /// </summary>
    /// <returns>Parsed weight in decimal (unit depends on ScaleUnit setting) or null if parsing failed</returns>
    private decimal? ParseStringWeight(string data)
    {
        try
        {
            // Remove the end character if present
            var weightString = data.TrimEnd('=');

            // Try to parse as decimal (weight in kg)
            if (decimal.TryParse(weightString, out var weight))
            {
                _logger?.LogDebug($"Parsed String weight: {weight}");
                return weight;
            }

            _logger?.LogWarning($"Failed to parse weight string: {data}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Error parsing String weight data: {data}");
        }

        return null;
    }

    /// <summary>
    ///     Convert weight based on scale unit setting
    ///     ScaleUnit represents the unit of the value returned by the device
    ///     Software always uses ton (t) as the weight unit
    ///     If ScaleUnit is Kg, convert from kg to ton using MaterialMath.ConvertKgToTon
    ///     If ScaleUnit is Ton, no conversion needed (device already returns ton)
    /// </summary>
    /// <param name="weightFromDevice">Weight from device (unit depends on ScaleUnit setting)</param>
    /// <returns>Weight in ton (t) for software use</returns>
    private decimal ConvertWeight(decimal weightFromDevice)
    {
        // Get current settings (read-only access)
        ScaleSettings? settings;
        using (_rwLock.ReadLock())
        {
            settings = _currentSettings;
        }

        // If no settings, assume device returns kg and convert to ton (default behavior)
        if (settings == null)
        {
            return MaterialMath.ConvertKgToTon(weightFromDevice);
        }

        // If ScaleUnit is Kg, device returns kg, convert to ton
        if (settings.ScaleUnit == ScaleUnit.Kg)
        {
            return MaterialMath.ConvertKgToTon(weightFromDevice);
        }

        // If ScaleUnit is Ton, device already returns ton, no conversion needed
        return weightFromDevice;
    }

    /// <summary>
    ///     Internal method to close serial port
    /// </summary>
    private void CloseInternal()
    {
        // Set closing flag outside of lock
        _isClosing = true;

        // Wait for any ongoing receive operation to complete (outside of lock)
        var waitCount = 0;
        while (_isListening && waitCount < 100)
        {
            Thread.Sleep(10);
            waitCount++;
        }

        // Acquire write lock only for cleanup
        using var _ = _rwLock.WriteLock();
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;

                _logger?.LogInformation("Truck scale serial port closed");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error closing serial port: {ex.Message}");
        }
        finally
        {
            _isClosing = false;
        }
    }

    // Receiving parameters
    private enum ReceType
    {
        Hex = 0,
        String = 1
    }
}