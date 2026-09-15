using System.Text;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;

/// <summary>
///     Production Yaohua Type1: Demo-aligned command/response exchange on a fixed interval.
///     Discard → Write → sync-read until ETX (same as Demo <c>Exchange</c>), not DataReceived listen.
/// </summary>
public sealed class YaohuaTf1Protocol : IScaleTransmissionProtocol
{
    public static readonly TimeSpan QueryInterval = TimeSpan.FromSeconds(10);
    public const int ReplyTimeoutMs = 700;

    private const int StandardFrameLength = 12;
    private const int ExtendedMinLength = 16;
    private const int MaxReplyBytes = 32;
    private const char QueryCommand = 'B';

    private readonly object _sync = new();
    private readonly SemaphoreSlim _io = new(1, 1);
    private Timer? _queryTimer;
    private ScaleProtocolContext? _context;
    private char _address = 'A';
    private volatile bool _stopped = true;

    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.Yaohua &&
        format == TransmissionFormatType.TransmissionFormatType1;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format)
    {
        if (!Supports(scaleType, format))
            throw new UnsupportedTransmissionFormatException(scaleType, format);
    }

    public void OnStart(ScaleProtocolContext context)
    {
        OnStop();

        _address = YaohuaCommunicationParameter.ResolveAddressOrThrow(
            context.Settings.CommunicationParameter);

        lock (_sync)
        {
            _context = context;
            _stopped = false;
        }

        context.Logger?.LogInformation(
            "Yaohua Type1 Demo-style exchange started. Address={Address} Command={Command} IntervalSeconds={Interval} ReplyTimeoutMs={Timeout}",
            _address,
            QueryCommand,
            QueryInterval.TotalSeconds,
            ReplyTimeoutMs);

        // dueTime=0: first exchange after OnStart returns (caller must not hold facade WriteLock).
        _queryTimer = new Timer(
            _ => RunExchangeTick(),
            null,
            TimeSpan.Zero,
            QueryInterval);
    }

    /// <summary>
    ///     Type1 owns I/O via <see cref="Exchange"/>; ignore DataReceived to avoid races with the timer.
    /// </summary>
    public void OnDataReceived(ScaleProtocolContext context)
    {
    }

    public void OnStop()
    {
        Timer? timer;
        lock (_sync)
        {
            _stopped = true;
            timer = _queryTimer;
            _queryTimer = null;
            _context = null;
        }

        if (timer != null)
        {
            try
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // already disposed
            }

            timer.Dispose();
        }
    }

    /// <summary>
    ///     Demo-compatible query: STX + Address + Command + XOR ASCII nibbles + ETX.
    /// </summary>
    internal static byte[] BuildQuery(char address, char command)
    {
        var addr = char.ToUpperInvariant(address);
        var cmd = char.ToUpperInvariant(command);
        var xor = addr ^ cmd;
        return
        [
            0x02,
            (byte)addr,
            (byte)cmd,
            ToAsciiNibble(xor >> 4),
            ToAsciiNibble(xor & 0x0F),
            0x03
        ];
    }

    /// <summary>
    ///     Demo <c>Exchange</c>: discard → write → read until ETX or timeout.
    /// </summary>
    internal static byte[] Exchange(ISerialPort port, byte[] request, int replyTimeoutMs = ReplyTimeoutMs)
    {
        port.DiscardInBuffer();
        port.Write(request, 0, request.Length);

        var received = new List<byte>(MaxReplyBytes);
        var deadline = Environment.TickCount64 + replyTimeoutMs;
        var sawStart = false;
        while (Environment.TickCount64 < deadline && received.Count < MaxReplyBytes)
        {
            int value;
            try
            {
                value = port.ReadByte();
            }
            catch (TimeoutException)
            {
                continue;
            }

            if (value < 0) continue;
            var b = (byte)value;
            if (!sawStart)
            {
                if (b != 0x02) continue;
                sawStart = true;
            }

            received.Add(b);
            if (b == 0x03) break;
        }

        return received.ToArray();
    }

    /// <summary>
    ///     Extended continuous frame: STX + Gross(6 digits) + Tare(6 digits) + status + CR/LF.
    /// </summary>
    internal static bool TryParseExtendedFrame(
        ReadOnlySpan<byte> data,
        out decimal gross,
        out decimal tare)
    {
        gross = 0;
        tare = 0;
        if (data.Length < ExtendedMinLength) return false;

        int stx = -1;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x02)
            {
                stx = i;
                break;
            }
        }

        if (stx < 0 || stx + 1 + 12 > data.Length) return false;

        if (stx + StandardFrameLength <= data.Length && data[stx + StandardFrameLength - 1] == 0x03)
            return false;

        var payload = data.Slice(stx + 1);
        if (payload.Length < 12) return false;

        var grossSpan = payload.Slice(0, 6);
        var tareSpan = payload.Slice(6, 6);
        if (!TryParseSixDigitField(grossSpan, out gross)) return false;
        if (!TryParseSixDigitField(tareSpan, out tare)) return false;

        return true;
    }

    internal static bool TryParseStandardFrame(
        ReadOnlySpan<byte> data,
        ILogger? logger,
        out decimal weight)
    {
        weight = 0;
        int stx = -1;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x02)
            {
                stx = i;
                break;
            }
        }

        if (stx < 0 || stx + StandardFrameLength > data.Length) return false;
        if (data[stx + StandardFrameLength - 1] != 0x03) return false;

        var frame = data.Slice(stx, StandardFrameLength).ToArray();
        var parsed = YaohuaTf0Protocol.ParseHexWeight(frame, logger);
        if (!parsed.HasValue) return false;
        weight = parsed.Value;
        return true;
    }

    /// <summary>
    ///     Demo-style command reply with weight for B/C/D (14-byte frame).
    /// </summary>
    internal static bool TryParseCommandReply(
        ReadOnlySpan<byte> data,
        ILogger? logger,
        out decimal weight,
        out char command)
    {
        weight = 0;
        command = '\0';

        var start = data.IndexOf((byte)0x02);
        if (start < 0) return false;
        var endRel = data[start..].IndexOf((byte)0x03);
        if (endRel < 0) return false;
        var frame = data.Slice(start, endRel + 1);
        if (frame.Length != 14) return false;

        var xor = 0;
        for (var i = 1; i < frame.Length - 3; i++)
            xor ^= frame[i];

        if (frame[^3] != ToAsciiNibble(xor >> 4) || frame[^2] != ToAsciiNibble(xor & 0x0F))
        {
            logger?.LogWarning("Yaohua Type1 command reply checksum mismatch");
            return false;
        }

        command = (char)frame[2];
        if (command is not ('B' or 'C' or 'D')) return false;
        if (frame[3] is not (0x2B or 0x2D)) return false;

        var digits = 0;
        for (var i = 4; i <= 9; i++)
        {
            var digit = frame[i] - (byte)'0';
            if (digit is < 0 or > 9) return false;
            digits = (digits * 10) + digit;
        }

        var decimalPlaces = frame[10] - (byte)'0';
        if (decimalPlaces is < 0 or > 4) return false;

        decimal value = digits;
        for (var i = 0; i < decimalPlaces; i++)
            value /= 10m;
        if (frame[3] == 0x2D)
            value = -value;

        weight = value;
        logger?.LogDebug(
            "Parsed Yaohua Type1 command reply: Command={Command} Weight={Weight}",
            command,
            weight);
        return true;
    }

    private void RunExchangeTick()
    {
        if (_stopped) return;

        ScaleProtocolContext? context;
        lock (_sync)
            context = _context;
        if (context == null || _stopped) return;

        if (!_io.Wait(0))
        {
            context.Logger?.LogDebug("Yaohua Type1 exchange skipped: previous tick still running");
            return;
        }

        try
        {
            if (_stopped) return;

            var port = context.GetSerialPort();
            if (port == null || !port.IsOpen)
            {
                context.Logger?.LogDebug("Yaohua Type1 exchange skipped: serial port not open");
                return;
            }

            var request = BuildQuery(_address, QueryCommand);
            context.Logger?.LogInformation(
                "Yaohua Type1 exchange TX. Address={Address} Command={Command} Hex={Hex}",
                _address,
                QueryCommand,
                Convert.ToHexString(request));

            var raw = Exchange(port, request, ReplyTimeoutMs);

            if (raw.Length == 0)
            {
                context.Logger?.LogWarning("Yaohua Type1 exchange timeout (no reply within {Timeout}ms)", ReplyTimeoutMs);
                return;
            }

            context.Logger?.LogInformation(
                "Yaohua Type1 exchange RX. Hex={Hex} Length={Length}",
                Convert.ToHexString(raw),
                raw.Length);

            if (TryParseCommandReply(raw, context.Logger, out var replyRaw, out var command))
            {
                var tons = context.ConvertWeight(replyRaw);
                context.PublishWeight(tons);
                context.PublishComponentWeights(ScaleComponentWeights.Invalid);
                context.Logger?.LogDebug(
                    "Yaohua Type1 exchange applied command reply {Command} weight={Weight}",
                    command,
                    tons);
                return;
            }

            if (TryParseExtendedFrame(raw, out var grossRaw, out var tareRaw))
            {
                var grossTon = context.ConvertWeight(grossRaw);
                var tareTon = context.ConvertWeight(tareRaw);
                context.PublishWeight(grossTon);
                context.PublishComponentWeights(ScaleComponentWeights.FromGrossTareTons(grossTon, tareTon));
                return;
            }

            if (TryParseStandardFrame(raw, context.Logger, out var displayRaw))
            {
                var displayTon = context.ConvertWeight(displayRaw);
                context.PublishWeight(displayTon);
                context.PublishComponentWeights(ScaleComponentWeights.Invalid);
                return;
            }

            context.Logger?.LogWarning(
                "Yaohua Type1 exchange unparsed reply len={Length} Hex={Hex}",
                raw.Length,
                Convert.ToHexString(raw));
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Yaohua Type1 exchange failed");
        }
        finally
        {
            _io.Release();
        }
    }

    private static bool TryParseSixDigitField(ReadOnlySpan<byte> field, out decimal value)
    {
        value = 0;
        var sb = new StringBuilder(6);
        foreach (var b in field)
        {
            var c = (char)b;
            if (c is >= '0' and <= '9' or '.' or '+' or '-')
                sb.Append(c);
            else if (c is ' ' or '\0')
                continue;
            else
                return false;
        }

        var text = sb.ToString().Trim();
        if (string.IsNullOrEmpty(text)) return false;
        return decimal.TryParse(text, out value);
    }

    private static byte ToAsciiNibble(int nibble) =>
        (byte)(nibble <= 9 ? nibble + 0x30 : nibble + 0x37);
}
