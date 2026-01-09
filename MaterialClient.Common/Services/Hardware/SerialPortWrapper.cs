using System.IO.Ports;
using System.Text;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
///     Interface for serial port operations
///     Allows mocking SerialPort for unit testing
/// </summary>
public interface ISerialPort : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether the serial port is open
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    ///     Gets or sets the port for communications, including but not limited to all available COM ports
    /// </summary>
    string PortName { get; set; }

    /// <summary>
    ///     Gets or sets the serial baud rate
    /// </summary>
    int BaudRate { get; set; }

    /// <summary>
    ///     Gets or sets the standard length of data bits per byte
    /// </summary>
    int DataBits { get; set; }

    /// <summary>
    ///     Gets or sets the standard number of stopbits per byte
    /// </summary>
    StopBits StopBits { get; set; }

    /// <summary>
    ///     Gets or sets the parity-checking protocol
    /// </summary>
    Parity Parity { get; set; }

    /// <summary>
    ///     Gets or sets the size of the SerialPort input buffer
    /// </summary>
    int WriteBufferSize { get; set; }

    /// <summary>
    ///     Gets or sets the size of the SerialPort output buffer
    /// </summary>
    int ReadBufferSize { get; set; }

    /// <summary>
    ///     Gets or sets the byte encoding for pre- and post-transmission conversion of text
    /// </summary>
    Encoding Encoding { get; set; }

    /// <summary>
    ///     Gets or sets the handshaking protocol for serial port transmission of data
    /// </summary>
    Handshake Handshake { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the Request to Send (RTS) signal is enabled during serial communication
    /// </summary>
    bool RtsEnable { get; set; }

    /// <summary>
    ///     Gets or sets the number of milliseconds before a time-out occurs when a read operation does not finish
    /// </summary>
    int ReadTimeout { get; set; }

    /// <summary>
    ///     Gets the number of bytes of data in the receive buffer
    /// </summary>
    int BytesToRead { get; }

    /// <summary>
    ///     Opens a new serial port connection
    /// </summary>
    void Open();

    /// <summary>
    ///     Closes the port connection, sets the IsOpen property to false, and disposes of the internal Stream object
    /// </summary>
    void Close();

    /// <summary>
    ///     Synchronously reads one byte from the SerialPort input buffer
    /// </summary>
    /// <returns>The byte, cast to an Int32, or -1 if no byte was read</returns>
    int ReadByte();

    /// <summary>
    ///     Reads a number of bytes from the SerialPort input buffer and writes those bytes into a byte array at the specified offset
    /// </summary>
    /// <param name="buffer">The byte array to write the input to</param>
    /// <param name="offset">The offset in buffer at which to write the bytes</param>
    /// <param name="count">The maximum number of bytes to read</param>
    /// <returns>The number of bytes read</returns>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>
    ///     Reads up to the NewLine value in the input buffer
    /// </summary>
    /// <param name="value">A value that indicates where the read operation stops</param>
    /// <returns>The contents of the input buffer up to the specified value</returns>
    string ReadTo(string value);

    /// <summary>
    ///     Discards data from the serial driver's receive buffer
    /// </summary>
    void DiscardInBuffer();

    /// <summary>
    ///     Represents the method that handles the data received event of a SerialPort object
    /// </summary>
    event SerialDataReceivedEventHandler DataReceived;
}

/// <summary>
///     Wrapper for System.IO.Ports.SerialPort that implements ISerialPort
///     Allows dependency injection and mocking for unit testing
/// </summary>
public class SerialPortWrapper : ISerialPort
{
    private readonly SerialPort _serialPort;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of SerialPortWrapper with a new SerialPort
    /// </summary>
    public SerialPortWrapper()
    {
        _serialPort = new SerialPort();
    }

    /// <summary>
    ///     Initializes a new instance of SerialPortWrapper with an existing SerialPort
    ///     This constructor is useful for testing scenarios
    /// </summary>
    /// <param name="serialPort">The SerialPort instance to wrap</param>
    public SerialPortWrapper(SerialPort serialPort)
    {
        _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
    }

    /// <inheritdoc />
    public bool IsOpen => _serialPort.IsOpen;

    /// <inheritdoc />
    public string PortName
    {
        get => _serialPort.PortName;
        set => _serialPort.PortName = value;
    }

    /// <inheritdoc />
    public int BaudRate
    {
        get => _serialPort.BaudRate;
        set => _serialPort.BaudRate = value;
    }

    /// <inheritdoc />
    public int DataBits
    {
        get => _serialPort.DataBits;
        set => _serialPort.DataBits = value;
    }

    /// <inheritdoc />
    public StopBits StopBits
    {
        get => _serialPort.StopBits;
        set => _serialPort.StopBits = value;
    }

    /// <inheritdoc />
    public Parity Parity
    {
        get => _serialPort.Parity;
        set => _serialPort.Parity = value;
    }

    /// <inheritdoc />
    public int WriteBufferSize
    {
        get => _serialPort.WriteBufferSize;
        set => _serialPort.WriteBufferSize = value;
    }

    /// <inheritdoc />
    public int ReadBufferSize
    {
        get => _serialPort.ReadBufferSize;
        set => _serialPort.ReadBufferSize = value;
    }

    /// <inheritdoc />
    public Encoding Encoding
    {
        get => _serialPort.Encoding;
        set => _serialPort.Encoding = value;
    }

    /// <inheritdoc />
    public Handshake Handshake
    {
        get => _serialPort.Handshake;
        set => _serialPort.Handshake = value;
    }

    /// <inheritdoc />
    public bool RtsEnable
    {
        get => _serialPort.RtsEnable;
        set => _serialPort.RtsEnable = value;
    }

    /// <inheritdoc />
    public int ReadTimeout
    {
        get => _serialPort.ReadTimeout;
        set => _serialPort.ReadTimeout = value;
    }

    /// <inheritdoc />
    public int BytesToRead => _serialPort.BytesToRead;

    /// <inheritdoc />
    public void Open()
    {
        _serialPort.Open();
    }

    /// <inheritdoc />
    public void Close()
    {
        _serialPort.Close();
    }

    /// <inheritdoc />
    public int ReadByte()
    {
        return _serialPort.ReadByte();
    }

    /// <inheritdoc />
    public int Read(byte[] buffer, int offset, int count)
    {
        return _serialPort.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public string ReadTo(string value)
    {
        return _serialPort.ReadTo(value);
    }

    /// <inheritdoc />
    public void DiscardInBuffer()
    {
        _serialPort.DiscardInBuffer();
    }

    /// <inheritdoc />
    public event SerialDataReceivedEventHandler DataReceived
    {
        add => _serialPort.DataReceived += value;
        remove => _serialPort.DataReceived -= value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the unmanaged resources used by SerialPortWrapper and optionally releases the managed resources
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _serialPort?.Dispose();
            }

            _disposed = true;
        }
    }
}
