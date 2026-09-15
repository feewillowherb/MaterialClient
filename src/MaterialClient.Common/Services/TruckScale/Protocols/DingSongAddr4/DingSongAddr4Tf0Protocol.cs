using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.TruckScale.Protocols.DingSongAddr4;

public sealed class DingSongAddr4Tf0Protocol : IScaleTransmissionProtocol
{
    private const int FrameLength = 17;

    private readonly byte[] _buffer = new byte[4096];
    private int _bufferIndex;

    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.DingSongAddr4 &&
        format == TransmissionFormatType.TransmissionFormatType0;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format)
    {
        if (!Supports(scaleType, format))
            throw new UnsupportedTransmissionFormatException(scaleType, format);
    }

    public void OnStart(ScaleProtocolContext context)
    {
        _bufferIndex = 0;
    }

    public void OnDataReceived(ScaleProtocolContext context)
    {
        try
        {
            var port = context.GetSerialPort();
            if (port == null || !port.IsOpen) return;

            var availableBytes = port.BytesToRead;
            if (availableBytes <= 0) return;

            if (_bufferIndex + availableBytes > _buffer.Length)
            {
                context.Logger?.LogWarning("DingSong Addr4 receive buffer overflow, resetting");
                _bufferIndex = 0;
            }

            var bytesRead = port.Read(_buffer, _bufferIndex, availableBytes);
            if (bytesRead <= 0) return;

            _bufferIndex += bytesRead;

            while (_bufferIndex >= FrameLength)
            {
                var startIndex = FindFrameStart();
                if (startIndex < 0)
                {
                    if (_bufferIndex > FrameLength - 1)
                    {
                        Array.Copy(
                            _buffer,
                            _bufferIndex - (FrameLength - 1),
                            _buffer,
                            0,
                            FrameLength - 1);
                        _bufferIndex = FrameLength - 1;
                    }

                    return;
                }

                if (startIndex > 0)
                {
                    Array.Copy(_buffer, startIndex, _buffer, 0, _bufferIndex - startIndex);
                    _bufferIndex -= startIndex;
                }

                if (_bufferIndex < FrameLength) return;

                var message = new byte[FrameLength];
                Array.Copy(_buffer, 0, message, 0, FrameLength);

                var parsedWeight = ParseHexWeight(message, context.Logger);
                if (parsedWeight.HasValue)
                {
                    var convertedWeight = context.ConvertWeight(parsedWeight.Value);
                    context.PublishWeight(convertedWeight);
                }

                Array.Copy(
                    _buffer,
                    FrameLength,
                    _buffer,
                    0,
                    _bufferIndex - FrameLength);
                _bufferIndex -= FrameLength;
            }
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error receiving DingSong Addr4 HEX data from truck scale");
            _bufferIndex = 0;
        }
    }

    public void OnStop()
    {
        _bufferIndex = 0;
    }

    private int FindFrameStart()
    {
        for (var i = 0; i <= _bufferIndex - FrameLength; i++)
        {
            if (_buffer[i] != 0x02) continue;
            if (_buffer[i + 1] != 0x2A) continue;
            if (_buffer[i + FrameLength - 1] != 0x0D) continue;
            return i;
        }

        return -1;
    }

    internal static decimal? ParseHexWeight(byte[] buffer, ILogger? logger)
    {
        try
        {
            if (buffer.Length < FrameLength) return null;

            if (buffer[0] != 0x02 ||
                buffer[1] != 0x2A ||
                buffer[FrameLength - 1] != 0x0D)
            {
                return null;
            }

            var weightString = string.Empty;
            for (var i = 4; i < 10; i++)
            {
                var c = (char)buffer[i];
                if (!char.IsDigit(c))
                {
                    logger?.LogWarning("DingSong Addr4 non-digit at position {Index}: 0x{Byte:X2}", i, buffer[i]);
                    return null;
                }

                weightString += c;
            }

            for (var i = 10; i < 16; i++)
            {
                if (!char.IsDigit((char)buffer[i]))
                {
                    logger?.LogWarning("DingSong Addr4 non-digit at position {Index}: 0x{Byte:X2}", i, buffer[i]);
                    return null;
                }
            }

            if (decimal.TryParse(weightString, out var weightKg))
            {
                logger?.LogDebug("Parsed DingSong Addr4 HEX weight: {Weight} kg (raw: {Raw})", weightKg, weightString);
                return weightKg;
            }

            logger?.LogWarning("Failed to parse DingSong Addr4 weight string: {WeightString}", weightString);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error parsing DingSong Addr4 HEX weight data");
        }

        return null;
    }
}
