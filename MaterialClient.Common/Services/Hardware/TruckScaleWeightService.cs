using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Truck scale weight service interface
/// </summary>
public interface ITruckScaleWeightService : IDisposable
{
    /// <summary>
    /// Observable stream of weight updates from truck scale
    /// </summary>
    IObservable<decimal> WeightUpdates { get; }

    /// <summary>
    /// Get current weight from truck scale
    /// </summary>
    /// <returns>Current weight in decimal (kg)</returns>
    Task<decimal> GetCurrentWeightAsync();

    /// <summary>
    /// Initialize serial port connection with settings
    /// </summary>
    Task<bool> InitializeAsync(ScaleSettings settings);

    /// <summary>
    /// Close serial port connection
    /// </summary>
    void Close();

    /// <summary>
    /// Restart the truck scale service with current settings
    /// </summary>
    Task<bool> RestartAsync();

    /// <summary>
    /// Set weight for testing purposes (for hardware simulation API)
    /// </summary>
    void SetWeight(decimal weight);

    /// <summary>
    /// Get current weight synchronously (for testing)
    /// </summary>
    decimal GetCurrentWeight();

    /// <summary>
    /// Check if truck scale is online (serial port is open and connected)
    /// </summary>
    bool IsOnline { get; }
}

/// <summary>
/// Truck scale weight service implementation
/// Uses serial port communication to read weight from truck scale
/// </summary>
public class TruckScaleWeightService : ITruckScaleWeightService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TruckScaleWeightService>? _logger;
    
    private SerialPort? _serialPort;
    private decimal _currentWeight = 0m;
    private bool _isListening = false;
    private bool _isClosing = false;
    private readonly object _lockObject = new();
    private ScaleSettings? _currentSettings;
    
    // Rx Subject for weight updates
    private readonly Subject<decimal> _weightSubject = new();

    // Receiving parameters
    private enum ReceType
    {
        Hex = 0,
        String = 1
    }

    private ReceType _receType = ReceType.String;
    private int _byteCount = 10;
    private string _endChar = "=";

    /// <summary>
    /// Observable stream of weight updates from truck scale
    /// </summary>
    public IObservable<decimal> WeightUpdates => _weightSubject.AsObservable();

    /// <summary>
    /// Check if truck scale is online (serial port is open and connected)
    /// </summary>
    public bool IsOnline
    {
        get
        {
            lock (_lockObject)
            {
                return _serialPort != null && _serialPort.IsOpen && !_isClosing;
            }
        }
    }

    public TruckScaleWeightService(
        ISettingsService settingsService,
        ILogger<TruckScaleWeightService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize serial port connection with settings
    /// </summary>
    public Task<bool> InitializeAsync(ScaleSettings settings)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        if (_currentSettings != null &&
                            _currentSettings.SerialPort == settings.SerialPort &&
                            _currentSettings.BaudRate == settings.BaudRate &&
                            _currentSettings.CommunicationMethod == settings.CommunicationMethod)
                        {
                            // Settings haven't changed, keep existing connection
                            return true;
                        }
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
                    _logger?.LogInformation($"Truck scale serial port opened: {settings.SerialPort} at {settings.BaudRate} baud");
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize truck scale serial port: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Serial port data received event handler
    /// </summary>
    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_isClosing) return;

            lock (_lockObject)
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                _isListening = true;

                switch (_receType)
                {
                    case ReceType.Hex:
                        ReceiveHex();
                        break;
                    case ReceType.String:
                        ReceiveString();
                        break;
                }

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
    /// Receive HEX format data
    /// </summary>
    private void ReceiveHex()
    {
        try
        {
            if (_serialPort == null) return;

            int receivedCount = 0;
            byte[] readBuffer = new byte[_byteCount];
            
            while (receivedCount < _byteCount)
            {
                int bytesRead = _serialPort.Read(readBuffer, receivedCount, _byteCount - receivedCount);
                receivedCount += bytesRead;
            }

            // Check frame format: 0x02 at start, 0x03 at end
            if (readBuffer[0] == 0x02 && readBuffer[_byteCount - 1] == 0x03)
            {
                ParseHexWeight(readBuffer);
            }
            else
            {
                _serialPort.DiscardInBuffer();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error receiving HEX data from truck scale");
        }
    }

    /// <summary>
    /// Receive String format data
    /// </summary>
    private void ReceiveString()
    {
        try
        {
            if (_serialPort == null) return;

            string receivedData = _serialPort.ReadTo(_endChar);
            
            // Reverse the string as per reference implementation
            var reversed = string.Empty;
            for (int i = receivedData.Length - 1; i >= 0; i--)
            {
                reversed += receivedData[i];
            }
            
            ParseStringWeight(reversed);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error receiving String data from truck scale");
        }
    }

    /// <summary>
    /// Parse weight from HEX data
    /// Format: 0x02 [weight bytes] 0x03
    /// Weight bytes are typically BCD encoded or ASCII
    /// </summary>
    private void ParseHexWeight(byte[] buffer)
    {
        try
        {
            // Extract weight bytes (skip 0x02 and 0x03)
            // Assuming weight is in bytes 1-10 (12 bytes total: 0x02 + 10 weight bytes + 0x03)
            // Common format: BCD encoded weight
            // Example: 0x02 0x01 0x23 0x45 0x67 0x89 0x00 0x00 0x00 0x00 0x00 0x03 = 12345.67
            
            if (buffer.Length < 12) return;

            // Try to parse as BCD (Binary Coded Decimal)
            // Bytes 1-6 might contain weight digits
            var weightString = string.Empty;
            for (int i = 1; i < 7; i++)
            {
                byte b = buffer[i];
                // Convert BCD to decimal
                int high = (b >> 4) & 0x0F;
                int low = b & 0x0F;
                if (high <= 9 && low <= 9)
                {
                    weightString += high.ToString();
                    weightString += low.ToString();
                }
            }

            if (!string.IsNullOrEmpty(weightString))
            {
                // Parse as decimal with 2 decimal places
                if (decimal.TryParse(weightString, out decimal weight))
                {
                    decimal parsedWeight = weight / 100m; // Convert from integer to decimal (e.g., 1234567 -> 12345.67)
                    lock (_lockObject)
                    {
                        _currentWeight = parsedWeight;
                    }
                    _logger?.LogDebug($"Parsed HEX weight: {parsedWeight} kg");
                    // Push weight update to Rx stream
                    _weightSubject.OnNext(parsedWeight);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing HEX weight data");
        }
    }

    /// <summary>
    /// Parse weight from String data
    /// Format: reversed string ending with "="
    /// Example: "=76.54321" reversed = "12345.67="
    /// </summary>
    private void ParseStringWeight(string data)
    {
        try
        {
            // Remove the end character if present
            string weightString = data.TrimEnd('=');
            
            // Try to parse as decimal
            if (decimal.TryParse(weightString, out decimal weight))
            {
                lock (_lockObject)
                {
                    _currentWeight = weight;
                }
                _logger?.LogDebug($"Parsed String weight: {weight} kg");
                // Push weight update to Rx stream
                _weightSubject.OnNext(weight);
            }
            else
            {
                _logger?.LogWarning($"Failed to parse weight string: {data}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Error parsing String weight data: {data}");
        }
    }

    /// <summary>
    /// Get current weight from truck scale
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
            lock (_lockObject)
            {
                return _currentWeight;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error getting current weight: {ex.Message}");
            return 0m;
        }
    }

    /// <summary>
    /// Close serial port connection
    /// </summary>
    public void Close()
    {
        CloseInternal();
    }

    /// <summary>
    /// Restart the truck scale service with current settings
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
    /// Internal method to close serial port
    /// </summary>
    private void CloseInternal()
    {
        lock (_lockObject)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _isClosing = true;
                    
                    // Wait for any ongoing receive operation to complete
                    int waitCount = 0;
                    while (_isListening && waitCount < 100)
                    {
                        Thread.Sleep(10);
                        waitCount++;
                    }

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
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        Close();
        _weightSubject?.Dispose();
    }

    /// <summary>
    /// Set weight for testing purposes (for hardware simulation API)
    /// </summary>
    public void SetWeight(decimal weight)
    {
        lock (_lockObject)
        {
            _currentWeight = weight;
        }
        // Push weight update to Rx stream
        _weightSubject.OnNext(weight);
    }

    /// <summary>
    /// Get current weight synchronously (for testing)
    /// </summary>
    public decimal GetCurrentWeight()
    {
        lock (_lockObject)
        {
            return _currentWeight;
        }
    }
}
