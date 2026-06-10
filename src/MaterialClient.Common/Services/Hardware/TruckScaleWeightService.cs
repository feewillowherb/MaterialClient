using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
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
    private readonly ILogger<TruckScaleWeightService>? _logger;

    private readonly ReaderWriterLockSlim _rwLock =
        new(LockRecursionPolicy.NoRecursion);

    private readonly ISettingsService _settingsService;
    private readonly ISerialPortFactory _serialPortFactory;

    // Rx Subject for weight updates
    private readonly Subject<decimal> _weightSubject = new();
    private int _byteCount = 12;

    private ScaleSettings? _currentSettings;
    private decimal _currentWeight;
    private string _endChar = "=";
    private bool _isClosing;
    private bool _isListening;

    private ReceType _receType = ReceType.String;

    private ISerialPort? _serialPort;

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
            // Test mode: always treated as online to enable UI + weighing flow.
            if (_currentSettings?.ScaleType == ScaleType.TestMode) return true;
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
                // Test mode: do not open / use physical serial port.
                if (settings.ScaleType == ScaleType.TestMode)
                {
                    _currentSettings = settings;

                    // Prevent any in-flight serial receive from pushing updates.
                    _isClosing = true;

                    // Ensure defaults for conversions (used by UI and weight updates).
                    _receType = ReceType.String;
                    _endChar = "=";
                    _byteCount = 12;
                    return true;
                }

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    if (_currentSettings != null &&
                        _currentSettings.SerialPort == settings.SerialPort &&
                        _currentSettings.BaudRate == settings.BaudRate &&
                        _currentSettings.CommunicationMethod == settings.CommunicationMethod &&
                        _currentSettings.ScaleType == settings.ScaleType)
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
                _serialPort = _serialPortFactory.Create();
                _serialPort.PortName = settings.SerialPort;
                _serialPort.BaudRate = int.Parse(settings.BaudRate);
                _serialPort.DataBits = 8;
                _serialPort.StopBits = StopBits.One;
                _serialPort.Parity = Parity.None;
                _serialPort.WriteBufferSize = 1048576;
                _serialPort.ReadBufferSize = 2097152;
                _serialPort.Encoding = Encoding.GetEncoding("UTF-8");
                _serialPort.Handshake = Handshake.None;
                _serialPort.RtsEnable = true;
                _serialPort.ReadTimeout = 200; // Set timeout to prevent infinite blocking (100ms for faster response)

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
                        // Get scale type to determine which ReceiveHex method to use
                        ScaleType? scaleType;
                        using (_rwLock.ReadLock())
                        {
                            scaleType = _currentSettings?.ScaleType;
                        }

                        if (scaleType == ScaleType.DingSong)
                        {
                            ReceiveHexDingSong(); // Internal lock management
                        }
                        else
                        {
                            ReceiveHexDefault(); // Internal lock management
                        }
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
    ///     Receive HEX format data for Default scale type
    /// </summary>
    private void ReceiveHexDefault()
    {
        try
        {
            // Use read lock to get serial port reference (allows concurrent access)
            ISerialPort? port;
            using (_rwLock.ReadLock())
            {
                port = _serialPort;
                if (port == null) return;
            }

            // I/O operation outside of lock (non-blocking for other threads)
            // Optimized search for valid frame start (0x02) using batch reading
            // This reduces I/O operations and minimizes delay
            byte[] readBuffer = new byte[_byteCount];
            int frameStartIndex = -1;
            int searchBufferSize = _byteCount * 3; // Read larger buffer to search for frame start

            try
            {
                // First, try to read available bytes to search for frame start
                int availableBytes = port.BytesToRead;
                if (availableBytes == 0)
                {
                    // No data available, read first byte with timeout
                    byte firstByte = (byte)port.ReadByte();
                    if (firstByte == 0x02)
                    {
                        readBuffer[0] = firstByte;
                        // Read remaining bytes
                        int receivedCount = 1;
                        while (receivedCount < _byteCount)
                        {
                            int bytesRead = port.Read(readBuffer, receivedCount, _byteCount - receivedCount);
                            receivedCount += bytesRead;
                        }
                    }
                    else
                    {
                        // Invalid first byte, discard and return
                        _logger?.LogWarning($"Invalid first byte 0x{firstByte:X2}, discarding");
                        using var _ = _rwLock.ReadLock();
                        _serialPort?.DiscardInBuffer();
                        return;
                    }
                }
                else
                {
                    // Read available bytes in batch to search for frame start
                    int bytesToRead = Math.Min(availableBytes, searchBufferSize);
                    byte[] searchBuffer = new byte[bytesToRead];
                    int bytesRead = port.Read(searchBuffer, 0, bytesToRead);
                    
                    // Ensure we read some data
                    if (bytesRead == 0)
                    {
                        _logger?.LogWarning("No data read from serial port");
                        using var _ = _rwLock.ReadLock();
                        _serialPort?.DiscardInBuffer();
                        return;
                    }
                    
                    // Search for 0x02 in the buffer
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (searchBuffer[i] == 0x02)
                        {
                            frameStartIndex = i;
                            break;
                        }
                    }
                    
                    if (frameStartIndex == -1)
                    {
                        // No valid frame start found, discard and return
                        _logger?.LogWarning($"No valid frame start (0x02) found in {bytesRead} bytes, discarding");
                        using var _ = _rwLock.ReadLock();
                        _serialPort?.DiscardInBuffer();
                        return;
                    }
                    
                    // Copy from frame start position
                    int remainingInSearchBuffer = bytesRead - frameStartIndex;
                    int bytesToCopy = Math.Min(remainingInSearchBuffer, _byteCount);
                    Array.Copy(searchBuffer, frameStartIndex, readBuffer, 0, bytesToCopy);
                    
                    // If we don't have enough bytes, read the rest with retry
                    if (bytesToCopy < _byteCount)
                    {
                        int receivedCount = bytesToCopy;
                        
                        // Keep reading until we have all bytes
                        // SerialPort.Read will wait for data or timeout based on ReadTimeout setting
                        while (receivedCount < _byteCount)
                        {
                            int remainingBytes = _byteCount - receivedCount;
                            int additionalBytesRead = port.Read(readBuffer, receivedCount, remainingBytes);
                            receivedCount += additionalBytesRead;
                            
                            // If no data was read, the Read method should have thrown TimeoutException
                            // But if we get here, it means Read returned 0, which shouldn't happen with ReadTimeout set
                            // This is a safety check
                            if (additionalBytesRead == 0)
                            {
                                break;
                            }
                        }
                        
                        // Verify we got all bytes
                        if (receivedCount < _byteCount)
                        {
                            _logger?.LogWarning($"Incomplete data read, expected {_byteCount} bytes, got {receivedCount}");
                            using var _ = _rwLock.ReadLock();
                            _serialPort?.DiscardInBuffer();
                            return;
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                // Timeout reading bytes, discard and return
                _logger?.LogWarning("Timeout reading data from truck scale");
                using var _ = _rwLock.ReadLock();
                _serialPort?.DiscardInBuffer();
                return;
            }

            // Verify readBuffer is properly initialized (should always be 0x02 at start)
            if (readBuffer[0] != 0x02)
            {
                _logger?.LogWarning($"Invalid frame start in readBuffer: 0x{readBuffer[0]:X2}, discarding");
                using var _ = _rwLock.ReadLock();
                _serialPort?.DiscardInBuffer();
                return;
            }
            
            // Check frame format: 0x02 at start, 0x03 at end
            if (readBuffer[_byteCount - 1] == 0x03)
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
                
                // Clear buffer after successful parsing to prevent stale data accumulation
                using var clearLock = _rwLock.ReadLock();
                _serialPort?.DiscardInBuffer();
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
    ///     Receive HEX format data for DingSong scale type
    /// </summary>
    private void ReceiveHexDingSong()
    {
        try
        {
            // Use read lock to get serial port reference (allows concurrent access)
            ISerialPort? port;
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
                var parsedWeight = ParseHexWeightDingSong(readBuffer);

                // Only use write lock to update state (hold time < 50ns)
                if (parsedWeight.HasValue)
                {
                    // Convert weight based on scale unit
                    var convertedWeight = ConvertWeight(parsedWeight.Value);
                    
                    using var _ = _rwLock.WriteLock();
                    _currentWeight = convertedWeight;
                    _weightSubject.OnNext(convertedWeight);
                }
                
                // Clear buffer after successful parsing to prevent stale data accumulation
                using var clearLock = _rwLock.ReadLock();
                _serialPort?.DiscardInBuffer();
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
            ISerialPort? port;
            using (_rwLock.ReadLock())
            {
                port = _serialPort;
                if (port == null) return;
            }

            // I/O operation outside of lock (non-blocking for other threads)
            var receivedData = port.ReadTo(_endChar);

            // Validate data format before processing
            if (!IsValidWeightFormat(receivedData))
            {
                _logger?.LogWarning($"Invalid weight data format, discarding: {receivedData}");
                return;
            }

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
            
            // Clear buffer after successful parsing to prevent stale data accumulation
            using var clearLock = _rwLock.ReadLock();
            _serialPort?.DiscardInBuffer();
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

                    // Apply sign
                    if (isNegative) weightInt = -weightInt;

                    _logger?.LogDebug(
                        $"Parsed HEX weight: {weightInt} (raw: {weightString}, sign: {(isNegative ? "-" : "+")})");

                    return weightInt;
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
    ///     Parse weight from HEX data for DingSong scale type
    ///     Format: 0x02 [sign] [8 weight bytes as ASCII] [end marker] 0x03
    ///     Example: 02 2B 30 30 30 30 30 30 30 31 42 03
    ///     STX '+' "00000001" 'B' ETX = 0.01 (assuming 2 decimal places)
    /// </summary>
    /// <returns>Parsed weight in decimal (kg) or null if parsing failed</returns>
    private decimal? ParseHexWeightDingSong(byte[] buffer)
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

            // Extract ASCII weight digits (bytes 2-9, 8 digits total)
            // Format: 8 digits (6 integer + 2 decimal)
            // Example: "00000001" -> 0.01
            var weightString = string.Empty;
            var startIndex = 2; // Skip STX and sign
            var endIndex = 10; // Before end marker and ETX

            // Read 8 ASCII digits
            for (var i = startIndex; i < endIndex; i++)
            {
                var b = buffer[i];
                var c = (char)b;

                // Only include digits
                if (char.IsDigit(c))
                {
                    weightString += c;
                }
                else
                {
                    _logger?.LogWarning($"Non-digit character found at position {i}: 0x{b:X2}");
                    return null;
                }
            }

            // Verify we got exactly 8 digits
            if (weightString.Length != 8)
            {
                _logger?.LogWarning($"Expected 8 digits, got {weightString.Length}: {weightString}");
                return null;
            }

            // End marker (byte 10) is a checksum/status byte, can be any hex character (0x30-0x46)
            // We don't validate it, just ensure it's within valid hex range
            var endMarker = buffer[10];
            if (endMarker < 0x30 || endMarker > 0x46)
            {
                _logger?.LogWarning($"Invalid end marker: 0x{endMarker:X2}, expected hex character (0x30-0x46)");
                return null;
            }

            // Parse the weight string
            // Format: "00000001" -> 0.01 (6 integer + 2 decimal places)
            // Convert to decimal by inserting decimal point
            if (decimal.TryParse(weightString, out var weightInt))
            {
                // Apply decimal point: divide by 100 (2 decimal places)
                var weight = weightInt;

                // Apply sign
                if (isNegative) weight = -weight;

                _logger?.LogDebug(
                    $"Parsed DingSong HEX weight: {weight} (raw: {weightString}, sign: {(isNegative ? "-" : "+")})");

                return weight;
            }

            _logger?.LogWarning($"Failed to parse weight string: {weightString}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing DingSong HEX weight data");
        }

        return null;
    }

    /// <summary>
    ///     Validate weight data format
    ///     Format: +/- + 8 digits + 1 letter (A-F, case insensitive)
    ///     Example: +001570018, +00154001B, -00000001A
    /// </summary>
    /// <param name="data">Data string to validate</param>
    /// <returns>True if format is valid, false otherwise</returns>
    private bool IsValidWeightFormat(string data)
    {
        if (string.IsNullOrEmpty(data))
            return false;

        // Format: +/- + 8 digits + 1 letter (A-F, case insensitive)
        // Regex pattern: ^[+-]\d{8}[A-Fa-f]$
        return Regex.IsMatch(data, @"^[+-]\d{8}[A-Fa-f]$");
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
    ///     If ScaleUnit is TenGram, convert from ten-gram to ton (value / 100000)
    ///     If ScaleUnit is HundredGram, convert from hundred-gram to ton (value / 10000)
    ///     If ScaleUnit is Gram, convert from gram to ton (value / 1000000)
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

        // If ScaleUnit is TenGram, device returns weight in ten-gram units, convert to ton
        if (settings.ScaleUnit == ScaleUnit.TenGram)
        {
            return MaterialMath.TenGramToTon(weightFromDevice);
        }

        // If ScaleUnit is HundredGram, device returns weight in hundred-gram units, convert to ton
        if (settings.ScaleUnit == ScaleUnit.HundredGram)
        {
            return MaterialMath.HundredGramToTon(weightFromDevice);
        }

        // If ScaleUnit is Gram, device returns weight in gram units, convert to ton
        if (settings.ScaleUnit == ScaleUnit.Gram)
        {
            return MaterialMath.GramToTon(weightFromDevice);
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