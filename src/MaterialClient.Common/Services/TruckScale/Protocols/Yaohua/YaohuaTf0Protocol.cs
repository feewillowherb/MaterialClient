using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;

public sealed class YaohuaTf0Protocol : IScaleTransmissionProtocol
{
    private const int FrameLength = 12;

    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.Yaohua &&
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

            byte[] readBuffer = new byte[FrameLength];
            int frameStartIndex = -1;
            int searchBufferSize = FrameLength * 3;

            try
            {
                int availableBytes = port.BytesToRead;
                if (availableBytes == 0)
                {
                    byte firstByte = (byte)port.ReadByte();
                    if (firstByte == 0x02)
                    {
                        readBuffer[0] = firstByte;
                        int receivedCount = 1;
                        while (receivedCount < FrameLength)
                        {
                            int bytesRead = port.Read(readBuffer, receivedCount, FrameLength - receivedCount);
                            receivedCount += bytesRead;
                        }
                    }
                    else
                    {
                        context.Logger?.LogWarning("Invalid first byte 0x{Byte:X2}, discarding", firstByte);
                        port.DiscardInBuffer();
                        return;
                    }
                }
                else
                {
                    int bytesToRead = Math.Min(availableBytes, searchBufferSize);
                    byte[] searchBuffer = new byte[bytesToRead];
                    int bytesRead = port.Read(searchBuffer, 0, bytesToRead);

                    if (bytesRead == 0)
                    {
                        context.Logger?.LogWarning("No data read from serial port");
                        port.DiscardInBuffer();
                        return;
                    }

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
                        context.Logger?.LogWarning(
                            "No valid frame start (0x02) found in {Count} bytes, discarding",
                            bytesRead);
                        port.DiscardInBuffer();
                        return;
                    }

                    int remainingInSearchBuffer = bytesRead - frameStartIndex;
                    int bytesToCopy = Math.Min(remainingInSearchBuffer, FrameLength);
                    Array.Copy(searchBuffer, frameStartIndex, readBuffer, 0, bytesToCopy);

                    if (bytesToCopy < FrameLength)
                    {
                        int receivedCount = bytesToCopy;
                        while (receivedCount < FrameLength)
                        {
                            int remainingBytes = FrameLength - receivedCount;
                            int additionalBytesRead = port.Read(readBuffer, receivedCount, remainingBytes);
                            receivedCount += additionalBytesRead;
                            if (additionalBytesRead == 0)
                                break;
                        }

                        if (receivedCount < FrameLength)
                        {
                            context.Logger?.LogWarning(
                                "Incomplete data read, expected {Expected} bytes, got {Actual}",
                                FrameLength,
                                receivedCount);
                            port.DiscardInBuffer();
                            return;
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                context.Logger?.LogWarning("Timeout reading data from truck scale");
                port.DiscardInBuffer();
                return;
            }

            if (readBuffer[0] != 0x02)
            {
                context.Logger?.LogWarning("Invalid frame start in readBuffer: 0x{Byte:X2}, discarding", readBuffer[0]);
                port.DiscardInBuffer();
                return;
            }

            if (readBuffer[FrameLength - 1] == 0x03)
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
            context.Logger?.LogWarning(ex, "Error receiving HEX data from Yaohua truck scale");
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
            var startIndex = 2;

            for (var i = startIndex; i < buffer.Length - 1; i++)
            {
                var b = buffer[i];
                if (b == 0x45) break;

                var c = (char)b;
                if (char.IsDigit(c) && weightString.Length < 6) weightString += c;
            }

            if (!string.IsNullOrEmpty(weightString) && weightString.Length >= 1)
            {
                if (decimal.TryParse(weightString, out var weightInt))
                {
                    if (isNegative) weightInt = -weightInt;

                    logger?.LogDebug(
                        "Parsed HEX weight: {Weight} (raw: {Raw}, sign: {Sign})",
                        weightInt,
                        weightString,
                        isNegative ? "-" : "+");

                    return weightInt;
                }

                logger?.LogWarning("Failed to parse weight string: {WeightString}", weightString);
            }
            else
            {
                logger?.LogWarning("No weight digits found in buffer");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error parsing HEX weight data");
        }

        return null;
    }
}
