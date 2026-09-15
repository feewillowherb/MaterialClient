using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.TruckScale.Protocols;
using MaterialClient.Common.Services.TruckScale.Routing;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.TruckScale.Facade;

/// <summary>
///     Truck scale weight service implementation
/// </summary>
[AutoConstructor]
public partial class TruckScaleWeightService : ITruckScaleWeightService, ISingletonDependency
{
    private readonly ILogger<TruckScaleWeightService>? _logger;
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);
    private readonly ISettingsService _settingsService;
    private readonly ISerialPortFactory _serialPortFactory;
    private readonly ITruckScaleProtocolRouter _protocolRouter;

    private readonly Subject<decimal> _weightSubject = new();
    private readonly Subject<ScaleComponentWeights> _componentWeightSubject = new();

    private ScaleSettings? _currentSettings;
    private decimal _currentWeight;
    private ScaleComponentWeights _latestComponentWeights = ScaleComponentWeights.Invalid;
    private bool _isClosing;
    private bool _isListening;
    private ISerialPort? _serialPort;
    private IScaleTransmissionProtocol? _activeProtocol;

    public IObservable<decimal> WeightUpdates => _weightSubject.AsObservable();

    public IObservable<ScaleComponentWeights> ComponentWeightUpdates =>
        _componentWeightSubject.AsObservable();

    public ScaleComponentWeights LatestComponentWeights
    {
        get
        {
            using var _ = _rwLock.ReadLock();
            return _latestComponentWeights;
        }
    }

    public bool IsOnline
    {
        get
        {
            using var _ = _rwLock.ReadLock();
            if (_currentSettings?.ScaleType == ScaleType.TestMode) return true;
            return _serialPort != null && _serialPort.IsOpen && !_isClosing;
        }
    }

    public Task<bool> InitializeAsync(ScaleSettings settings)
    {
        return Task.Run(() =>
        {
            try
            {
                IScaleTransmissionProtocol? protocolToStop = null;
                ScaleSettings? startSettings = null;
                IScaleTransmissionProtocol? startProtocol = null;
                var earlyExitSameSettings = false;

                using (_rwLock.WriteLock())
                {
                    var protocol = _protocolRouter.Resolve(settings.ScaleType, settings.TransmissionFormatType);
                    protocol.EnsureSupported(settings.ScaleType, settings.TransmissionFormatType);

                    if (settings.ScaleType == ScaleType.TestMode)
                    {
                        protocolToStop = DetachActiveProtocolUnderWriteLock();
                        _currentSettings = settings;
                        _activeProtocol = protocol;
                        _latestComponentWeights = ScaleComponentWeights.Invalid;
                        _componentWeightSubject.OnNext(_latestComponentWeights);
                        _isClosing = true;
                        startSettings = settings;
                        startProtocol = protocol;
                    }
                    else if (_serialPort != null &&
                             _serialPort.IsOpen &&
                             _currentSettings != null &&
                             _currentSettings.SerialPort == settings.SerialPort &&
                             _currentSettings.BaudRate == settings.BaudRate &&
                             _currentSettings.TransmissionFormatType == settings.TransmissionFormatType &&
                             _currentSettings.ScaleType == settings.ScaleType &&
                             string.Equals(
                                 _currentSettings.CommunicationParameter,
                                 settings.CommunicationParameter,
                                 StringComparison.Ordinal))
                    {
                        earlyExitSameSettings = true;
                    }
                    else
                    {
                        protocolToStop = DetachActiveProtocolUnderWriteLock();
                    }
                }

                SafeStopProtocol(protocolToStop);

                if (earlyExitSameSettings)
                    return true;

                if (settings.ScaleType != ScaleType.TestMode)
                {
                    using (_rwLock.WriteLock())
                    {
                        CloseSerialPortUnderWriteLock();
                        _isClosing = false;

                        var protocol = _protocolRouter.Resolve(settings.ScaleType, settings.TransmissionFormatType);
                        protocol.EnsureSupported(settings.ScaleType, settings.TransmissionFormatType);

                        _currentSettings = settings;
                        _activeProtocol = protocol;
                        _latestComponentWeights = ScaleComponentWeights.Invalid;
                        _componentWeightSubject.OnNext(_latestComponentWeights);

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
                        _serialPort.ReadTimeout = 200;

                        _serialPort.DataReceived += SerialPort_DataReceived;
                        _serialPort.Open();
                        _isClosing = false;
                        _logger?.LogInformation(
                            "Truck scale serial port opened: {Port} at {BaudRate} baud; protocol={Protocol} ScaleType={ScaleType} TransmissionFormatType={Format} CommunicationParameter={Parameter}",
                            settings.SerialPort,
                            settings.BaudRate,
                            protocol.GetType().Name,
                            settings.ScaleType,
                            settings.TransmissionFormatType,
                            settings.CommunicationParameter);

                        startSettings = settings;
                        startProtocol = protocol;
                    }
                }

                // OnStart outside WriteLock: Type1 Exchange uses GetSerialPort (ReadLock) + PublishWeight (WriteLock).
                if (startProtocol != null && startSettings != null)
                    startProtocol.OnStart(CreateProtocolContext(startSettings));

                return true;
            }
            catch (UnsupportedTransmissionFormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize truck scale serial port: {Message}", ex.Message);
                return false;
            }
        });
    }

    public async Task<decimal> GetCurrentWeightAsync()
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                var settings = await _settingsService.GetSettingsAsync();
                await InitializeAsync(settings.ScaleSettings);
            }

            using var _ = _rwLock.ReadLock();
            return _currentWeight;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting current weight: {Message}", ex.Message);
            return 0m;
        }
    }

    public void Close() => CloseInternal();

    public async Task<bool> RestartAsync()
    {
        try
        {
            CloseInternal();
            var settings = await _settingsService.GetSettingsAsync();
            return await InitializeAsync(settings.ScaleSettings);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error restarting truck scale service: {Message}", ex.Message);
            return false;
        }
    }

    public void SetWeight(decimal weight)
    {
        using var _ = _rwLock.WriteLock();
        _currentWeight = weight;
        _latestComponentWeights = ScaleComponentWeights.Invalid;
        _weightSubject.OnNext(weight);
        _componentWeightSubject.OnNext(_latestComponentWeights);
    }

    public decimal GetCurrentWeight()
    {
        using var _ = _rwLock.ReadLock();
        return _currentWeight;
    }

    public async ValueTask DisposeAsync()
    {
        Close();
        _weightSubject.Dispose();
        _componentWeightSubject.Dispose();
        _rwLock.Dispose();
        await Task.CompletedTask;
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_isClosing) return;

            using (_rwLock.ReadLock())
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
            }

            _isListening = true;

            try
            {
                ScaleSettings? settings;
                IScaleTransmissionProtocol? protocol;
                using (_rwLock.ReadLock())
                {
                    settings = _currentSettings;
                    protocol = _activeProtocol;
                }

                if (settings == null || protocol == null) return;

                protocol.OnDataReceived(CreateProtocolContext(settings));
            }
            finally
            {
                _isListening = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error receiving data from truck scale: {Message}", ex.Message);
            _isListening = false;
        }
    }

    private ScaleProtocolContext CreateProtocolContext(ScaleSettings settings) =>
        new(
            settings,
            () =>
            {
                using var _ = _rwLock.ReadLock();
                return _serialPort;
            },
            PublishWeight,
            PublishComponentWeights,
            ConvertWeight,
            _logger);

    private void PublishWeight(decimal convertedWeight)
    {
        using var _ = _rwLock.WriteLock();
        _currentWeight = convertedWeight;
        _weightSubject.OnNext(convertedWeight);
    }

    private void PublishComponentWeights(ScaleComponentWeights components)
    {
        using var _ = _rwLock.WriteLock();
        _latestComponentWeights = components;
        _componentWeightSubject.OnNext(components);
    }

    private decimal ConvertWeight(decimal weightFromDevice)
    {
        ScaleSettings? settings;
        using (_rwLock.ReadLock())
        {
            settings = _currentSettings;
        }

        if (settings == null)
            return MaterialMath.ConvertKgToTon(weightFromDevice);

        if (settings.ScaleUnit == ScaleUnit.Kg)
            return MaterialMath.ConvertKgToTon(weightFromDevice);

        if (settings.ScaleUnit == ScaleUnit.TenGram)
            return MaterialMath.TenGramToTon(weightFromDevice);

        if (settings.ScaleUnit == ScaleUnit.HundredGram)
            return MaterialMath.HundredGramToTon(weightFromDevice);

        if (settings.ScaleUnit == ScaleUnit.Gram)
            return MaterialMath.GramToTon(weightFromDevice);

        return weightFromDevice;
    }

    private void CloseInternal()
    {
        _isClosing = true;

        var waitCount = 0;
        while (_isListening && waitCount < 100)
        {
            Thread.Sleep(10);
            waitCount++;
        }

        // Stop protocol outside WriteLock so Type1 timer can finish PublishWeight without deadlock.
        IScaleTransmissionProtocol? protocolToStop;
        using (_rwLock.WriteLock())
        {
            protocolToStop = DetachActiveProtocolUnderWriteLock();
        }

        SafeStopProtocol(protocolToStop);

        using (_rwLock.WriteLock())
        {
            CloseSerialPortUnderWriteLock();
            _isClosing = false;
        }
    }

    private IScaleTransmissionProtocol? DetachActiveProtocolUnderWriteLock()
    {
        var protocol = _activeProtocol;
        _activeProtocol = null;
        return protocol;
    }

    private void SafeStopProtocol(IScaleTransmissionProtocol? protocol)
    {
        if (protocol == null) return;
        try
        {
            protocol.OnStop();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping truck scale protocol: {Message}", ex.Message);
        }
    }

    private void CloseSerialPortUnderWriteLock()
    {
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
            else if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error closing serial port: {Message}", ex.Message);
        }
    }
}
