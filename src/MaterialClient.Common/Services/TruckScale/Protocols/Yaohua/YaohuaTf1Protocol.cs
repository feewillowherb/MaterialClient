using System.Text;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;

/// <summary>
///     Production Yaohua Type1: continuous-query listen path.
///     Parses arriving serial frames only; MUST NOT write host query commands.
/// </summary>
public sealed class YaohuaTf1Protocol : IScaleTransmissionProtocol
{
    private const int StandardFrameLength = 12;
    private const int ExtendedMinLength = 16;
    private const int MaxReadLength = 64;

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
        // Validate address parameter early; production Type1 never uses it to write queries.
        var address = YaohuaCommunicationParameter.ResolveAddressOrThrow(
            context.Settings.CommunicationParameter);
        context.Logger?.LogInformation(
            "Yaohua Type1 continuous-query listen started (no host query writes). Address={Address}",
            address);
    }

    public void OnDataReceived(ScaleProtocolContext context)
    {
        try
        {
            var port = context.GetSerialPort();
            if (port == null || !port.IsOpen) return;

            var buffer = new byte[MaxReadLength];
            int count;

            try
            {
                // Match Tf0: DataReceived may fire before BytesToRead is non-zero.
                int available = port.BytesToRead;
                if (available == 0)
                {
                    buffer[0] = (byte)port.ReadByte();
                    count = 1;
                    while (count < MaxReadLength && port.BytesToRead > 0)
                    {
                        int n = port.Read(buffer, count, Math.Min(port.BytesToRead, MaxReadLength - count));
                        if (n <= 0) break;
                        count += n;
                    }
                }
                else
                {
                    count = port.Read(buffer, 0, Math.Min(available, MaxReadLength));
                }
            }
            catch (TimeoutException)
            {
                context.Logger?.LogWarning("Timeout reading Yaohua Type1 continuous-query data");
                port.DiscardInBuffer();
                return;
            }

            if (count <= 0) return;

            var span = buffer.AsSpan(0, count);
            if (TryParseExtendedFrame(span, out var grossRaw, out var tareRaw))
            {
                var grossTon = context.ConvertWeight(grossRaw);
                var tareTon = context.ConvertWeight(tareRaw);
                var components = ScaleComponentWeights.FromGrossTareTons(grossTon, tareTon);
                context.PublishWeight(grossTon);
                context.PublishComponentWeights(components);
                port.DiscardInBuffer();
                return;
            }

            if (TryParseStandardFrame(span, context.Logger, out var displayRaw))
            {
                var displayTon = context.ConvertWeight(displayRaw);
                context.PublishWeight(displayTon);
                // Standard frame has no complete G/T/N trio → fall back to stable path.
                context.PublishComponentWeights(ScaleComponentWeights.Invalid);
                port.DiscardInBuffer();
                return;
            }

            port.DiscardInBuffer();
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error receiving continuous-query data from Yaohua Type1");
        }
    }

    public void OnStop()
    {
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

        // Reject classic 12-byte HEX frames that end with ETX (handled as standard).
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
}
