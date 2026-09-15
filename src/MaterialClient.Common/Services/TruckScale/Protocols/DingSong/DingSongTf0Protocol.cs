using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.DingSong;

public sealed class DingSongTf0Protocol : IScaleTransmissionProtocol
{
    private const int FrameLength = 12;

    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.DingSong &&
        format == TransmissionFormatType.TransmissionFormatType0;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format)
    {
        if (!Supports(scaleType, format))
            throw new UnsupportedTransmissionFormatException(scaleType, format);
    }

    public void OnStart(ScaleProtocolContext context)
    {
    }

    public void OnDataReceived(ScaleProtocolContext context)
    {
        try
        {
            var port = context.GetSerialPort();
            if (port == null || !port.IsOpen) return;

            var receivedCount = 0;
            var readBuffer = new byte[FrameLength];

            while (receivedCount < FrameLength)
            {
                var bytesRead = port.Read(readBuffer, receivedCount, FrameLength - receivedCount);
                receivedCount += bytesRead;
            }

            if (readBuffer[0] == 0x02 && readBuffer[FrameLength - 1] == 0x03)
            {
                var parsedWeight = ParseHexWeight(readBuffer, context.Logger);
                if (parsedWeight.HasValue)
                {
                    var convertedWeight = context.ConvertWeight(parsedWeight.Value);
                    context.PublishWeight(convertedWeight);
                }

                port.DiscardInBuffer();
            }
            else
            {
                port.DiscardInBuffer();
            }
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error receiving HEX data from DingSong truck scale");
        }
    }

    public void OnStop()
    {
    }

    internal static decimal? ParseHexWeight(byte[] buffer, ILogger? logger)
    {
        try
        {
            if (buffer.Length < FrameLength) return null;

            if (buffer[0] != 0x02 || buffer[buffer.Length - 1] != 0x03)
            {
                logger?.LogWarning(
                    "Invalid frame format: STX={Stx:X2}, ETX={Etx:X2}",
                    buffer[0],
                    buffer[buffer.Length - 1]);
                return null;
            }

            var isNegative = buffer[1] == 0x2D;
            var weightString = string.Empty;
            const int startIndex = 2;
            const int endIndex = 10;

            for (var i = startIndex; i < endIndex; i++)
            {
                var b = buffer[i];
                var c = (char)b;

                if (char.IsDigit(c))
                {
                    weightString += c;
                }
                else
                {
                    logger?.LogWarning("Non-digit character found at position {Index}: 0x{Byte:X2}", i, b);
                    return null;
                }
            }

            if (weightString.Length != 8)
            {
                logger?.LogWarning("Expected 8 digits, got {Count}: {WeightString}", weightString.Length, weightString);
                return null;
            }

            var endMarker = buffer[10];
            if (endMarker < 0x30 || endMarker > 0x46)
            {
                logger?.LogWarning(
                    "Invalid end marker: 0x{Marker:X2}, expected hex character (0x30-0x46)",
                    endMarker);
                return null;
            }

            if (decimal.TryParse(weightString, out var weightInt))
            {
                var weight = weightInt;
                if (isNegative) weight = -weight;

                logger?.LogDebug(
                    "Parsed DingSong HEX weight: {Weight} (raw: {Raw}, sign: {Sign})",
                    weight,
                    weightString,
                    isNegative ? "-" : "+");

                return weight;
            }

            logger?.LogWarning("Failed to parse weight string: {WeightString}", weightString);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error parsing DingSong HEX weight data");
        }

        return null;
    }
}
